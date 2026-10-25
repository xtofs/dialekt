namespace Dialekt;

using Dialekt.Green;

public sealed record ParseResult(GreenTree Tree, IReadOnlyList<Diagnostic> Diagnostics);
