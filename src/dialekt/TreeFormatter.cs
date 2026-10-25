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

    /// <summary>Also print the placeholders for optional parts that are not there.</summary>
    public bool ShowAbsent { get; init; }

    public void PrintTree(SyntaxTree tree)
    {
        PrintNode(tree.Root, null, "", true);
    }

    private void PrintNode(SyntaxNode node, string? slotName, string indent, bool isLast)
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
        var slot = slotName is null ? "" : $" {slotName}:";
        Console.WriteLine($"{slot} {node.Kind}{text} {_fileName}({line},{column})");

        var slots = SyntaxSchema.GetSlots(node.Kind);
        var visible = new List<(SyntaxNode Child, string? Slot)>();
        var index = 0;
        foreach (var child in node.Children)
        {
            var hidden = (!ShowTrivia && child.IsTrivia) || (!ShowAbsent && child.Kind == SyntaxKind.Absent);
            if (!hidden)
            {
                visible.Add((child, index < slots.Length ? slots[index].Name : null));
            }
            index++;
        }
        for (var i = 0; i < visible.Count; i++)
        {
            PrintNode(visible[i].Child, visible[i].Slot, indent, i == visible.Count - 1);
        }
    }
}
