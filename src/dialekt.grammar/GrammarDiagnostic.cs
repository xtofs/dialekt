namespace Dialekt.Grammar;

public enum GrammarSeverity
{
    Error,
    Warning,
}

public static class GrammarDiagnosticCodes
{
    public const string SyntaxError = "syntax-error";
    public const string DuplicateRule = "duplicate-rule";
    public const string UnresolvedRule = "unresolved-rule";
    public const string UnresolvedType = "unresolved-type";
    public const string NoEntryRule = "no-entry-rule";
    public const string MultipleEntryRules = "multiple-entry-rules";
    /// <summary>A construct of the grammar language that the generator does not support yet.</summary>
    public const string Unsupported = "unsupported";
}

/// <summary>A problem found in a grammar, with the position of the construct it is about.</summary>
public sealed record GrammarDiagnostic(GrammarSeverity Severity, string Code, string Message, int Line, int Column)
{
    public override string ToString() => $"({Line},{Column}): {Severity.ToString().ToLowerInvariant()} {Code}: {Message}";
}

/// <summary>The model together with the problems found while building it.</summary>
public sealed record GrammarBuildResult(GrammarModel Model, IReadOnlyList<GrammarDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(d => d.Severity == GrammarSeverity.Error);
}
