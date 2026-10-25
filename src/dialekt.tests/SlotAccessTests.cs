using Dialekt.Green;
using Dialekt.Parsing;
using Dialekt.Syntax;
using Xunit;

public class SlotAccessTests
{
    private static SyntaxNode SingleMember(string text, SyntaxKind kind)
    {
        var member = Assert.Single(TestFiles.Members(Parser.Parse(text).Tree));
        Assert.Equal(kind, member.Kind);
        return member;
    }

    [Fact]
    public void Parser_rule_slots_have_fixed_meaning()
    {
        var rule = SingleMember("entry Grammar returns Grammar: a=ID b+=Foo*;\n", SyntaxKind.ParserRule);

        Assert.Equal("entry", rule.GetSlot("Modifier").GetSlot("Keyword").Text);
        Assert.Equal("Grammar", rule.GetSlot("Name").Text);
        Assert.True(rule.GetSlot("Parameters").IsAbsent);
        Assert.Equal("Grammar", rule.GetSlot("ReturnType").GetSlot("Name").Text);
        Assert.True(rule.GetSlot("InferredType").IsAbsent);
        Assert.False(rule.GetSlot("Colon").IsMissing);

        var elements = rule.GetSlot("Definition").GetSlot("Elements").Children.ToList();
        Assert.Equal([SyntaxKind.Assignment, SyntaxKind.Cardinality], elements.Select(e => e.Kind));
        var assignment = elements[0];
        Assert.Equal("a", assignment.GetSlot("Feature").GetSlot("Name").Text);
        Assert.Equal("=", assignment.GetSlot("Operator").GetSlot("Token").Text);
        Assert.Equal("ID", assignment.GetSlot("Terminal").GetSlot("Name").Text);

        var cardinality = elements[1];
        Assert.Equal("*", cardinality.GetSlot("Operator").Text);
        Assert.Equal("b", cardinality.GetSlot("Element").GetSlot("Feature").GetSlot("Name").Text);
    }

    [Fact]
    public void Removing_optional_parts_does_not_move_the_other_slots()
    {
        var rule = SingleMember("A: b=ID;\n", SyntaxKind.ParserRule);

        Assert.True(rule.GetSlot("Modifier").IsAbsent);
        Assert.True(rule.GetSlot("Parameters").IsAbsent);
        Assert.True(rule.GetSlot("ReturnType").IsAbsent);
        Assert.Equal("A", rule.GetSlot("Name").Text);
        Assert.Equal(SyntaxKind.Assignment, rule.GetSlot("Definition").Kind);
    }

    [Fact]
    public void Parameters_and_arguments_are_separated_lists()
    {
        var rule = SingleMember("R<a, b> returns T: X<a, b=true> | Y;\n", SyntaxKind.ParserRule);

        var parameters = rule.GetSlot("Parameters").GetSlot("Parameters").Children.ToList();
        Assert.Equal(["a", ",", "b"], parameters.Select(p => p.Text));

        var items = rule.GetSlot("Definition").GetSlot("Items").Children.ToList();
        Assert.Equal([SyntaxKind.RuleCall, SyntaxKind.BarToken, SyntaxKind.RuleCall], items.Select(i => i.Kind));
        var arguments = items[0].GetSlot("Arguments").GetSlot("Arguments").Children.Where(a => a.Kind == SyntaxKind.NamedArgument).ToList();
        Assert.Equal(2, arguments.Count);
        Assert.True(arguments[0].GetSlot("Parameter").IsAbsent);
        Assert.Equal("b", arguments[1].GetSlot("Parameter").Text);
        Assert.Equal(SyntaxKind.BooleanLiteral, arguments[1].GetSlot("Value").Kind);
        Assert.True(items[2].GetSlot("Arguments").IsAbsent);
    }

    [Fact]
    public void Interfaces_expose_super_types_and_attributes()
    {
        var declaration = SingleMember("interface A extends B, C {\n  x?: string[] | @D+;\n  y: 'k' = 3;\n}\n", SyntaxKind.Interface);

        Assert.Equal("A", declaration.GetSlot("Name").Text);
        Assert.Equal(["B", "C"], declaration.GetSlot("SuperTypes").Children.Where(s => s.Kind == SyntaxKind.SuperType).Select(s => s.GetSlot("Name").Text));

        var attributes = declaration.GetSlot("Attributes").Children.ToList();
        Assert.Equal(2, attributes.Count);

        var x = attributes[0];
        Assert.Equal("x", x.GetSlot("Name").Text);
        Assert.False(x.GetSlot("Optional").IsAbsent);
        var union = x.GetSlot("Type").GetSlot("Types").Children.Where(t => !t.IsToken).ToList();
        Assert.Equal([SyntaxKind.ArrayType, SyntaxKind.ReferenceType], union.Select(t => t.Kind));
        Assert.False(union[1].GetSlot("Multi").IsAbsent);

        var y = attributes[1];
        Assert.True(y.GetSlot("Optional").IsAbsent);
        Assert.Equal("3", y.GetSlot("DefaultValue").GetSlot("Value").Text);
    }

    [Fact]
    public void Terminal_rules_distinguish_the_two_modifiers()
    {
        var hidden = SingleMember("hidden terminal WS: /\\s+/;\n", SyntaxKind.TerminalRule);
        var fragment = SingleMember("terminal fragment F: 'a'..'z'+;\n", SyntaxKind.TerminalRule);

        Assert.Equal("hidden", hidden.GetSlot("HiddenModifier").Text);
        Assert.True(hidden.GetSlot("FragmentModifier").IsAbsent);
        Assert.True(fragment.GetSlot("HiddenModifier").IsAbsent);
        Assert.Equal("fragment", fragment.GetSlot("FragmentModifier").Text);
        Assert.Equal("F", fragment.GetSlot("Name").Text);
        Assert.Equal(SyntaxKind.RegexToken, hidden.GetSlot("Definition").Kind);
        Assert.Equal(SyntaxKind.TerminalCardinality, fragment.GetSlot("Definition").Kind);
    }

    [Fact]
    public void Syntax_errors_show_up_as_error_members()
    {
        var members = TestFiles.Members(Parser.Parse("A: b=; B: c=ID;\n").Tree).ToList();
        Assert.Equal([SyntaxKind.Error, SyntaxKind.ParserRule], members.Select(m => m.Kind));
        Assert.False(members[0].TryGetSlot("Name", out _));
    }

    [Fact]
    public void Asking_for_a_slot_the_kind_does_not_have_names_the_valid_ones()
    {
        var rule = SingleMember("A: b=ID;\n", SyntaxKind.ParserRule);

        var error = Assert.Throws<ArgumentException>(() => rule.GetSlot("Nmae"));
        Assert.Contains("ParserRule", error.Message);
        Assert.Contains("Name", error.Message);
        Assert.False(rule.TryGetSlot("Nmae", out _));
    }

    [Fact]
    public void Reading_slots_of_the_whole_langium_grammar_does_not_allocate()
    {
        var tree = Parser.Parse(TestFiles.GrammarText).Tree;

        static int Walk(SyntaxTree tree)
        {
            var count = 0;
            foreach (var member in tree.Root.GetSlot("Members").Children)
            {
                if (member.Kind == SyntaxKind.ParserRule)
                {
                    count += member.GetSlot("Name").Kind == SyntaxKind.IdentifierToken ? 1 : 0;
                    var definition = member.GetSlot("Definition");
                    if (definition.Kind == SyntaxKind.Group)
                    {
                        foreach (var element in definition.GetSlot("Elements").Children)
                        {
                            count += element.Kind == SyntaxKind.Assignment ? 1 : 0;
                        }
                    }
                }
            }
            return count;
        }

        Walk(tree);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = Walk(tree);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(result > 50);
        Assert.Equal(0, allocated);
    }
}
