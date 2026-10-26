namespace Dialekt.Scanning;

public readonly record struct SourceSpan(Position Start, Position End)
{
    public override string ToString() =>
        Start.Line == End.Line ?
        $"{Start.Line}:{Start.Column}-{End.Column}" :
        $"{Start}-{End}";
}
