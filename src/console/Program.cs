using System.Diagnostics;
using Dialekt;
using Dialekt.Scanning;

// Usage: dotnet run --project src/console -- [--tree] [file...]
// Without files, parses langium-grammar.langium and langium-types.langium from the repository root.
var showTree = args.Contains("--tree");
var files = args.Where(a => !a.StartsWith("--")).ToList();

AddDefaultFilesIfNoneProvided(ref files);

var failed = false;
foreach (var file in files)
{
    var content = File.ReadAllText(file);
    var sw = Stopwatch.StartNew();
    var result = Parser.Parse(content);
    sw.Stop();

    var nodeCount = result.Root.Descendants().Count() + 1;
    Console.WriteLine("{0}: {1} nodes, {2} diagnostics, {3} ms", file, nodeCount, result.Diagnostics.Count, sw.ElapsedMilliseconds);

    foreach (var d in result.Diagnostics)
    {
        Console.WriteLine("  {0}{1}", Path.GetFileName(file), d);
    }

    // // print all kinds of nodes in the syntax tree
    // var kinds = new HashSet<string>();
    // result.Root.Traverse(n => kinds.Add(n.Kind));
    // Console.WriteLine(string.Join(", ", kinds));

    if (showTree)
    {
        // Console.Write(result.Root.Dump());
        var treeFormatter = new TreeFormatter(file);
        treeFormatter.PrintTree(result.Root);
    }
    failed |= result.Diagnostics.Count > 0;
}
return failed ? 1 : 0;

static void AddDefaultFilesIfNoneProvided(ref List<string> files)
{
    if (files.Count == 0)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "langium-grammar.langium")))
        {
            dir = dir.Parent;
        }
        var root = dir?.FullName ?? ".";
        files = [Path.Combine(root, "langium-grammar.langium"), Path.Combine(root, "langium-types.langium")];
    }

}