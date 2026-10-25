namespace Dialekt.Green;

/// <summary>
/// Index of a node inside a <see cref="GreenArena"/>. Offset 0 is never a node, so <c>default</c> is the null handle.
/// A handle stays valid in every arena forked from the one that issued it.
/// </summary>
public readonly record struct GreenNodeHandle(int Offset)
{
    public bool IsNull => Offset == 0;

    public override string ToString() => IsNull ? "null" : $"@{Offset}";
}
