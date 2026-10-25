namespace Dialekt.Syntax;

using Dialekt.Green;

/// <summary>
/// A parsed file: the green tree (structure, relative widths) together with the source text it was parsed from.
/// The red tree (<see cref="Root"/> and its descendants) is created lazily on top of it and adds parents and absolute positions.
/// </summary>
public sealed class SyntaxTree
{
    private readonly Lazy<LineMap> _lineMap;
    private readonly Lazy<SyntaxNode> _root;

    public SyntaxTree(GreenTree green, string source)
    {
        if (green.FullWidth != source.Length)
        {
            throw new ArgumentException($"The green tree covers {green.FullWidth} characters but the source has {source.Length}.", nameof(source));
        }
        Green = green;
        Source = source;
        _lineMap = new Lazy<LineMap>(() => new LineMap(source));
        _root = new Lazy<SyntaxNode>(() => new SyntaxNode(this, null, green.RootHandle, 0, 0));
    }

    public GreenTree Green { get; }
    public string Source { get; }
    public LineMap LineMap => _lineMap.Value;
    public SyntaxNode Root => _root.Value;

    public GreenArena Arena => Green.Arena;

    public int CountNodes() => Green.CountNodes();

    /// <summary>The same tree in a fresh arena that holds only the nodes reachable from the root (see <see cref="GreenTree.Compact"/>).</summary>
    public SyntaxTree Compact() => new(Green.Compact(), Source);
}
