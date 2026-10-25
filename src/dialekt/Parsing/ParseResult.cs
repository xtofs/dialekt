namespace Dialekt.Parsing;

using Dialekt.Green;

public sealed record ParseResult(GreenTree Tree, IReadOnlyList<Diagnostic> Diagnostics);
