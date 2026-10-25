using Dialekt.Green;
using Dialekt.Parsing;
using Dialekt.Syntax;
using Xunit;

public class TypedSyntaxTests
{
    private static ParserRuleSyntax SingleRule(string text)
    {
        var grammar = Parser.Parse(text).Tree.Grammar;
        return Assert.Single(grammar.Members).AsParserRule()!.Value;
    }

    [Fact]
    public void Parser_rule_slots_have_fixed_meaning()
    {
        var rule = SingleRule("entry Grammar returns Grammar: a=ID b+=Foo*;\n");

        Assert.Equal("entry", rule.Modifier!.Value.Keyword.Text);
        Assert.Equal("Grammar", rule.Name.Text);
        Assert.Null(rule.Parameters);
        Assert.Equal("Grammar", rule.ReturnType!.Value.Name.Text);
        Assert.Null(rule.InferredType);
        Assert.False(rule.Colon.IsMissing);

        var group = rule.Definition.AsGroup()!.Value;
        Assert.Equal(2, group.Elements.Count);
        var assignment = group.Elements[0].AsAssignment()!.Value;
        Assert.Equal("a", assignment.Feature.Name.Text);
        Assert.Equal("=", assignment.Operator.Token.Text);
        Assert.Equal("ID", assignment.Terminal.AsRuleCall()!.Value.Name.Text);

        var cardinality = group.Elements[1].AsCardinality()!.Value;
        Assert.Equal("*", cardinality.Operator.Text);
        Assert.Equal("b", cardinality.Element.AsAssignment()!.Value.Feature.Name.Text);
    }

    [Fact]
    public void Removing_optional_parts_does_not_move_the_other_slots()
    {
        var plain = SingleRule("A: b=ID;\n");

        Assert.Null(plain.Modifier);
        Assert.Null(plain.Parameters);
        Assert.Null(plain.ReturnType);
        Assert.Equal("A", plain.Name.Text);
        Assert.NotNull(plain.Definition.AsAssignment());
    }

    [Fact]
    public void Parameters_and_arguments_are_separated_lists()
    {
        var rule = SingleRule("R<a, b> returns T: X<a, b=true> | Y;\n");

        var parameters = rule.Parameters!.Value.Parameters;
        Assert.Equal(["a", "b"], parameters.Select(p => p.Name.Text));
        Assert.Equal(",", parameters.GetSeparator(0).Text);

        var alternatives = rule.Definition.AsAlternatives()!.Value;
        Assert.Equal(2, alternatives.Items.Count);
        var arguments = alternatives.Items[0].AsRuleCall()!.Value.Arguments!.Value.Arguments;
        Assert.Equal(2, arguments.Count);
        Assert.Null(arguments[0].Parameter);
        Assert.Equal("b", arguments[1].Parameter!.Value.Text);
        Assert.Equal(SyntaxKind.BooleanLiteral, arguments[1].Value.Kind);
        Assert.Null(alternatives.Items[1].AsRuleCall()!.Value.Arguments);
    }

    [Fact]
    public void Interfaces_expose_super_types_and_attributes()
    {
        var tree = Parser.Parse("interface A extends B, C {\n  x?: string[] | @D+;\n  y: 'k' = 3;\n}\n").Tree;
        var declaration = Assert.Single(tree.Grammar.Members).AsInterface()!.Value;

        Assert.Equal("A", declaration.Name.Text);
        Assert.Equal(["B", "C"], declaration.SuperTypes.Select(s => s.Name.Text));
        Assert.Equal(2, declaration.Attributes.Count);

        var x = declaration.Attributes[0].AsAttribute()!.Value;
        Assert.Equal("x", x.Name.Text);
        Assert.NotNull(x.Optional);
        var union = x.Type.AsUnionType()!.Value.Types;
        Assert.Equal(2, union.Count);
        Assert.NotNull(union[0].AsArrayType());
        var reference = union[1].AsReferenceType()!.Value;
        Assert.NotNull(reference.Multi);

        var y = declaration.Attributes[1].AsAttribute()!.Value;
        Assert.Null(y.Optional);
        Assert.Equal("3", y.DefaultValue!.Value.AsNumberLiteral()!.Value.Value.Text);
    }

    [Fact]
    public void Terminal_rules_distinguish_the_two_modifiers()
    {
        var hidden = Assert.Single(Parser.Parse("hidden terminal WS: /\\s+/;\n").Tree.Grammar.Members).AsTerminalRule()!.Value;
        var fragment = Assert.Single(Parser.Parse("terminal fragment F: 'a'..'z'+;\n").Tree.Grammar.Members).AsTerminalRule()!.Value;

        Assert.Equal("hidden", hidden.HiddenModifier!.Value.Keyword.Text);
        Assert.Null(hidden.FragmentModifier);
        Assert.Null(fragment.HiddenModifier);
        Assert.Equal("fragment", fragment.FragmentModifier!.Value.Keyword.Text);
        Assert.Equal("F", fragment.Name.Text);
        Assert.NotNull(hidden.Definition.AsRegexToken());
        Assert.NotNull(fragment.Definition.AsTerminalCardinality());
    }

    [Fact]
    public void Syntax_errors_show_up_as_error_members()
    {
        var members = Parser.Parse("A: b=; B: c=ID;\n").Tree.Grammar.Members;
        Assert.Equal([SyntaxKind.Error, SyntaxKind.ParserRule], members.Select(m => m.Kind));
        Assert.Null(members[0].AsParserRule());
    }

    [Fact]
    public void Walking_the_whole_langium_grammar_through_typed_views_does_not_allocate()
    {
        var grammar = Parser.Parse(TestFiles.GrammarText).Tree.Grammar;

        static int Walk(GrammarSyntax grammar)
        {
            var count = 0;
            foreach (var member in grammar.Members)
            {
                if (member.AsParserRule() is { } rule)
                {
                    count += rule.Name.Node.Kind == SyntaxKind.IdentifierToken ? 1 : 0;
                    if (rule.Definition.AsGroup() is { } group)
                    {
                        foreach (var element in group.Elements)
                        {
                            count += element.Kind == SyntaxKind.Assignment ? 1 : 0;
                        }
                    }
                }
            }
            return count;
        }

        Walk(grammar);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = Walk(grammar);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(result > 50);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Generated_views_are_up_to_date()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tonto.slnx")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        var path = Path.Combine(dir.FullName, "src", "dialekt", "Syntax", "Generated", "SyntaxViews.g.cs");

        Assert.Equal(SyntaxGenerator.Generator.Generate(), File.ReadAllText(path));
    }
}
