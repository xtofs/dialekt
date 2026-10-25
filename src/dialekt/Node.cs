namespace Dialekt;

using System.Text;
using Dialekt.Scanning;

/// <summary>
/// Generic syntax tree node: what it is (<see cref="Kind"/>), an optional value (<see cref="Name"/>, e.g. a rule name
/// or the raw text of a literal), its <see cref="Children"/> and where it came from in the source.
/// </summary>
public sealed class Node(string kind, SourceSpan span, string? name = null)
{
    public string Kind { get; } = kind;
    public string? Name { get; set; } = name;
    public SourceSpan Span { get; set; } = span;
    public List<Node> Children { get; } = [];

    public Node Add(Node child)
    {
        Children.Add(child);
        return this;
    }

    public IEnumerable<Node> Descendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var d in child.Descendants())
            {
                yield return d;
            }
        }
    }

    public override string ToString() => Name is null ? $"{Kind}" : $"{Kind} {Name}";

    // public string Dump()
    // {
    //     var sb = new StringBuilder();
    //     Dump(sb, 0);
    //     return sb.ToString();
    // }

    // private void Dump(StringBuilder sb, int depth)
    // {
    //     sb.Append(' ', depth * 2).Append(this).Append("  [").Append(Span).AppendLine("]");
    //     foreach (var child in Children)
    //     {
    //         child.Dump(sb, depth + 1);
    //     }
    // }

    public void Traverse(Action<Node> action)
    {
        // action(this);
        // foreach (var child in Children)
        // {
        //     child.Traverse(action);
        // }
        var queue = new Queue<Node>();
        queue.Enqueue(this);
        while (queue.TryDequeue(out var current))
        {
            action(current);
            foreach (var child in current.Children)
            {
                queue.Enqueue(child);
            }
        }
    }
}
