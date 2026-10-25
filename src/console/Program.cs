using System.Diagnostics;
using Dialekt;
using Dialekt.Parsing;

internal class Program
{
    private static int Main(string[] args)
    {
        // Usage: dotnet run --project src/console -- [--tree [--trivia]] [file...]
        // Without files, parses langium-grammar.langium and langium-types.langium from the repository root.
        var showTree = args.Contains("--tree");
        var showTrivia = args.Contains("--trivia");
        var isEdit = args.Contains("--edit");
        var files = args.Where(a => !a.StartsWith("--")).ToList();

        AddDefaultFilesIfNoneProvided(ref files);

        // warmup
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            Parser.Parse(content);
        }


        var failed = false;
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var sw = Stopwatch.StartNew();
            var result = Parser.Parse(content);
            sw.Stop();

            var arena = result.Tree.Arena;
            var count = result.Tree.CountNodes();
            Console.WriteLine("{0}: {1} nodes ({2} unique: {3:P2}), {4} bytes), {5} diagnostics, {6} ms",
                file, count, arena.NodeCount, (float)arena.NodeCount / count, arena.Length, result.Diagnostics.Count, sw.ElapsedMilliseconds);

            // Parsing again on top of the previous tree must share every node with it.
            var reparsed = Parser.Parse(content, result.Tree);
            Console.WriteLine("  reparse: {0}, arena grew by {1} bytes", reparsed.Tree.Green.RootHandle == result.Tree.Green.RootHandle ? "same root" : "NEW root", reparsed.Tree.Arena.Length - arena.Length);

            if (isEdit)
            {
                // Incremental parse after a one-character edit in the middle of the file.
                var at = content.IndexOf('\n', content.Length / 2 - 5);
                var insert = "  ";
                var edited = content.Insert(at, insert);

                Console.WriteLine("edited: {0}",
                    edited[(at - 20)..at] + "⟨HERE⟩" + edited[at..(at + 20)].WithControlCharactersAsPictures());

                var incremental = Parser.ReParse(edited, result.Tree, new TextChange(at, 0, insert.Length));
                Console.WriteLine("  edit at {0}: reused {1} declarations, reparsed {2}", at, incremental.ReusedDeclarations, incremental.ReparsedDeclarations);
            }

            foreach (var d in result.Diagnostics)
            {
                Console.WriteLine("  {0}{1}", Path.GetFileName(file), d);
            }

            if (showTree)
            {
                var treeFormatter = new TreeFormatter(file) { ShowTrivia = showTrivia };
                treeFormatter.PrintTree(result.Tree);
            }
            failed |= result.Diagnostics.Count > 0;
        }
        return failed ? 1 : 0;
    }

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
}
