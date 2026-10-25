namespace Dialekt;


public class TreeFormatter(string source)
{
    // Constants for drawing lines and spaces
    private const string _cross = " ├─";
    private const string _corner = " └─";
    private const string _vertical = " │ ";
    private const string _space = "   ";
    private readonly string _source = source;

    public void PrintTree(Node root)
    {
        PrintNode(root, "", true);
    }

    private void PrintNode(Node node, string indent, bool isLast)
    {
        // Print the provided pipes/spaces indent
        Console.Write(indent);

        // Depending if this node is a last child, print the
        // corner or cross, and calculate the indent that will
        // be passed to its children
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

        var nameIfAny = string.IsNullOrEmpty(node.Name) ? "" : $" {node.Name}";
        Console.WriteLine($" {node.Kind}{nameIfAny} {_source}({node.Span.Start.Line},{node.Span.Start.Column})");

        // Loop through the children recursively, passing in the
        // indent, and the isLast parameter
        var numberOfChildren = node.Children.Count;
        for (var i = 0; i < numberOfChildren; i++)
        {
            var child = node.Children[i];
            isLast = (i == (numberOfChildren - 1));
            PrintNode(child, indent, isLast);
        }
    }
}