namespace Dialekt.Syntax;

using Dialekt.Green;

/// <summary>
/// A parsed file: the green tree (structure, relative widths) together with the source text it was parsed from.
/// The red tree (<see cref="Root"/> and its descendants) is created lazily on top of it and adds parents and absolute positions.
/// </summary>
public sealed class SyntaxTree
{
    private readonly Lazy<LineMap> _lineMap;

    public SyntaxTree(GreenTree green, string source)
    {
        if (green.FullWidth != source.Length)
        {
            throw new ArgumentException($"The green tree covers {green.FullWidth} characters but the source has {source.Length}.", nameof(source));
        }
        Green = green;
        Source = source;
        _lineMap = new Lazy<LineMap>(() => new LineMap(source));
    }

    public GreenTree Green { get; }
    public string Source { get; }
    public LineMap LineMap => _lineMap.Value;
    public SyntaxNode Root => new(this, Green.RootHandle, 0);

    /// <summary>The root as a typed view.</summary>
    public GrammarSyntax Grammar => GrammarSyntax.Create(Root);

    /// <summary>
    /// The parent of <paramref name="node"/>, or null for the root. Green nodes do not know their parent, so this descends
    /// from the root by position: at each level the node itself is looked for among the children, then the child covering
    /// its position is entered. A zero-width node (e.g. an absent optional part) cannot be told apart from an equal sibling
    /// at the same position; for those the outermost match is returned.
    /// </summary>
    public SyntaxNode? GetParent(SyntaxNode node)
    {
        var current = Root;
        if (node == current) return null;

        while (true)
        {
            SyntaxNode? next = null;
            foreach (var child in current.Children)
            {
                if (child == node) return current;
                if (next is null && child.Position <= node.Position && node.Position < child.End)
                {
                    next = child;
                }
            }
            current = next ?? throw new ArgumentException("The node does not belong to this tree.", nameof(node));
        }
    }

    /// <summary>The parent, grandparent, ... of <paramref name="node"/> up to the root.</summary>
    public IEnumerable<SyntaxNode> Ancestors(SyntaxNode node)
    {
        for (var parent = GetParent(node); parent is { } p; parent = GetParent(p))
        {
            yield return p;
        }
    }

    public GreenArena Arena => Green.Arena;

    public int CountNodes() => Green.CountNodes();

    /// <summary>The same tree in a fresh arena that holds only the nodes reachable from the root (see <see cref="GreenTree.Compact"/>).</summary>
    public SyntaxTree Compact() => new(Green.Compact(), Source);
}
