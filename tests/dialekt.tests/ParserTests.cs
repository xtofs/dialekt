using Dialekt.Green;
using Dialekt.Parsing;
using Xunit;

public class ParserTests
{
    [Fact]
    public void The_langium_files_parse_without_diagnostics()
    {
        Assert.Empty(LangiumParser.Parse(TestFiles.GrammarText).Diagnostics);
        Assert.Empty(LangiumParser.Parse(TestFiles.TypesText).Diagnostics);
    }

    [Fact]
    public void Parsing_on_top_of_the_previous_tree_shares_all_nodes()
    {
        var first = LangiumParser.Parse(TestFiles.GrammarText);
        var second = LangiumParser.Parse(TestFiles.GrammarText, first.Tree);

        Assert.True(second.Tree.Green.RootHandle == first.Tree.Green.RootHandle);
        Assert.Equal(first.Tree.Arena.Length, second.Tree.Arena.Length);
    }

    [Theory]
    [InlineData("A returns A: b=ID c=[Foo:ID ;\n", 1, 29, "Expected ']' but found ';'")]
    [InlineData("B: x=ID @ y=STRING;\n", 1, 9, "Expected ';' but found '@'")]
    [InlineData("D: {X.y+=current} | e=;\n", 1, 23, "Expected a keyword, rule call, '(' or cross reference but found ';'")]
    [InlineData("interface I { b: ; c: number; }\n", 1, 18, "Expected a type but found ';'")]
    [InlineData("type T = A | ;\n", 1, 14, "Expected a type but found ';'")]
    [InlineData("terminal X: /abc ;\n", 1, 13, "Invalid or unterminated regular expression literal")]
    [InlineData("A: b=ID #  c=ID;\n", 1, 9, "Unexpected character '#'")]
    [InlineData("B: x=ID /* open\n", 1, 9, "Unterminated comment")]
    public void Errors_are_reported_with_line_and_column(string text, int line, int column, string message)
    {
        var diagnostic = Assert.Single(LangiumParser.Parse(text).Diagnostics);
        Assert.Equal((line, column, message), (diagnostic.Line, diagnostic.Column, diagnostic.Message));
    }

    [Fact]
    public void Parsing_continues_after_an_error()
    {
        var result = LangiumParser.Parse("A: b=; B: c=ID; C: d=; D: 'ok';\n");
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Equal(2, TestFiles.Members(result.Tree).Count(n => n.Kind == LangiumKinds.ParserRule));
        Assert.Equal(2, TestFiles.Members(result.Tree).Count(n => n.Kind == SyntaxKind.Error));
    }

    [Fact]
    public void Symbol_table_is_consistent_with_MaxSymbolLength()
    {
        string[] symbols =
        [
            ":", "|", ">", "=", "<", "&", ".", "..", ",", "=>", "+", "*", "?<=", "?<!", "?", "?!", "->",
            "(", ")", "{", "}", "[", "]", ";", "!", "@", "+=", "?=",
        ];
        var kinds = new List<SyntaxKind>();
        foreach (var symbol in symbols)
        {
            Assert.True(LangiumFacts.TryGetSymbolKind(symbol, out var kind), $"'{symbol}' is not recognized");
            kinds.Add(kind);
        }
        Assert.Equal(symbols.Length, kinds.Distinct().Count());
        Assert.Equal(LangiumFacts.MaxSymbolLength, symbols.Max(s => s.Length));

        // nothing longer than MaxSymbolLength can be a symbol: extend every symbol by every symbol character
        var alphabet = symbols.SelectMany(s => s).Distinct().ToArray();
        foreach (var symbol in symbols.Where(s => s.Length == LangiumFacts.MaxSymbolLength))
        {
            foreach (var c in alphabet)
            {
                Assert.False(LangiumFacts.TryGetSymbolKind(symbol + c, out _), $"'{symbol}{c}' must not be a symbol");
            }
        }
    }

    [Fact]
    public void Langium_kinds_have_unique_numbers_after_the_runtime_kinds()
    {
        var fields = typeof(LangiumKinds).GetFields().Where(f => f.IsLiteral).ToList();
        var numbers = fields.Select(f => (int)(SyntaxKind)f.GetRawConstantValue()!).ToList();

        Assert.Equal(numbers.Count, numbers.Distinct().Count());
        Assert.True(numbers.Min() >= (int)SyntaxKind.FirstLanguageKind);
        // no gaps, so that a name table indexed by kind stays small
        Assert.Equal((int)SyntaxKind.FirstLanguageKind + numbers.Count - 1, numbers.Max());
        Assert.Equal("ParserRule", LangiumLanguage.Instance.GetKindName(LangiumKinds.ParserRule));
        Assert.Equal("Absent", LangiumLanguage.Instance.GetKindName(SyntaxKind.Absent));
    }

    [Fact]
    public void Every_sample_grammar_parses_without_diagnostics()
    {
        var samples = Directory.GetFiles(AppContext.BaseDirectory, "*.langium");

        Assert.True(samples.Length >= 5, "the samples are copied next to the tests");
        foreach (var sample in samples)
        {
            var result = LangiumParser.Parse(File.ReadAllText(sample));
            Assert.True(result.Diagnostics.Count == 0, $"{Path.GetFileName(sample)}: {string.Join("; ", result.Diagnostics.Take(3))}");
        }
    }
}
