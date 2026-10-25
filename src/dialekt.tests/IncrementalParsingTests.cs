using Dialekt.Parsing;
using Xunit;

public class IncrementalParsingTests
{
    private static readonly string[] Snippets =
    [
        ";", "}", "{", " ", "\n", "\n\n", "/*", "*/", "//", "'", "\"", "|", "&", "A", "ID", "=", "+=", "?=", "?", "[", "]", "(", ")", "<", ">",
        "@", ".", "..", "->", "=>", "grammar G", "import 'x';", "interface I {", "type T = A | B;", "R: a=ID;", "terminal X: /a/;", "\r\n", "#", "123",
    ];

    public static TheoryData<string> Files => new() { "grammar", "types" };

    private static string Load(string name) => name == "grammar" ? TestFiles.GrammarText : TestFiles.TypesText;

    private static string Describe(ParseResult r) => string.Join("\n", r.Diagnostics.Select(d => d.ToString()));

    /// <summary>The incremental result must be exactly what a full parse gives; nodes are hash-consed, so equal structure is an equal root handle.</summary>
    private static ParseResult ApplyAndCheck(ParseResult before, string oldText, TextChange change, string newText, string context)
    {
        var incremental = Parser.Parse(newText, before.Tree, change);
        var full = Parser.Parse(newText, incremental.Tree);

        Assert.True(full.Tree.Green.RootHandle == incremental.Tree.Green.RootHandle, $"different tree: {context}");
        Assert.Equal(Describe(full), Describe(incremental));
        Assert.Equal(newText.Length, incremental.Tree.Green.FullWidth);
        return incremental;
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Random_edits_give_the_same_tree_as_a_full_parse(string file)
    {
        var random = new Random(12345);
        var baseText = Load(file);
        var baseResult = Parser.Parse(baseText);

        for (var i = 0; i < 600; i++)
        {
            var (newText, change) = RandomEdit(random, baseText);
            ApplyAndCheck(baseResult, baseText, change, newText, $"{file} edit #{i}: {change} -> '{newText.Substring(change.Start, change.NewLength).Replace("\n", "\\n")}'");
        }
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void A_session_of_consecutive_edits_stays_consistent(string file)
    {
        var random = new Random(777);
        var text = Load(file);
        var result = Parser.Parse(text);

        for (var i = 0; i < 300; i++)
        {
            var (newText, change) = RandomEdit(random, text);
            result = ApplyAndCheck(result, text, change, newText, $"{file} session step #{i}: {change}");
            text = newText;
        }
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Edits_at_declaration_boundaries_give_the_same_tree(string file)
    {
        var text = Load(file);
        var result = Parser.Parse(text);
        var root = result.Tree.Root;
        var boundaries = root.Children.SelectMany(c => new[] { c.Position, c.TextStart, c.End }).Distinct().ToList();
        var random = new Random(99);

        foreach (var at in boundaries)
        {
            foreach (var snippet in Snippets)
            {
                var insert = new TextChange(at, 0, snippet.Length);
                ApplyAndCheck(result, text, insert, text.Insert(at, snippet), $"{file} insert '{snippet}' at {at}");
            }
            var length = Math.Min(random.Next(1, 40), text.Length - at);
            var delete = new TextChange(at, length, 0);
            ApplyAndCheck(result, text, delete, text.Remove(at, length), $"{file} delete {length} at {at}");
        }
    }

    [Fact]
    public void A_small_edit_inside_one_rule_reparses_only_that_rule()
    {
        var text = TestFiles.GrammarText;
        var before = Parser.Parse(text);
        var declarations = before.Tree.Root.Children.Count(c => !c.IsToken);

        var at = text.IndexOf("'interface' name=ID", StringComparison.Ordinal) + 3;
        var newText = text.Insert(at, "x");
        var after = Parser.Parse(newText, before.Tree, new TextChange(at, 0, 1));

        Assert.True(after.ReparsedDeclarations <= 2, $"reparsed {after.ReparsedDeclarations}");
        Assert.True(after.ReusedDeclarations >= declarations - 3, $"reused only {after.ReusedDeclarations} of {declarations}");
        Assert.Empty(after.Diagnostics);
    }

    [Fact]
    public void An_unchanged_text_reuses_everything()
    {
        var text = TestFiles.TypesText;
        var before = Parser.Parse(text);
        var after = Parser.Parse(text, before.Tree, new TextChange(0, 0, 0));

        Assert.True(after.Tree.Green.RootHandle == before.Tree.Green.RootHandle);
        Assert.Equal(0, after.ReparsedDeclarations);
    }

    [Fact]
    public void An_edit_that_swallows_later_declarations_is_handled()
    {
        var text = TestFiles.TypesText;
        var before = Parser.Parse(text);
        var change = new TextChange(0, 0, 2);
        var after = ApplyAndCheck(before, text, change, "/*" + text, "open comment at start");
        Assert.NotEmpty(after.Diagnostics);
    }

    [Fact]
    public void Compacting_keeps_the_tree_and_drops_old_versions()
    {
        var random = new Random(5);
        var text = TestFiles.GrammarText;
        var result = Parser.Parse(text);
        for (var i = 0; i < 200; i++)
        {
            var (newText, change) = RandomEdit(random, text);
            result = Parser.Parse(newText, result.Tree, change);
            text = newText;
        }

        var compact = result.Tree.Compact();

        Assert.True(compact.Arena.Length < result.Tree.Arena.Length);
        Assert.Equal(result.Tree.CountNodes(), compact.CountNodes());
        Assert.Equal(Shape(result.Tree.Root), Shape(compact.Root));
        // Parsing on top of the compacted tree still shares everything.
        Assert.True(Parser.Parse(text, compact).Tree.Green.RootHandle == compact.Green.RootHandle);
    }

    private static string Shape(Dialekt.Syntax.SyntaxNode root) =>
        string.Join(",", root.DescendantsAndSelf().Select(n => $"{n.Kind}:{n.FullWidth}"));

    private static (string NewText, TextChange Change) RandomEdit(Random random, string text)
    {
        var start = random.Next(text.Length + 1);
        switch (random.Next(3))
        {
            case 0:
                var insert = Snippets[random.Next(Snippets.Length)];
                return (text.Insert(start, insert), new TextChange(start, 0, insert.Length));
            case 1:
                var length = Math.Min(random.Next(1, 40), text.Length - start);
                return (text.Remove(start, length), new TextChange(start, length, 0));
            default:
                var replaced = Math.Min(random.Next(1, 20), text.Length - start);
                var replacement = Snippets[random.Next(Snippets.Length)];
                return (text.Remove(start, replaced).Insert(start, replacement), new TextChange(start, replaced, replacement.Length));
        }
    }
}
