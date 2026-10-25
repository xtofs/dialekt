namespace Dialekt;

using Dialekt.Green;

/// <summary>
/// Prints a green tree. The green nodes carry no positions, so the formatter derives each node's absolute offset
/// while walking down and maps it to line/column with a table of line starts.
/// </summary>
public class TreeFormatter
{
    // Constants for drawing lines and spaces
    private const string _cross = " ├─";
    private const string _corner = " └─";
    private const string _vertical = " │ ";
    private const string _space = "   ";

    private readonly string _fileName;
    private readonly string _source;
    private readonly int[] _lineStarts;

    public TreeFormatter(string fileName, string source)
    {
        _fileName = fileName;
        _source = source;
        _lineStarts = [0, .. source.Select((c, i) => (c, i)).Where(x => x.c == '\n').Select(x => x.i + 1)];
    }

    public bool ShowTrivia { get; init; }

    public void PrintTree(GreenTree tree)
    {
        PrintNode(tree.Arena, tree.RootHandle, 0, "", true);
    }

    private void PrintNode(GreenArena arena, GreenNodeHandle handle, int start, string indent, bool isLast)
    {
        var node = arena.GetReader(handle);

        Console.Write(indent);
        if (isLast)
        {
            Console.Write(_corner);
            indent += _space;
        }
        else
        {
            Console.Write(_cross);
            indent += _vertical;
        }

        var textStart = start + FirstTokenLeadingWidth(node);
        var (line, column) = LineAndColumn(textStart);
        var text = "";
        if (node.IsToken && node.Kind != SyntaxKind.EndOfFileToken)
        {
            text = " " + _source.Substring(textStart, node.TextWidth).Replace("\n", "\\n").Replace("\r", "\\r");
        }
        Console.WriteLine($" {node.Kind}{text} {_fileName}({line},{column})");

        var childStart = start;
        var visible = new List<(GreenNodeHandle Handle, int Start)>();
        for (var i = 0; i < node.ChildCount; i++)
        {
            var child = node.GetChild(i);
            if (ShowTrivia || !child.IsTrivia)
            {
                visible.Add((child.Handle, childStart));
            }
            childStart += child.FullWidth;
        }
        for (var i = 0; i < visible.Count; i++)
        {
            PrintNode(arena, visible[i].Handle, visible[i].Start, indent, i == visible.Count - 1);
        }
    }

    /// <summary>Width of the whitespace/comments before the first token below <paramref name="node"/>.</summary>
    private static int FirstTokenLeadingWidth(GreenNodeReader node)
    {
        while (!node.IsToken)
        {
            if (node.ChildCount == 0) return 0;
            node = node.GetChild(0);
        }
        return node.LeadingTriviaWidth;
    }

    private (int Line, int Column) LineAndColumn(int offset)
    {
        var index = Array.BinarySearch(_lineStarts, offset);
        var line = index >= 0 ? index : ~index - 1;
        return (line + 1, offset - _lineStarts[line] + 1);
    }
}
