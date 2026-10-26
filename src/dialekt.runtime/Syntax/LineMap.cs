namespace Dialekt.Syntax;

/// <summary>Maps character offsets to 1-based line and column numbers.</summary>
public sealed class LineMap
{
    private readonly int[] _lineStarts;

    public LineMap(string source)
    {
        var lineStarts = new List<int> { 0 };
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] == '\n')
            {
                lineStarts.Add(i + 1);
            }
        }
        _lineStarts = [.. lineStarts];
    }

    public int LineCount => _lineStarts.Length;

    public (int Line, int Column) GetLineAndColumn(int offset)
    {
        var index = Array.BinarySearch(_lineStarts, offset);
        var line = index >= 0 ? index : ~index - 1;
        return (line + 1, offset - _lineStarts[line] + 1);
    }
}
