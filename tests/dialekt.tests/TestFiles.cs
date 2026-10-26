static class TestFiles
{
    /// <summary>The declarations of a file (the members list under the Grammar node).</summary>
    public static IEnumerable<Dialekt.Syntax.SyntaxNode> Members(Dialekt.Syntax.SyntaxTree tree) => tree.Root.GetChild(1).Children;

    public static string GrammarText => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "langium-grammar.langium"));
    public static string TypesText => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "langium-types.langium"));
}
