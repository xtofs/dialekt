namespace Dialekt.Syntax;

using System.Collections;
using Dialekt.Green;

/// <summary>
/// Red node: a throw-away view of a green node together with its absolute position. It holds only the tree, the
/// green handle and the position (16 bytes), allocates nothing and caches nothing; navigating just recreates views.
/// There is no parent pointer: <see cref="Parent"/> and <see cref="Ancestors"/> re-descend from the root.
/// The same green node can appear at several positions (hash-consed subtrees), so identity is (handle, position).
/// </summary>
public readonly struct SyntaxNode : IEquatable<SyntaxNode>
{
    internal SyntaxNode(SyntaxTree tree, GreenNodeHandle handle, int position)
    {
        Tree = tree;
        Handle = handle;
        Position = position;
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
    public int LeadingTriviaWidth
    {
        get
        {
            return Green.TryGetFirstToken(out var token) ? token.LeadingTriviaWidth : 0;
        }
    }

    /// <summary>The source text of the node without its leading trivia.</summary>
    public string Text => Tree.Source.Substring(TextStart, TextWidth);
    public string FullText => Tree.Source.Substring(Position, FullWidth);

    /// <summary>1-based line and column where the node's text starts.</summary>
    public (int Line, int Column) StartLineAndColumn => Tree.LineMap.GetLineAndColumn(TextStart);

    /// <summary>The child in <paramref name="slot"/>; its position is the sum of the widths of the slots before it.</summary>
    public SyntaxNode GetChild(int slot)
    {
        var green = Green;
        if ((uint)slot >= (uint)green.ChildCount) throw new ArgumentOutOfRangeException(nameof(slot));

        var position = Position;
        for (var i = 0; i < slot; i++)
        {
            position += green.GetChild(i).FullWidth;
        }
        return new SyntaxNode(Tree, green.GetChildHandle(slot), position);
    }

    /// <summary>All children; for a token these are its leading trivia.</summary>
    public ChildList Children => new(this);

    /// <summary>The node that has this node as a child, or null for the root. Costs a descent from the root; see <see cref="SyntaxTree.GetParent"/>.</summary>
    public SyntaxNode? Parent => Tree.GetParent(this);

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

    public override string ToString() => $"{Kind} [{Position}..{End})";

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
                Current = new SyntaxNode(parent.Tree, green.GetChildHandle(_index), _position);
                return true;
            }

            public void Reset() => throw new NotSupportedException();
            public void Dispose() { }
        }
    }
}
