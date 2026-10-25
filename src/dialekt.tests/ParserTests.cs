using Dialekt.Green;
using Dialekt.Parsing;
using Xunit;

public class ParserTests
{
    [Fact]
    public void The_langium_files_parse_without_diagnostics()
    {
        Assert.Empty(Parser.Parse(TestFiles.GrammarText).Diagnostics);
        Assert.Empty(Parser.Parse(TestFiles.TypesText).Diagnostics);
    }

    [Fact]
    public void Parsing_on_top_of_the_previous_tree_shares_all_nodes()
    {
        var first = Parser.Parse(TestFiles.GrammarText);
        var second = Parser.Parse(TestFiles.GrammarText, first.Tree);

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
        var diagnostic = Assert.Single(Parser.Parse(text).Diagnostics);
        Assert.Equal((line, column, message), (diagnostic.Line, diagnostic.Column, diagnostic.Message));
    }

    [Fact]
    public void Parsing_continues_after_an_error()
    {
        var result = Parser.Parse("A: b=; B: c=ID; C: d=; D: 'ok';\n");
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Equal(2, TestFiles.Members(result.Tree).Count(n => n.Kind == SyntaxKind.ParserRule));
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
            Assert.True(SyntaxFacts.TryGetSymbolKind(symbol, out var kind), $"'{symbol}' is not recognized");
            kinds.Add(kind);
        }
        Assert.Equal(symbols.Length, kinds.Distinct().Count());
        Assert.Equal(SyntaxFacts.MaxSymbolLength, symbols.Max(s => s.Length));

        // nothing longer than MaxSymbolLength can be a symbol: extend every symbol by every symbol character
        var alphabet = symbols.SelectMany(s => s).Distinct().ToArray();
        foreach (var symbol in symbols.Where(s => s.Length == SyntaxFacts.MaxSymbolLength))
        {
            foreach (var c in alphabet)
            {
                Assert.False(SyntaxFacts.TryGetSymbolKind(symbol + c, out _), $"'{symbol}{c}' must not be a symbol");
            }
        }
    }
}
