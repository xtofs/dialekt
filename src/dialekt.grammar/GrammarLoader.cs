namespace Dialekt.Grammar;

using Dialekt.Langium;

/// <summary>Parses grammar text and builds its model in one step.</summary>
public static class GrammarLoader
{
    /// <summary>
    /// The syntax errors of the text are part of the diagnostics, next to the problems found in the model.
    /// </summary>
    public static GrammarBuildResult Load(string grammarText)
    {
        var parsed = LangiumParser.Parse(grammarText);
        var built = GrammarModelBuilder.Build(parsed.Tree);
        var syntaxErrors = parsed.Diagnostics.Select(d => new GrammarDiagnostic(GrammarSeverity.Error, GrammarDiagnosticCodes.SyntaxError, d.Message, d.Line, d.Column));
        var all = syntaxErrors.Concat(built.Diagnostics.Where(d => d.Code != GrammarDiagnosticCodes.SyntaxError || !parsed.Diagnostics.Any()))
            .OrderBy(d => d.Line).ThenBy(d => d.Column).ToList();
        return built with { Diagnostics = all };
    }
}
