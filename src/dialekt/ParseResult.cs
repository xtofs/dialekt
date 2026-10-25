namespace Dialekt; 

public sealed record ParseResult(Node Root, IReadOnlyList<Diagnostic> Diagnostics);
