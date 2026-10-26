using Dialekt.Green;
using Dialekt.Parsing;
using Dialekt.Syntax;
using Xunit;

public class RedTreeTests
{
    public static TheoryData<string> Texts => new() { "grammar", "types" };

    private static SyntaxTree Parse(string name) => LangiumParser.Parse(name == "grammar" ? TestFiles.GrammarText : TestFiles.TypesText).Tree;

    [Theory]
    [MemberData(nameof(Texts))]
    public void Tokens_and_trivia_reproduce_the_source(string name)
    {
        var tree = Parse(name);
        var tokens = tree.Root.DescendantsAndSelf().Where(n => n.IsToken).ToList();
        Assert.Equal(tree.Source, string.Concat(tokens.Select(t => t.FullText)));
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void Children_are_contiguous_and_know_their_parent(string name)
    {
        var tree = Parse(name);
        foreach (var node in tree.Root.DescendantsAndSelf())
        {
            var position = node.Position;
            foreach (var child in node.Children)
            {
                Assert.Equal(node, child.Parent);
                Assert.Equal(position, child.Position);
                position = child.End;
            }
            // a token's children are only its leading trivia; the token text follows them
            if (node.ChildCount > 0 && !node.IsToken) Assert.Equal(node.End, position);
            if (node.IsToken) Assert.Equal(node.TextStart, position);
        }
        Assert.Null(tree.Root.Parent);
        Assert.Equal(0, tree.Root.Position);
        Assert.Equal(tree.Source.Length, tree.Root.End);
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void Descendants_match_the_green_node_count(string name)
    {
        var tree = Parse(name);
        Assert.Equal(tree.CountNodes(), tree.Root.DescendantsAndSelf().Count());
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void FindToken_returns_the_token_covering_every_offset(string name)
    {
        var tree = Parse(name);
        for (var offset = 0; offset < tree.Source.Length; offset++)
        {
            var token = tree.Root.FindToken(offset);
            Assert.True(token.IsToken);
            Assert.True(token.Position <= offset && offset < token.End, $"offset {offset} -> {token}");
        }
        Assert.Equal(SyntaxKind.EndOfFileToken, tree.Root.FindToken(tree.Source.Length).Kind);
        // text inside a token maps to that token
        var identifier = tree.Root.Descendants().First(n => n.Kind == LangiumKinds.IdentifierToken);
        Assert.Equal(identifier, tree.Root.FindToken(identifier.TextStart));
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void Line_and_column_match_a_direct_count(string name)
    {
        var tree = Parse(name);
        foreach (var token in tree.Root.Descendants().Where(n => n.IsToken))
        {
            var before = tree.Source[..token.TextStart];
            var line = before.Count(c => c == '\n') + 1;
            var column = token.TextStart - (before.LastIndexOf('\n') + 1) + 1;
            Assert.Equal((line, column), token.StartLineAndColumn);
        }
    }

    [Fact]
    public void Node_text_excludes_leading_trivia()
    {
        var tree = LangiumParser.Parse("  /* c */ A: b=ID;\n").Tree;
        var rule = TestFiles.Members(tree).First();
        Assert.Equal(LangiumKinds.ParserRule, rule.Kind);
        Assert.Equal("A: b=ID;", rule.Text);
        Assert.Equal("  /* c */ A: b=ID;", rule.FullText);
        Assert.Equal((1, 11), rule.StartLineAndColumn);
        Assert.Equal([SyntaxKind.List, LangiumKinds.Grammar], rule.Ancestors().Select(a => a.Kind));
    }

    [Fact]
    public void Parent_of_a_navigated_node_is_exact_even_for_zero_width_children()
    {
        var tree = LangiumParser.Parse("A: b=ID;\nB: c=ID;\n").Tree;
        var rules = TestFiles.Members(tree).ToList();

        foreach (var rule in rules)
        {
            var modifier = rule.GetSlot("Modifier");
            Assert.True(modifier.IsAbsent);
            Assert.Equal(rule, modifier.Parent);
        }
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void Ancestors_match_the_path_taken_going_down(string name)
    {
        var tree = Parse(name);
        foreach (var token in tree.Root.Descendants().Where(n => n.IsToken))
        {
            // the path down, collected by navigating from the root
            var expected = new List<SyntaxNode>();
            var node = tree.Root;
            while (node != token)
            {
                expected.Add(node);
                node = node.Children.First(c => c.FullWidth > 0 && c.Position <= token.Position && token.End <= c.End);
            }
            expected.Reverse();

            Assert.Equal(expected, token.Ancestors());
            Assert.Equal(expected[0], token.Parent);
            // the parent was handed out without knowing its own parent; asking anyway still gives the right answer
            if (expected.Count > 1) Assert.Equal(expected[1], token.Parent!.Value.Parent);
        }
    }
}
