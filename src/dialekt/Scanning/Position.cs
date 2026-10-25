namespace Dialekt.Scanning;

public readonly record struct Position(int Offset, int Line, int Column)
{
    public override string ToString() => $"{Line}:{Column}";
}
