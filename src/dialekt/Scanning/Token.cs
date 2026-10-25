namespace Dialekt.Scanning;

internal record Token(string Kind, string Text, SourceSpan Span, string? Error = null)
{
    public const string Eof = "Eof";
    public const string ErrorKind = "Error";

    public bool IsTrivia => Kind is "Whitespace" or "SingleLineComment" or "MultiLineComment";
    public bool IsSymbol => Scanner.SymbolKinds.Contains(Kind);
}
