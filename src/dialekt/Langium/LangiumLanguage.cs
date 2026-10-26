namespace Dialekt.Langium;

using System.Reflection;
using Dialekt.Green;

/// <summary>The Langium grammar language: kind names, slot layouts and token classification for the runtime.</summary>
public sealed class LangiumLanguage : Language
{
    public static LangiumLanguage Instance { get; } = new();

    private LangiumLanguage() { }

    // Built on first use from the constants of LangiumKinds; only needed to print kinds.
    private static readonly Lazy<string?[]> KindNames = new(() =>
    {
        var names = new string?[(int)LangiumKinds.ParenthesizedTerminal + 1];
        foreach (var field in typeof(LangiumKinds).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            names[(int)(SyntaxKind)field.GetRawConstantValue()!] = field.Name;
        }
        return names;
    });

    public override string Name => "Langium";

    public override string GetKindName(SyntaxKind kind)
    {
        var names = KindNames.Value;
        return (int)kind < names.Length && names[(int)kind] is { } name ? name : kind.ToString();
    }

    public override ReadOnlySpan<SlotSpec> GetSlots(SyntaxKind kind) => LangiumSchema.GetSlots(kind);

    public override IEnumerable<SyntaxKind> KindsWithSlots => LangiumSchema.KindsWithSlots();

    public override bool IsInCategory(int category, SyntaxKind kind) => LangiumSchema.IsInCategory(category, kind);

    public override bool IsTokenKind(SyntaxKind kind) => LangiumFacts.IsToken(kind);

    public override bool IsTriviaKind(SyntaxKind kind) => LangiumFacts.IsTrivia(kind);
}
