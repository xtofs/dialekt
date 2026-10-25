namespace Dialekt.Syntax;

using Dialekt.Green;

/// <summary>
/// Red node: a green node together with its parent and absolute position. Red nodes are created on demand while
/// walking down from <see cref="SyntaxTree.Root"/> and cached, so a node and its parent chain are always consistent.
/// A green node that occurs in several places (the same subtree shared by many parents) has one red node per place.
/// </summary>
public sealed class SyntaxNode
{
    private SyntaxNode?[]? _children;
    private int[]? _childOffsets;
    private int _leadingTriviaWidth = -1;

    internal SyntaxNode(SyntaxTree tree, SyntaxNode? parent, GreenNodeHandle handle, int position, int slotInParent)
    {
        Tree = tree;
        Parent = parent;
        Handle = handle;
        Position = position;
        SlotInParent = slotInParent;

        var green = tree.Arena.GetReader(handle);
        Kind = green.Kind;
        FullWidth = green.FullWidth;
        Flags = green.Flags;
        ChildCount = green.ChildCount;
    }

    public SyntaxTree Tree { get; }
    public SyntaxNode? Parent { get; }
    public GreenNodeHandle Handle { get; }
    /// <summary>Index of this node in its parent's slots; 0 for the root.</summary>
    public int SlotInParent { get; }

    public SyntaxKind Kind { get; }
    public GreenNodeFlags Flags { get; }
    public int ChildCount { get; }

    /// <summary>Absolute offset where the node starts, including its leading trivia.</summary>
    public int Position { get; }
    public int FullWidth { get; }
    public int End => Position + FullWidth;

    public bool IsToken => (Flags & GreenNodeFlags.IsToken) != 0;
    public bool IsTrivia => (Flags & GreenNodeFlags.IsTrivia) != 0;
    public bool ContainsError => (Flags & GreenNodeFlags.ContainsError) != 0;

    /// <summary>Absolute offset where the node's text starts, i.e. after the leading trivia of its first token.</summary>
    public int TextStart => Position + LeadingTriviaWidth;
    public int TextWidth => End - TextStart;

    /// <summary>Width of the whitespace and comments before the first token of this node.</summary>
    public int LeadingTriviaWidth
    {
        get
        {
            if (_leadingTriviaWidth < 0)
            {
                var green = Tree.Arena.GetReader(Handle);
                while (!green.IsToken && green.ChildCount > 0)
                {
                    green = green.GetChild(0);
                }
                _leadingTriviaWidth = green.IsToken ? green.LeadingTriviaWidth : 0;
            }
            return _leadingTriviaWidth;
        }
    }

    /// <summary>The source text of the node without its leading trivia.</summary>
    public string Text => Tree.Source.Substring(TextStart, TextWidth);
    public string FullText => Tree.Source.Substring(Position, FullWidth);

    /// <summary>1-based line and column where the node's text starts.</summary>
    public (int Line, int Column) StartLineAndColumn => Tree.LineMap.GetLineAndColumn(TextStart);

    public SyntaxNode GetChild(int slot)
    {
        if ((uint)slot >= (uint)ChildCount) throw new ArgumentOutOfRangeException(nameof(slot));

        var children = _children ?? Interlocked.CompareExchange(ref _children, new SyntaxNode?[ChildCount], null) ?? _children!;
        var existing = Volatile.Read(ref children[slot]);
        if (existing is not null) return existing;

        var green = Tree.Arena.GetReader(Handle);
        var created = new SyntaxNode(Tree, this, green.GetChildHandle(slot), Position + ChildOffsets[slot], slot);
        return Interlocked.CompareExchange(ref children[slot], created, null) ?? created;
    }

    /// <summary>Offset of each child from this node's position, plus the total as the last entry.</summary>
    private int[] ChildOffsets
    {
        get
        {
            if (_childOffsets is null)
            {
                var green = Tree.Arena.GetReader(Handle);
                var offsets = new int[ChildCount + 1];
                for (var i = 0; i < ChildCount; i++)
                {
                    offsets[i + 1] = offsets[i] + green.GetChild(i).FullWidth;
                }
                _childOffsets = offsets;
            }
            return _childOffsets;
        }
    }

    /// <summary>All children; for a token these are its leading trivia.</summary>
    public IEnumerable<SyntaxNode> Children
    {
        get
        {
            for (var i = 0; i < ChildCount; i++)
            {
                yield return GetChild(i);
            }
        }
    }

    public IEnumerable<SyntaxNode> Ancestors()
    {
        for (var node = Parent; node is not null; node = node.Parent)
        {
            yield return node;
        }
    }

    /// <summary>Pre-order traversal of this node and everything below it (trivia included).</summary>
    public IEnumerable<SyntaxNode> DescendantsAndSelf()
    {
        var pending = new Stack<SyntaxNode>();
        pending.Push(this);
        while (pending.TryPop(out var node))
        {
            yield return node;
            for (var i = node.ChildCount - 1; i >= 0; i--)
            {
                pending.Push(node.GetChild(i));
            }
        }
    }

    public IEnumerable<SyntaxNode> Descendants() => DescendantsAndSelf().Skip(1);

    /// <summary>
    /// The token whose full span (leading trivia + text) contains <paramref name="offset"/>; the end-of-file token for
    /// the end of the file. Whitespace and comments therefore belong to the token that follows them.
    /// </summary>
    public SyntaxNode FindToken(int offset)
    {
        if (offset < Position || offset > End) throw new ArgumentOutOfRangeException(nameof(offset));

        var node = this;
        while (!node.IsToken)
        {
            if (node.ChildCount == 0) throw new InvalidOperationException("Node without tokens.");
            var offsets = node.ChildOffsets;
            var relative = offset - node.Position;
            var slot = Array.BinarySearch(offsets, 0, node.ChildCount, relative);
            slot = slot >= 0 ? slot : ~slot - 1;
            // zero-width children can share an offset; the last child starts at or before the end.
            slot = Math.Min(slot, node.ChildCount - 1);
            node = node.GetChild(slot);
        }
        return node;
    }

    public override string ToString() => $"{Kind} [{Position}..{End})";
}
