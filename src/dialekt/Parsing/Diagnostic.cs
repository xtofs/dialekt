namespace Dialekt.Parsing;


public sealed record Diagnostic(string Message, int Line, int Column, int Length, bool IsLexical)
{
    public override string ToString() => $"({Line},{Column}): error: {Message}";
}
