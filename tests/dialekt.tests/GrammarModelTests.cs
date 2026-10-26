using Dialekt.Grammar;
using Xunit;

public class GrammarModelTests
{
    private static GrammarBuildResult LoadSample(string name) => GrammarLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name + ".langium")));

    [Fact]
    public void Hello_world_model()
    {
        var result = LoadSample("hello-world");
        var model = result.Model;

        Assert.Empty(result.Diagnostics);
        Assert.Equal("HelloWorld", model.Name);
        Assert.Equal("Model", model.EntryRule!.Name);
        Assert.Equal(["WS", "ID", "Model", "Person", "Greeting"], model.Rules.Select(r => r.Name));
        Assert.Equal(["WS"], model.HiddenTerminals.Select(t => t.Name));
        Assert.Equal(["person", "Hello", "!"], model.Keywords);

        var repetition = Assert.IsType<RepetitionElement>(model.EntryRule.Body);
        Assert.Equal(Cardinality.ZeroOrMore, repetition.Cardinality);
        var alternatives = Assert.IsType<AlternativesElement>(repetition.Inner);
        var persons = Assert.IsType<AssignmentElement>(alternatives.Branches[0]);
        Assert.Equal(("persons", AssignmentOperator.Append), (persons.Feature, persons.Operator));
        Assert.Equal(new RuleCallElement("Person", RuleKind.ParserRule, default), persons.Terminal);

        var greeting = (ParserRule)model.FindRule("Greeting")!;
        var group = Assert.IsType<GroupElement>(greeting.Body);
        Assert.Equal(new KeywordElement("Hello"), group.Items[0]);
        var reference = Assert.IsType<AssignmentElement>(group.Items[1]);
        Assert.Equal(new CrossReferenceElement("Person", RuleKind.ParserRule, false, null), reference.Terminal);

        var id = (TerminalRule)model.FindRule("ID")!;
        Assert.Equal(new RegexTerminal("[_a-zA-Z][\\w]*", ""), id.Definition);
    }

    [Fact]
    public void The_model_can_be_written_back_as_grammar_text()
    {
        var model = LoadSample("hello-world").Model;

        Assert.Equal("entry Model: (persons+=Person | greetings+=Greeting)*;", GrammarFormatter.Format(model.FindRule("Model")!));
        Assert.Equal("Greeting: 'Hello' person=[Person] '!';", GrammarFormatter.Format(model.FindRule("Greeting")!));
        Assert.Equal("hidden terminal WS: /\\s+/;", GrammarFormatter.Format(model.FindRule("WS")!));
    }

    [Theory]
    [InlineData("statemachine")]
    [InlineData("domain-model")]
    public void Grammars_without_unsupported_constructs_have_no_diagnostics(string name)
    {
        Assert.Empty(LoadSample(name).Diagnostics);
    }

    [Fact]
    public void Domain_model_data_type_rule_and_multi_cross_reference()
    {
        var model = LoadSample("domain-model").Model;

        var qualifiedName = (ParserRule)model.FindRule("QualifiedName")!;
        Assert.Equal("string", qualifiedName.ReturnType);

        var entity = (ParserRule)model.FindRule("Entity")!;
        var superType = ModelWalker.DescendantsAndSelf(entity.Body).OfType<AssignmentElement>().Single(a => a.Feature == "superType");
        var reference = Assert.IsType<CrossReferenceElement>(superType.Terminal);
        Assert.True(reference.IsMulti);
        Assert.Equal("Entity", reference.TypeName);
        Assert.Equal(new RuleCallElement("QualifiedName", RuleKind.ParserRule, default), reference.Terminal);
    }

    [Fact]
    public void Unsupported_constructs_are_warnings_and_still_modelled()
    {
        var result = LoadSample("arithmetics");

        Assert.False(result.HasErrors);
        Assert.Equal(3, result.Diagnostics.Count(d => d.Code == GrammarDiagnosticCodes.Unsupported && d.Severity == GrammarSeverity.Warning));
        Assert.IsType<InfixRule>(result.Model.FindRule("BinaryExpression"));
        var primary = (ParserRule)result.Model.FindRule("PrimaryExpression")!;
        Assert.Equal("Expression", primary.InferredType);
        Assert.Contains(ModelWalker.DescendantsAndSelf(primary.Body), e => e is ActionElement { InferredType: "NumberLiteral" });
        Assert.Contains(result.Model.Types, t => t is TypeAlias { Name: "AbstractDefinition" });
    }

    [Fact]
    public void The_two_langium_grammars_load_with_warnings_only()
    {
        var grammar = LoadSample("langium-grammar");
        var types = LoadSample("langium-types");

        Assert.False(grammar.HasErrors);
        Assert.False(types.HasErrors);
        Assert.Equal("LangiumGrammar", grammar.Model.Name);
        Assert.Equal("Grammar", grammar.Model.EntryRule!.Name);
        Assert.Contains(grammar.Diagnostics, d => d.Message.Contains("Actions"));
        Assert.Contains(grammar.Diagnostics, d => d.Message.Contains("parameters"));
        Assert.Contains(grammar.Diagnostics, d => d.Message.Contains("Guarded"));
        // AbstractType is declared in the imported types file, which is not followed: a warning, not an error
        Assert.Contains(grammar.Diagnostics, d => d.Code == GrammarDiagnosticCodes.UnresolvedType && d.Severity == GrammarSeverity.Warning);
        // the types file only declares interfaces and type aliases
        Assert.Empty(types.Model.Rules);
        Assert.Equal(GrammarDiagnosticCodes.NoEntryRule, Assert.Single(types.Diagnostics).Code);
        Assert.Contains(types.Model.Types, t => t is InterfaceDeclaration { Name: "ParserRule" });
    }

    [Fact]
    public void Problems_are_reported_with_their_position()
    {
        var result = GrammarLoader.Load("""
            grammar G
            entry A: b=B 'x' c=[Nope];
            A: 'again';
            terminal ID: /a/;
            """.Replace("\r\n", "\n"));

        Assert.Equal(
            [
                (2, 12, GrammarDiagnosticCodes.UnresolvedRule),
                (2, 21, GrammarDiagnosticCodes.UnresolvedType),
                (3, 1, GrammarDiagnosticCodes.DuplicateRule),
            ],
            result.Diagnostics.Select(d => (d.Line, d.Column, d.Code)));
        Assert.All(result.Diagnostics, d => Assert.Equal(GrammarSeverity.Error, d.Severity));
        // the first declaration of a name wins
        Assert.Equal(2, result.Model.Rules.Count);
    }

    [Fact]
    public void Several_entry_rules_and_missing_entry_rule()
    {
        Assert.Equal(GrammarDiagnosticCodes.MultipleEntryRules, Assert.Single(GrammarLoader.Load("entry A: 'a'; entry B: 'b';").Diagnostics).Code);
        Assert.Null(GrammarLoader.Load("entry A: 'a'; entry B: 'b';").Model.EntryRule);
        var none = GrammarLoader.Load("A: 'a';");
        Assert.Equal(GrammarSeverity.Warning, Assert.Single(none.Diagnostics).Severity);
    }

    [Fact]
    public void Syntax_errors_are_diagnostics_and_the_broken_declaration_is_left_out()
    {
        var result = GrammarLoader.Load("entry A: 'a'; B: ; C: 'c';\n");

        Assert.Contains(result.Diagnostics, d => d.Code == GrammarDiagnosticCodes.SyntaxError && d.Message.StartsWith("Expected"));
        Assert.Equal(["A", "C"], result.Model.Rules.Select(r => r.Name));
    }

    [Fact]
    public void Models_are_equal_when_the_grammars_say_the_same_thing()
    {
        var original = GrammarLoader.Load("grammar G\nentry A: x=B+;\nB: 'b' name=ID;\nterminal ID: /\\w+/;\n").Model;
        var reformatted = GrammarLoader.Load("// a comment\ngrammar   G\n\nentry A:\n    x=B+ ;\n\nB: 'b'\n    name = ID ;\n   terminal ID: /\\w+/;").Model;
        var changed = GrammarLoader.Load("grammar G\nentry A: x=B+;\nB: 'c' name=ID;\nterminal ID: /\\w+/;\n").Model;

        Assert.Equal(original, reformatted);
        Assert.Equal(original.GetHashCode(), reformatted.GetHashCode());
        Assert.NotEqual(original, changed);
    }

    [Fact]
    public void Keywords_have_their_escapes_resolved()
    {
        var model = GrammarLoader.Load("entry A: 'it\\'s' \"tab\\t\" '\\u0041';").Model;

        Assert.Equal(["it's", "tab\t", "A"], model.Keywords);
    }
}
