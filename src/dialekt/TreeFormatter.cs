namespace Dialekt;

using Dialekt.Green;
using Dialekt.Syntax;

/// <summary>Prints a syntax tree; positions come from the red tree.</summary>
public class TreeFormatter(string fileName)
{
    // Constants for drawing lines and spaces
    private const string _cross = " ├─";
    private const string _corner = " └─";
    private const string _vertical = " │ ";
    private const string _space = "   ";

    private readonly string _fileName = fileName;

    public bool ShowTrivia { get; init; }

    public void PrintTree(SyntaxTree tree)
    {
        PrintNode(tree.Root, "", true);
    }

    private void PrintNode(SyntaxNode node, string indent, bool isLast)
    {
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

        var (line, column) = node.StartLineAndColumn;
        var text = "";
        if (node.IsToken && node.Kind != SyntaxKind.EndOfFileToken)
        {
            text = " " + node.Text.Replace("\n", "\\n").Replace("\r", "\\r");
        }
        Console.WriteLine($" {node.Kind}{text} {_fileName}({line},{column})");

        var visible = node.Children.Where(child => ShowTrivia || !child.IsTrivia).ToList();
        for (var i = 0; i < visible.Count; i++)
        {
            PrintNode(visible[i], indent, i == visible.Count - 1);
        }
    }
}
