namespace Dialekt.Parsing;

using System.Runtime.InteropServices;
using Dialekt.Green;
using Dialekt.Scanning;
using Dialekt.Syntax;

/// <summary>
/// The language-independent machinery of a recursive-descent parser that builds a lossless green tree (every token and all
/// whitespace/comments are in it) directly into a <see cref="GreenArena"/>. A language derives from it and adds one
/// <c>Parse...</c> method per grammar rule; it consumes tokens with <see cref="Advance"/> and turns what it has consumed
/// into nodes with <see cref="Mark"/> and <see cref="Close"/>.
/// <para>
/// <see cref="Close"/> fits the children to the fixed slots of the node kind (<see cref="Language.GetSlots"/>): optional
/// parts that are not there become <see cref="SyntaxKind.Absent"/>, required ones <see cref="SyntaxKind.Missing"/>, and runs
/// of items are wrapped in list nodes.
/// </para>
/// <para>
/// Given the previous tree and the edit that produced the new text, whole items of the root's reusable list (for a
/// grammar: its declarations) that the edit did not touch are taken over from the previous tree instead of being parsed again.
/// </para>
/// </summary>
public abstract class ParserBase
{
    /// <summary>Thrown by <see cref="Fail"/>; a language catches it where it can resynchronize (see the error nodes).</summary>
    protected sealed class ParseException : Exception;

    private readonly Language _language;
    private readonly string _source;
    private readonly GreenArena _arena;
    private readonly GreenNodeCache _cache;

    // One entry per non-trivia token: the scanner token and its green node (leading trivia + text).
    private readonly List<Token> _tokens = [];
    private readonly List<GreenNodeHandle> _tokenNodes = [];
    private readonly List<int> _tokenWidths = [];
    private readonly List<int> _tokenFullStarts = [];

    // Completed nodes and consumed tokens not yet owned by a node; Close(mark, kind) turns everything after `mark` into a node.
    private readonly List<GreenNodeHandle> _children = [];
    private readonly List<int> _widths = [];

    private readonly List<Diagnostic> _diagnostics = [];
    private int _pos;

    // Items of the previous tree's reusable list that can be reused, by their start offset in the new text.
    private readonly Dictionary<int, ReusableItem>? _reusable;
    private int _reused;
    private int _reparsed;

    private readonly record struct ReusableItem(GreenNodeHandle Handle, int Width, int NewEnd, GreenNodeHandle FirstToken, GreenNodeHandle LastToken, SyntaxKind LastTokenKind);

    /// <param name="previous">An earlier tree: its nodes are shared with the new tree (the arena is forked).</param>
    /// <param name="change">The edit that turned the text of <paramref name="previous"/> into <paramref name="content"/>; enables reuse.</param>
    protected ParserBase(string content, Language language, ITokenizer tokenizer, SyntaxTree? previous, TextChange? change)
    {
        _language = language;
        _source = content;
        // Forking keeps all nodes of the previous version addressable, so identical subtrees are shared with it.
        _arena = previous is null ? new GreenArena(content.Length * 4) : previous.Arena.Fork();
        _cache = previous is null ? new GreenNodeCache(_arena) : GreenNodeCache.FromArena(_arena);
        if (previous is not null && change is { } edit && ReusableListSlot >= 0)
        {
            _reusable = FindReusableItems(previous, edit);
        }

        var trivia = new List<GreenNodeHandle>();
        var triviaWidth = 0;
        var fullStart = 0;
        foreach (var token in tokenizer.Scan(content))
        {
            if (token.Kind == SyntaxKind.ErrorToken)
            {
                _diagnostics.Add(new Diagnostic(token.Error!, token.Span.Start.Line, token.Span.Start.Column, token.Text.Length, true));
            }
            if (language.IsTriviaKind(token.Kind))
            {
                trivia.Add(_cache.GetOrAdd(token.Kind, token.Text.Length, [], GreenNodeFlags.IsTrivia));
                triviaWidth += token.Text.Length;
                continue;
            }

            var flags = GreenNodeFlags.IsToken | (token.Kind == SyntaxKind.ErrorToken ? GreenNodeFlags.IsError : GreenNodeFlags.None);
            var width = triviaWidth + token.Text.Length;
            _tokens.Add(token);
            _tokenNodes.Add(_cache.GetOrAdd(token.Kind, width, CollectionsMarshal.AsSpan(trivia), flags));
            _tokenWidths.Add(width);
            _tokenFullStarts.Add(fullStart);
            fullStart += width;
            trivia.Clear();
            triviaWidth = 0;
        }
    }

    // ---------------------------------------------------------------- what a language provides

    /// <summary>Builds the tree: consumes all tokens, appends the end-of-file token and returns the root node.</summary>
    protected abstract GreenNodeHandle ParseRoot();

    /// <summary>
    /// Index of the slot of the root node that holds the list of independent items (a grammar's declarations) that can be
    /// reused after an edit, or -1 to turn reuse off.
    /// </summary>
    protected virtual int ReusableListSlot => -1;

    /// <summary>
    /// Whether an item that ends with a token of kind <paramref name="lastTokenKind"/> parses the same when followed by
    /// <paramref name="next"/>. A parser that looks at the following token to decide that an item is over (an optional
    /// closing ';') must say so here; the default accepts everything.
    /// </summary>
    protected virtual bool IsSafeToReuse(SyntaxKind lastTokenKind, Token next) => true;

    /// <summary>Runs the parser and packages the sealed tree with the diagnostics.</summary>
    protected ParseResult Run()
    {
        var root = ParseRoot();
        _arena.Seal();
        var tree = new SyntaxTree(new GreenTree(_arena, root), _source, _language);
        return new ParseResult(tree, _diagnostics.OrderBy(d => d.Line).ThenBy(d => d.Column).ToList(), _reused, _reparsed);
    }

    // ---------------------------------------------------------------- reuse

    private Dictionary<int, ReusableItem> FindReusableItems(SyntaxTree previous, TextChange change)
    {
        var result = new Dictionary<int, ReusableItem>();
        var root = previous.Arena.GetReader(previous.Green.RootHandle);
        var slot = ReusableListSlot;
        var items = root.GetChild(slot);
        var start = 0;
        for (var i = 0; i < slot; i++)
        {
            start += root.GetChild(i).FullWidth;
        }
        for (var i = 0; i < items.ChildCount; i++)
        {
            var node = items.GetChild(i);
            var end = start + node.FullWidth;
            var delta = end <= change.Start ? 0 : start >= change.OldEnd ? change.Delta : int.MinValue;
            if (delta != int.MinValue && !node.IsToken && !node.ContainsError)
            {
                if (node.TryGetFirstToken(out var first) && node.TryGetLastToken(out var last))
                {
                    result[start + delta] = new ReusableItem(node.Handle, node.FullWidth, end + delta, first.Handle, last.Handle, last.Kind);
                }
            }
            start = end;
        }
        return result;
    }

    /// <summary>
    /// If the previous tree has an item at the current token whose tokens are all unchanged in the new text, appends it as
    /// is, skips its tokens and returns true. Comparing the first and last token nodes is enough: the token nodes carry
    /// their leading trivia and width, tokens are scanned left to right from a known boundary, and the text in between is
    /// the old text. Items with errors, and items that touch the edit, are never reused.
    /// </summary>
    protected bool TryReuse()
    {
        if (_reusable is null || !_reusable.TryGetValue(_tokenFullStarts[_pos], out var old)) return false;

        var next = _tokenFullStarts.BinarySearch(old.NewEnd);
        if (next <= _pos || _tokenNodes[_pos] != old.FirstToken || _tokenNodes[next - 1] != old.LastToken) return false;
        if (!IsSafeToReuse(old.LastTokenKind, _tokens[next])) return false;

        _children.Add(old.Handle);
        _widths.Add(old.Width);
        _pos = next;
        _reused++;
        return true;
    }

    /// <summary>Counts an item that is parsed from tokens (for <see cref="ParseResult.ReparsedDeclarations"/>).</summary>
    protected void CountReparsed() => _reparsed++;

    // ---------------------------------------------------------------- tokens

    protected Token Cur => _tokens[_pos];
    protected Token Peek(int n) => _tokens[Math.Min(_pos + n, _tokens.Count - 1)];
    protected bool AtEof => Cur.Kind == SyntaxKind.EndOfFileToken;

    /// <summary>Index of the current token; compare two values to see whether anything was consumed.</summary>
    protected int Pos => _pos;

    /// <summary>Consumes the current token (never the end of file) and hands its green node to the enclosing node.</summary>
    protected Token Advance()
    {
        var t = Cur;
        if (!AtEof)
        {
            _children.Add(_tokenNodes[_pos]);
            _widths.Add(_tokenWidths[_pos]);
            _pos++;
        }
        return t;
    }

    /// <summary>Appends the end-of-file token, which carries the trailing whitespace and comments.</summary>
    protected void AppendEndOfFile()
    {
        _children.Add(_tokenNodes[_pos]);
        _widths.Add(_tokenWidths[_pos]);
    }

    /// <summary>The most recently closed node, e.g. the root after the last <see cref="Close"/>.</summary>
    protected GreenNodeHandle LastNode => _children[^1];

    // ---------------------------------------------------------------- errors

    /// <summary>Records "Expected ... but found ..." at the current token (once per position) and returns the exception to throw.</summary>
    protected ParseException Fail(string expected)
    {
        var t = Cur;
        // Error tokens were already reported by the scanner.
        if (t.Kind != SyntaxKind.ErrorToken)
        {
            var found = t.Kind == SyntaxKind.EndOfFileToken ? "end of file" : $"'{Shorten(t.Text)}'";
            var (line, column) = (t.Span.Start.Line, t.Span.Start.Column);
            if (!_diagnostics.Any(d => d.Line == line && d.Column == column))
            {
                _diagnostics.Add(new Diagnostic($"Expected {expected} but found {found}", line, column, Math.Max(t.Text.Length, 1), false));
            }
        }
        return new ParseException();
    }

    private static string Shorten(string text) => text.Length > 20 ? text[..20] + "…" : text;

    // ---------------------------------------------------------------- node construction

    /// <summary>Position in the child list; everything added after it can later be closed into a node.</summary>
    protected int Mark() => _children.Count;

    /// <summary>
    /// Makes all children added since <paramref name="mark"/> the children of a new node of the given kind, fitted to the
    /// kind's slots (see the class remarks). An <see cref="SyntaxKind.Error"/> node keeps its children as they are.
    /// </summary>
    protected void Close(int mark, SyntaxKind kind)
    {
        var count = _children.Count - mark;
        var width = 0;
        for (var i = mark; i < _widths.Count; i++)
        {
            width += _widths[i];
        }
        // Error nodes are flagged so that ContainsError tells which declarations had syntax errors (they are never reused).
        var flags = kind == SyntaxKind.Error ? GreenNodeFlags.IsError : GreenNodeFlags.None;
        var children = CollectionsMarshal.AsSpan(_children).Slice(mark, count);
        var slots = _language.GetSlots(kind);
        var handle = slots.IsEmpty
            ? _cache.GetOrAdd(kind, width, children, flags)
            : _cache.GetOrAdd(kind, width, FitToSlots(kind, slots, children), flags);
        _children.RemoveRange(mark, count);
        _widths.RemoveRange(mark, count);
        _children.Add(handle);
        _widths.Add(width);
    }

    private readonly List<GreenNodeHandle> _slotted = [];

    /// <summary>Greedy left-to-right assignment of the children to the slots; see <see cref="Close"/>.</summary>
    private ReadOnlySpan<GreenNodeHandle> FitToSlots(SyntaxKind kind, ReadOnlySpan<SlotSpec> slots, ReadOnlySpan<GreenNodeHandle> children)
    {
        _slotted.Clear();
        var next = 0;
        foreach (var slot in slots)
        {
            switch (slot.Shape)
            {
                case SlotShape.Required or SlotShape.Optional:
                    if (next < children.Length && _language.Accepts(slot, KindOf(children[next])))
                    {
                        _slotted.Add(children[next++]);
                    }
                    else
                    {
                        _slotted.Add(slot.Shape == SlotShape.Optional ? AbsentNode() : MissingNode(slot));
                    }
                    break;

                case SlotShape.List:
                    var start = next;
                    while (next < children.Length && _language.Accepts(slot, KindOf(children[next])))
                    {
                        next++;
                    }
                    _slotted.Add(ListNode(SyntaxKind.List, children[start..next]));
                    break;

                case SlotShape.SeparatedList:
                    start = next;
                    if (next < children.Length && _language.Accepts(slot, KindOf(children[next])))
                    {
                        next++;
                        while (next + 1 < children.Length && KindOf(children[next]) == slot.Separator && _language.Accepts(slot, KindOf(children[next + 1])))
                        {
                            next += 2;
                        }
                    }
                    _slotted.Add(ListNode(SyntaxKind.SeparatedList, children[start..next]));
                    break;
            }
        }
        if (next < children.Length)
        {
            throw new InvalidOperationException(
                $"{_language.GetKindName(KindOf(children[next]))} does not fit into the slots of {_language.GetKindName(kind)}; the parser and the schema of {_language.Name} disagree.");
        }
        return CollectionsMarshal.AsSpan(_slotted);
    }

    private SyntaxKind KindOf(GreenNodeHandle handle) => _arena.GetReader(handle).Kind;

    private GreenNodeHandle AbsentNode() => _cache.GetOrAdd(SyntaxKind.Absent, 0, []);

    private GreenNodeHandle MissingNode(SlotSpec slot) =>
        slot.Category == 0 && _language.IsTokenKind(slot.Kind)
            ? _cache.GetOrAdd(slot.Kind, 0, [], GreenNodeFlags.IsToken | GreenNodeFlags.IsMissing)
            : _cache.GetOrAdd(SyntaxKind.Missing, 0, [], GreenNodeFlags.IsMissing);

    private GreenNodeHandle ListNode(SyntaxKind listKind, ReadOnlySpan<GreenNodeHandle> items)
    {
        var width = 0;
        foreach (var item in items)
        {
            width += _arena.GetReader(item).FullWidth;
        }
        return _cache.GetOrAdd(listKind, width, items);
    }
}
