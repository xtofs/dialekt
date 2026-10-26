namespace Dialekt.Syntax;

using System.Collections;
using Dialekt.Green;

/// <summary>
/// Red node: a throw-away view of a green node together with its absolute position. It allocates nothing and caches
/// nothing; navigating just recreates views. A node is only ever created by asking its parent for a child, so it is
/// handed the handle and position of that parent, which makes <see cref="Parent"/> O(1). A struct cannot contain its
/// parent struct (a recursive layout), so only the immediate parent is remembered, as a handle and a position;
/// the parent returned by <see cref="Parent"/> does not know its own parent (see <see cref="Ancestors"/>).
/// The same green node can appear at several positions (hash-consed subtrees), so identity is (handle, position).
/// </summary>
public readonly struct SyntaxNode : IEquatable<SyntaxNode>
{
    private readonly GreenNodeHandle _parentHandle;
    private readonly int _parentPosition;

    internal SyntaxNode(SyntaxTree tree, GreenNodeHandle handle, int position, GreenNodeHandle parentHandle = default, int parentPosition = 0)
    {
        Tree = tree;
        Handle = handle;
        Position = position;
        _parentHandle = parentHandle;
        _parentPosition = parentPosition;
    }

    public SyntaxTree Tree { get; }
    public GreenNodeHandle Handle { get; }
    /// <summary>Absolute offset where the node starts, including its leading trivia.</summary>
    public int Position { get; }

    private GreenNodeReader Green => Tree.Arena.GetReader(Handle);

    public SyntaxKind Kind => Green.Kind;
    public GreenNodeFlags Flags => Green.Flags;
    public int ChildCount => Green.ChildCount;
    public int FullWidth => Green.FullWidth;
    public int End => Position + FullWidth;

    public bool IsToken => Green.IsToken;
    public bool IsTrivia => Green.IsTrivia;
    public bool ContainsError => Green.ContainsError;

    /// <summary>Absolute offset where the node's text starts, i.e. after the leading trivia of its first token.</summary>
    public int TextStart => Position + LeadingTriviaWidth;
    public int TextWidth => End - TextStart;

    /// <summary>Width of the whitespace and comments before the first token of this node.</summary>
    public int LeadingTriviaWidth => Green.TryGetFirstToken(out var token) ? token.LeadingTriviaWidth : 0;

    /// <summary>The source text of the node without its leading trivia.</summary>
    public string Text => Tree.Source.Substring(TextStart, TextWidth);

    /// <summary>The source text of the node including its leading trivia.</summary>
    public string FullText => Tree.Source.Substring(Position, FullWidth);

    /// <summary>1-based line and column where the node's text starts.</summary>
    public (int Line, int Column) StartLineAndColumn => Tree.LineMap.GetLineAndColumn(TextStart);

    /// <summary>
    ///   The child in <paramref name="slot"/>; its position is the sum 
    ///   of the widths of the slots before it.
    /// </summary>
    public SyntaxNode GetChild(int slot)
    {
        var green = Green;
        if ((uint)slot >= (uint)green.ChildCount) throw new ArgumentOutOfRangeException(nameof(slot));

        var position = Position;
        for (var i = 0; i < slot; i++)
        {
            position += green.GetChild(i).FullWidth;
        }
        return new SyntaxNode(Tree, green.GetChildHandle(slot), position, Handle, Position);
    }

    /// <summary>
    /// The child in the slot called <paramref name="name"/> (see <see cref="Language.GetSlots"/>). For an optional part that is not
    /// there this is an <see cref="SyntaxKind.Absent"/> node (check <see cref="IsAbsent"/>); for a list slot it is the list node,
    /// whose <see cref="Children"/> are the items.
    /// </summary>
    /// <exception cref="ArgumentException">The kind has no slot with that name.</exception>
    public SyntaxNode GetSlot(string name)
    {
        return TryGetSlot(name, out var slot)
            ? slot
            : throw new ArgumentException(
                $"{Tree.Language.GetKindName(Kind)} has no slot '{name}'; its slots are: {string.Join(", ", Tree.Language.GetSlots(Kind).ToArray().Select(s => s.Name))}.", nameof(name));
    }

    public bool TryGetSlot(string name, out SyntaxNode slot)
    {
        var index = Tree.Language.IndexOf(Kind, name);
        slot = index >= 0 ? GetChild(index) : default;
        return index >= 0;
    }

    /// <summary>An optional part that is not there.</summary>
    public bool IsAbsent => Kind == SyntaxKind.Absent;

    /// <summary>A required part the parser expected but did not find.</summary>
    public bool IsMissing => (Flags & GreenNodeFlags.IsMissing) != 0;

    /// <summary>All children; for a token these are its leading trivia.</summary>
    public ChildList Children => new ChildList(this);

    /// <summary>True for the root of the tree.</summary>
    public bool IsRoot => Handle == Tree.Green.RootHandle;

    /// <summary>
    /// The node that has this node as a child, or null for the root. O(1) for a node that was reached by navigating down from
    /// its parent. The node returned does not know its own parent: asking it for <c>Parent</c> costs a descent from the root
    /// (<see cref="SyntaxTree.GetParent"/>); use <see cref="Ancestors"/> to walk all the way up.
    /// </summary>
    public SyntaxNode? Parent
    {
        get
        {
            if (IsRoot) return null;
            return _parentHandle.IsNull ? Tree.GetParent(this) : new SyntaxNode(Tree, _parentHandle, _parentPosition);
        }
    }

    /// <summary>The parent, grandparent, ... up to the root. One descent from the root, however many levels there are.</summary>
    public IEnumerable<SyntaxNode> Ancestors() => Tree.Ancestors(this);

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
            // zero-width children (absent, empty lists) cover no offset; at the very end the last child with a token is the answer
            SyntaxNode? next = null;
            foreach (var child in node.Children)
            {
                if (child.Position <= offset && offset < child.End)
                {
                    next = child;
                    break;
                }
                if (offset == node.End && child.Green.TryGetFirstToken(out _))
                {
                    next = child;
                }
            }
            node = next ?? throw new InvalidOperationException("No child covers the offset.");
        }
        return node;
    }

    public bool Equals(SyntaxNode other) => ReferenceEquals(Tree, other.Tree) && Handle == other.Handle && Position == other.Position;
    public override bool Equals(object? obj) => obj is SyntaxNode other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Tree, Handle, Position);
    public static bool operator ==(SyntaxNode left, SyntaxNode right) => left.Equals(right);
    public static bool operator !=(SyntaxNode left, SyntaxNode right) => !left.Equals(right);

    public override string ToString() => $"{Tree.Language.GetKindName(Kind)} [{Position}..{End})";

    /// <summary>Allocation-free enumeration of the children of a node.</summary>
    public readonly struct ChildList(SyntaxNode parent) : IEnumerable<SyntaxNode>
    {
        public int Count => parent.ChildCount;

        public Enumerator GetEnumerator() => new(parent);
        IEnumerator<SyntaxNode> IEnumerable<SyntaxNode>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator(SyntaxNode parent) : IEnumerator<SyntaxNode>
        {
            private int _index = -1;
            private int _position = parent.Position;

            public SyntaxNode Current { get; private set; }
            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                var green = parent.Green;
                if (_index >= 0)
                {
                    _position += Current.FullWidth;
                }
                if (++_index >= green.ChildCount)
                {
                    return false;
                }
                Current = new SyntaxNode(parent.Tree, green.GetChildHandle(_index), _position, parent.Handle, parent.Position);
                return true;
            }

            public void Reset() => throw new NotSupportedException();
            public void Dispose() { }
        }
    }
}
