# TASK PROMPT: Refactor Node Hierarchy to a Flat-Arena Green Tree Architecture

## Objective

Refactor the existing AST node implementation (`Node` class hierarchy using standard C# heap references) into an immutable, flat-arena **Green Tree Architecture**. The new design must eliminate individual node heap allocations, minimize garbage collection overhead, achieve optimal cache locality, and support safe structural sharing across immutable incremental updates (Roslyn-style parser design).

---

## 1. Core Architectural Constraints & Patterns

1. **Flat Arena Storage (Contiguous Memory):**
   - Green nodes MUST NOT be individual heap objects linked by reference.
   - All Green node structural data must be serialized sequentially into an array pool, unmanaged buffer, or `Memory<byte>` arena (`GreenArena`).
   - Node identifiers MUST be lightweight `int` offsets or index handles (`GreenNodeHandle`) into the arena rather than object pointers.

2. **Structural immutability & Reuse:**
   - Green nodes must be purely relative (storing only child slot indices, relative offsets/lengths, and `SyntaxKind`). They must have **NO knowledge of absolute file offsets or parent links**.
   - Identical Green subtrees must be deduplicatable (flyweight pattern) via a `GreenNodeCache` hash table to maximize structural sharing across parse tree updates.

3. **Untyped Green Layer:**
   - The Green Tree layer must be unified and untyped. Grammar concepts (e.g., `IfStatement`, `BinaryExpression`) are distinguished strictly by a scalar `ushort` or `enum` (`SyntaxKind`).
   - Slot arrays/children are addressed by positional slot index (`GetSlot(int index)`), not named C# fields.

4. **Zero-Allocation Traversals:**
   - Reading or inspecting Green nodes must be done via lightweight `readonly ref struct` wrappers (`GreenNodeSpan` or `GreenNodeReader`) that wrap a `ReadOnlySpan<byte>` or direct pointer/reference to the underlying arena data.

---

## 2. Refactoring Target Specification

Replace the existing `Node` graph with the following core constructs:

### A. The Layout Spec (`GreenNodeHeader`)

Design a fixed-size byte layout (or sequentially packed struct layout) for every node in the arena:

- `SyntaxKind`: 2 bytes (`ushort`)
- `FullWidth`: 4 bytes (`int`) - Total span including leading/trailing trivia.
- `Flags`: 2 bytes (`ushort`) - Bit flags indicating error tokens, missing nodes, trivia presence, etc.
- `ChildCount`: 2 bytes (`ushort`)
- `ChildrenOffset`: 4 bytes (`int`) - Index/Offset where the contiguous array of child `GreenNodeHandle` structs begins in the arena buffer.

### B. Arena Manager (`GreenArena`)

Create an arena memory pool manager:

- Backing store using `System.Buffers.ArrayPool<byte>` or pinned native memory.
- `GreenNodeHandle WriteNode(SyntaxKind kind, int fullWidth, ReadOnlySpan<GreenNodeHandle> children)`: Allocates space sequentially in the current active page, writes the header and child handle table, and returns an offset handle.
- Fully immutable once built; modification requires creating a new version of affected subtrees in a new/shared arena session while retaining unmodified handles.

### C. Inspection API (`GreenNodeReader`)

Implement a `readonly ref struct` that provides a safe, high-level API over an offset in an arena:

```csharp
public readonly ref struct GreenNodeReader
{
    private readonly ReadOnlySpan<byte> _buffer;
    private readonly int _offset;

    public SyntaxKind Kind => ...;
    public int FullWidth => ...;
    public int ChildCount => ...;

    public GreenNodeHandle GetChildHandle(int slotIndex);
    public GreenNodeReader GetChild(int slotIndex);
}
```
