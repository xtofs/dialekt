namespace Dialekt.Syntax;

using Dialekt.Green;

/// <summary>
/// A parsed file: the green tree (structure, relative widths) together with the source text it was parsed from.
/// The red tree (<see cref="Root"/> and its descendants) is created lazily on top of it and adds parents and absolute positions.
/// </summary>
public sealed class SyntaxTree
{
    private readonly Lazy<LineMap> _lineMap;

    public SyntaxTree(GreenTree green, string source, Language language)
    {
        if (green.FullWidth != source.Length)
        {
            throw new ArgumentException($"The green tree covers {green.FullWidth} characters but the source has {source.Length}.", nameof(source));
        }
        Green = green;
        Source = source;
        Language = language;
        _lineMap = new Lazy<LineMap>(() => new LineMap(source));
    }

    public GreenTree Green { get; }
    /// <summary>The language the tree was parsed as: names of kinds, slot layouts.</summary>
    public Language Language { get; }
    public string Source { get; }
    public LineMap LineMap => _lineMap.Value;
    public SyntaxNode Root => new(this, Green.RootHandle, 0);

    /// <summary>
    /// The parent of <paramref name="node"/>, or null for the root. Green nodes do not know their parent, so a node that was
    /// not reached from its parent (see <see cref="SyntaxNode.Parent"/>) is looked up by descending from the root; see
    /// <see cref="Ancestors"/>.
    /// </summary>
    public SyntaxNode? GetParent(SyntaxNode node)
    {
        var path = PathToParent(node);
        return path.Count == 0 ? null : path[^1];
    }

    /// <summary>The parent, grandparent, ... of <paramref name="node"/> up to the root.</summary>
    public IEnumerable<SyntaxNode> Ancestors(SyntaxNode node)
    {
        var path = PathToParent(node);
        for (var i = path.Count - 1; i >= 0; i--)
        {
            yield return path[i];
        }
    }

    /// <summary>
    /// The nodes from the root down to the parent of <paramref name="node"/>, found by one descent: at each level the node
    /// itself is looked for among the children, then the child covering its position is entered. A zero-width node whose parent
    /// is not known cannot be told apart from an equal sibling at the same position; the outermost match is used then.
    /// </summary>
    private List<SyntaxNode> PathToParent(SyntaxNode node)
    {
        var path = new List<SyntaxNode>();
        var current = Root;
        if (node == current) return path;

        while (true)
        {
            path.Add(current);
            SyntaxNode? next = null;
            foreach (var child in current.Children)
            {
                if (child == node) return path;
                if (next is null && child.Position <= node.Position && node.Position < child.End)
                {
                    next = child;
                }
            }
            current = next ?? throw new ArgumentException("The node does not belong to this tree.", nameof(node));
        }
    }

    public GreenArena Arena => Green.Arena;

    public int CountNodes() => Green.CountNodes();

    /// <summary>The same tree in a fresh arena that holds only the nodes reachable from the root (see <see cref="GreenTree.Compact"/>).</summary>
    public SyntaxTree Compact() => new(Green.Compact(), Source, Language);
}
