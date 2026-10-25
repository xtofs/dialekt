using System.Reflection;

internal record struct CodePoint(char Char, string Name)
{
    public static CodePoint[] codePoints = LoadCodePoints();

    private static CodePoint[] LoadCodePoints()
    {
        Console.WriteLine(string.Join(", ", Assembly.GetExecutingAssembly().GetManifestResourceNames()));
        using var csv = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Dialekt.res.UnicodeData.txt")!);
        string? line;
        var codePoints = new List<CodePoint>();
        while ((line = csv.ReadLine()) != null)
        {
            var fields = line.Split(';');
            if (fields.Length > 0)
            {
                var code = (char)(int.Parse(fields[0], System.Globalization.NumberStyles.HexNumber));
                var name = fields[1] == "<control>" ? fields[10] : fields[1];
                var codePoint = new CodePoint(code, name);
                // Store or process the codePoint as needed
                codePoints.Add(codePoint);
            }
        }
        return [.. codePoints];
    }

    public static implicit operator (char Char, string Name)(CodePoint value)
    {
        return (value.Char, value.Name);
    }

    public static implicit operator CodePoint((char Char, string Name) value)
    {
        return new CodePoint(value.Char, value.Name);
    }
}