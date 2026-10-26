namespace Dialekt.Green;

using System.Runtime.InteropServices;

[Flags]
public enum GreenNodeFlags : ushort
{
    None = 0,

    // intrinsic: part of a node's identity
    IsToken = 1,
    IsTrivia = 2,
    IsError = 4,
    IsMissing = 8,

    // propagated: derived from the children by the arena
    ContainsError = 16,
    ContainsTrivia = 32,

    IntrinsicMask = IsToken | IsTrivia | IsError | IsMissing,
    PropagatedMask = ContainsError | ContainsTrivia,
}

/// <summary>
/// Fixed 14-byte (packed) record at the start of every node in the arena. It is followed by 2 padding bytes and then,
/// 4-byte aligned, the child handle table (<see cref="ChildCount"/> × 4 bytes) that <see cref="ChildrenOffset"/> points to.
/// Nothing in here refers to absolute source positions or to a parent.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct GreenNodeHeader(SyntaxKind kind, int fullWidth, GreenNodeFlags flags, ushort childCount, int childrenOffset)
{
    public const int Size = 14;
    /// <summary>Header plus padding; where the child table of a node written by <see cref="GreenArena"/> starts.</summary>
    public const int AlignedSize = 16;

    public SyntaxKind Kind { get; } = kind;
    /// <summary>Width of the node's text including leading trivia (token) or the sum of its children's widths.</summary>
    public int FullWidth { get; } = fullWidth;
    public GreenNodeFlags Flags { get; } = flags;
    public ushort ChildCount { get; } = childCount;
    /// <summary>Arena offset of the first child handle.</summary>
    public int ChildrenOffset { get; } = childrenOffset;
}
