namespace Dialekt.Syntax;

using System.Collections;
using System.Diagnostics;
using Dialekt.Green;

/// <summary>A typed, throw-away view of a <see cref="SyntaxNode"/> of a particular kind (or category of kinds).</summary>
public interface ISyntaxView<TSelf> where TSelf : struct, ISyntaxView<TSelf>
{
    SyntaxNode Node { get; }
    static abstract TSelf Create(SyntaxNode node);
}

/// <summary>Helpers used by the generated views (see <c>tools/SyntaxGenerator</c>).</summary>
internal static class SyntaxViews
{
    public static T Required<T>(SyntaxNode parent, int slot) where T : struct, ISyntaxView<T> => T.Create(parent.GetChild(slot));

    /// <summary>The view of an optional slot, or null if the part is absent.</summary>
    public static T? Optional<T>(SyntaxNode parent, int slot) where T : struct, ISyntaxView<T>
    {
        var child = parent.GetChild(slot);
        return child.Kind == SyntaxKind.Absent ? null : T.Create(child);
    }

    [Conditional("DEBUG")]
    public static void AssertKind(SyntaxNode node, SyntaxKind kind) =>
        Debug.Assert(node.Kind == kind || node.Kind == SyntaxKind.Missing || (node.Flags & GreenNodeFlags.IsMissing) != 0, $"Expected {kind} but got {node.Kind}.");

    [Conditional("DEBUG")]
    public static void AssertCategory(SyntaxNode node, SyntaxCategory category) =>
        Debug.Assert(SyntaxSchema.IsInCategory(category, node.Kind) || node.Kind == SyntaxKind.Missing || (node.Flags & GreenNodeFlags.IsMissing) != 0, $"{node.Kind} is not a {category}.");
}

/// <summary>A token with its leading trivia.</summary>
public readonly struct SyntaxToken : ISyntaxView<SyntaxToken>, IEquatable<SyntaxToken>
{
    public SyntaxToken(SyntaxNode node)
    {
        Debug.Assert(node.IsToken, $"{node.Kind} is not a token.");
        Node = node;
    }

    public SyntaxNode Node { get; }
    public static SyntaxToken Create(SyntaxNode node) => new(node);

    public SyntaxKind Kind => Node.Kind;
    /// <summary>True for a token the parser expected but did not find; it has no text.</summary>
    public bool IsMissing => (Node.Flags & GreenNodeFlags.IsMissing) != 0;
    public string Text => IsMissing ? "" : Node.Text;
    /// <summary>Offset where the token text starts (after the leading trivia).</summary>
    public int Position => Node.TextStart;
    public int FullPosition => Node.Position;
    public SyntaxNode.ChildList LeadingTrivia => Node.Children;
    public (int Line, int Column) StartLineAndColumn => Node.StartLineAndColumn;

    public bool Equals(SyntaxToken other) => Node.Equals(other.Node);
    public override bool Equals(object? obj) => obj is SyntaxToken other && Equals(other);
    public override int GetHashCode() => Node.GetHashCode();
    public override string ToString() => Text;
    public static implicit operator SyntaxNode(SyntaxToken token) => token.Node;
}

/// <summary>The items of a <see cref="SyntaxKind.List"/> node.</summary>
public readonly struct SyntaxList<T>(SyntaxNode node) : IReadOnlyList<T> where T : struct, ISyntaxView<T>
{
    public SyntaxNode Node { get; } = node;
    public int Count => Node.ChildCount;
    public T this[int index] => T.Create(Node.GetChild(index));

    public Enumerator GetEnumerator() => new(Node);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator(SyntaxNode node) : IEnumerator<T>
    {
        private SyntaxNode.ChildList.Enumerator _children = node.Children.GetEnumerator();

        public T Current => T.Create(_children.Current);
        object IEnumerator.Current => Current;
        public bool MoveNext() => _children.MoveNext();
        public void Reset() => throw new NotSupportedException();
        public void Dispose() { }
    }
}

/// <summary>The items of a <see cref="SyntaxKind.SeparatedList"/> node; the separators are available through <see cref="GetSeparator"/>.</summary>
public readonly struct SeparatedSyntaxList<T>(SyntaxNode node) : IReadOnlyList<T> where T : struct, ISyntaxView<T>
{
    public SyntaxNode Node { get; } = node;
    public int Count => (Node.ChildCount + 1) / 2;
    public T this[int index] => T.Create(Node.GetChild(2 * index));

    /// <summary>The separator after item <paramref name="index"/>.</summary>
    public SyntaxToken GetSeparator(int index) => new(Node.GetChild(2 * index + 1));

    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
