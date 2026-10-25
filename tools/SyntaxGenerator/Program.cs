using System.Text;
using SyntaxGenerator;

// Usage: dotnet run --project tools/SyntaxGenerator [--check] [output file]
// Writes (or with --check, verifies) the typed syntax views generated from Dialekt.Green.SyntaxSchema.
var output = args.FirstOrDefault(a => !a.StartsWith("--")) ?? Generator.DefaultOutputPath();
var generated = Generator.Generate();

if (args.Contains("--check"))
{
    var current = File.Exists(output) ? File.ReadAllText(output) : "";
    if (current != generated)
    {
        Console.Error.WriteLine($"{output} is out of date; run: dotnet run --project tools/SyntaxGenerator");
        return 1;
    }
    return 0;
}

Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, generated, new UTF8Encoding(false));
Console.WriteLine($"wrote {output}");
return 0;
