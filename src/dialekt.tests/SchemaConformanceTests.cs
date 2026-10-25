using Dialekt.Green;
using Dialekt.Parsing;
using Dialekt.Syntax;
using Xunit;

public class SchemaConformanceTests
{
    private static readonly SyntaxKind[] WithoutSlots = [SyntaxKind.Error, SyntaxKind.List, SyntaxKind.SeparatedList, SyntaxKind.Absent, SyntaxKind.Missing];

    /// <summary>Returns a description of every place where the tree does not follow the schema.</summary>
    private static List<string> Violations(SyntaxTree tree)
    {
        var problems = new List<string>();
        foreach (var node in tree.Root.DescendantsAndSelf())
        {
            var kind = node.Kind;
            if (node.IsToken || node.IsTrivia)
            {
                continue;
            }
            var slots = SyntaxSchema.GetSlots(kind);
            if (slots.IsEmpty)
            {
                if (!WithoutSlots.Contains(kind)) problems.Add($"{node} has no schema");
                continue;
            }
            if (node.ChildCount != slots.Length)
            {
                problems.Add($"{node}: {node.ChildCount} children for {slots.Length} slots");
                continue;
            }
            for (var i = 0; i < slots.Length; i++)
            {
                Check(node, slots[i], node.GetChild(i), problems);
            }
        }
        return problems;
    }

    private static void Check(SyntaxNode parent, SlotSpec slot, SyntaxNode child, List<string> problems)
    {
        var where = $"{parent.Kind}.{slot.Name}";
        var kind = child.Kind;
        switch (slot.Shape)
        {
            case SlotShape.Required:
                if (!slot.Accepts(kind) && !child.Flags.HasFlag(GreenNodeFlags.IsMissing)) problems.Add($"{where} holds {child}");
                break;
            case SlotShape.Optional:
                if (!slot.Accepts(kind) && kind != SyntaxKind.Absent) problems.Add($"{where} holds {child}");
                break;
            case SlotShape.List:
                if (kind != SyntaxKind.List) problems.Add($"{where} is not a List: {child}");
                else if (child.Children.Any(item => !slot.Accepts(item.Kind))) problems.Add($"{where} has an item that is not accepted");
                break;
            case SlotShape.SeparatedList:
                if (kind != SyntaxKind.SeparatedList) problems.Add($"{where} is not a SeparatedList: {child}");
                else
                {
                    var i = 0;
                    foreach (var item in child.Children)
                    {
                        var expected = i % 2 == 0 ? slot.Accepts(item.Kind) : item.Kind == slot.Separator;
                        if (!expected) problems.Add($"{where} item {i} is {item.Kind}");
                        i++;
                    }
                    if (i > 0 && i % 2 == 0) problems.Add($"{where} ends with a separator");
                }
                break;
        }
    }

    [Theory]
    [InlineData("grammar")]
    [InlineData("types")]
    public void Both_langium_files_follow_the_schema_and_have_nothing_missing(string name)
    {
        var tree = Parser.Parse(name == "grammar" ? TestFiles.GrammarText : TestFiles.TypesText).Tree;

        Assert.Empty(Violations(tree));
        Assert.DoesNotContain(tree.Root.DescendantsAndSelf(), n => n.Flags.HasFlag(GreenNodeFlags.IsMissing));
        Assert.Contains(tree.Root.DescendantsAndSelf(), n => n.Kind == SyntaxKind.Absent);
    }

    [Fact]
    public void Edited_and_broken_texts_still_follow_the_schema()
    {
        var random = new Random(2024);
        string[] snippets = [";", "}", "{", "/*", "'", "|", "&", "=", "+=", "?", "[", "]", "(", ")", "<", ">", "@", "interface I {", "type T = A |", "R: a=", "#", "\n"];
        var text = TestFiles.GrammarText;
        var result = Parser.Parse(text);

        for (var i = 0; i < 300; i++)
        {
            var start = random.Next(text.Length + 1);
            var length = random.Next(2) == 0 ? 0 : Math.Min(random.Next(1, 30), text.Length - start);
            var insert = snippets[random.Next(snippets.Length)];
            var newText = text.Remove(start, length).Insert(start, insert);
            result = Parser.ReParse(newText, result.Tree, new TextChange(start, length, insert.Length));
            text = newText;

            var problems = Violations(result.Tree);
            Assert.True(problems.Count == 0, $"edit #{i}: {string.Join("; ", problems.Take(3))}");
        }
    }

    [Fact]
    public void Every_kind_with_a_schema_has_unique_slot_names()
    {
        foreach (var (kind, slots) in SyntaxSchema.All())
        {
            Assert.Equal(slots.Length, slots.Select(s => s.Name).Distinct().Count());
            Assert.All(slots, s => Assert.True(s.Shape != SlotShape.SeparatedList || s.Separator != SyntaxKind.None, $"{kind}.{s.Name}"));
        }
    }

    [Fact]
    public void Optional_parts_are_absent_and_their_neighbours_keep_their_slots()
    {
        var withModifier = Parser.Parse("entry A: b=ID;\n").Tree;
        var without = Parser.Parse("A: b=ID;\n").Tree;

        var ruleWith = TestFiles.Members(withModifier).Single();
        var ruleWithout = TestFiles.Members(without).Single();

        Assert.Equal(SyntaxKind.Modifier, ruleWith.GetChild(0).Kind);
        Assert.Equal(SyntaxKind.Absent, ruleWithout.GetChild(0).Kind);
        Assert.Equal("A", ruleWith.GetChild(1).Text);
        Assert.Equal("A", ruleWithout.GetChild(1).Text);
        Assert.Equal(SyntaxKind.Absent, ruleWithout.GetChild(2).Kind);
    }

    [Fact]
    public void Structure_nodes_do_not_break_positions()
    {
        var tree = Parser.Parse("interface I { a: string; }\n").Tree;
        var token = tree.Root.FindToken(tree.Source.IndexOf("a:", StringComparison.Ordinal));
        Assert.Equal("a", token.Text);
        Assert.Equal(SyntaxKind.EndOfFileToken, tree.Root.FindToken(tree.Source.Length).Kind);
    }
}
