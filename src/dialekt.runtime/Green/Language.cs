namespace Dialekt.Green;

public enum SlotShape : byte
{
    /// <summary>Exactly one child; a <see cref="SyntaxKind.Missing"/> placeholder if the parser did not find it.</summary>
    Required,
    /// <summary>At most one child; <see cref="SyntaxKind.Absent"/> if there is none.</summary>
    Optional,
    /// <summary>A <see cref="SyntaxKind.List"/> node with any number of children.</summary>
    List,
    /// <summary>A <see cref="SyntaxKind.SeparatedList"/> node: items alternating with separator tokens.</summary>
    SeparatedList,
}

/// <summary>One position in the fixed child layout of a node kind.</summary>
/// <param name="Name">Name of the slot, as used by <c>SyntaxNode.GetSlot</c>.</param>
/// <param name="Kind">The accepted kind when <paramref name="Category"/> is 0.</param>
/// <param name="Category">A language-defined set of accepted kinds (see <see cref="Language.IsInCategory"/>), or 0 for none.</param>
/// <param name="Separator">For separated lists: the separator token kind.</param>
public readonly record struct SlotSpec(string Name, SlotShape Shape, SyntaxKind Kind, int Category = 0, SyntaxKind Separator = SyntaxKind.None);

/// <summary>
/// Everything the language-agnostic runtime needs to know about a language: the names of its kinds, the fixed slot layout of
/// its node kinds, and which kinds are tokens or trivia. The scanner and parser of a language are separate classes; a
/// <see cref="Syntax.SyntaxTree"/> carries its language so that slots and kinds can be read by name.
/// </summary>
public abstract class Language
{
    public abstract string Name { get; }

    public abstract string GetKindName(SyntaxKind kind);

    /// <summary>The slots of <paramref name="kind"/>; empty for tokens, trivia, placeholders, lists and error nodes.</summary>
    public abstract ReadOnlySpan<SlotSpec> GetSlots(SyntaxKind kind);

    /// <summary>All node kinds that have slots.</summary>
    public abstract IEnumerable<SyntaxKind> KindsWithSlots { get; }

    /// <summary>Whether <paramref name="kind"/> belongs to the language-defined set <paramref name="category"/>.</summary>
    public abstract bool IsInCategory(int category, SyntaxKind kind);

    public abstract bool IsTokenKind(SyntaxKind kind);

    public abstract bool IsTriviaKind(SyntaxKind kind);

    public bool Accepts(in SlotSpec slot, SyntaxKind kind) => slot.Category == 0 ? kind == slot.Kind : IsInCategory(slot.Category, kind);

    /// <summary>The index of the slot called <paramref name="slotName"/> in <paramref name="kind"/>, or -1.</summary>
    public int IndexOf(SyntaxKind kind, string slotName)
    {
        var slots = GetSlots(kind);
        for (var i = 0; i < slots.Length; i++)
        {
            if (slots[i].Name == slotName) return i;
        }
        return -1;
    }
}
