static class TestFiles
{
    public static string GrammarText => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "langium-grammar.langium"));
    public static string TypesText => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "langium-types.langium"));
}
