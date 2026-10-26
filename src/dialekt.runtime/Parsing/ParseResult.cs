namespace Dialekt.Parsing;

using Dialekt.Syntax;

/// <param name="ReusedDeclarations">Top-level declarations taken unchanged from the previous tree (incremental parsing only).</param>
/// <param name="ReparsedDeclarations">Top-level declarations that were parsed from tokens.</param>
public sealed record ParseResult(SyntaxTree Tree, IReadOnlyList<Diagnostic> Diagnostics, int ReusedDeclarations = 0, int ReparsedDeclarations = 0);
