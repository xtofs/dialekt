namespace Dialekt.Green;

using System.Numerics;
using System.Runtime.InteropServices;

/// <summary>
/// Flyweight table over one arena: asking for a node that already exists (same kind, flags, width and children) returns
/// the existing handle instead of writing a copy. Because nodes are purely relative, equal subtrees from different places
/// in a file, or from different versions of it, collapse to one node.
/// </summary>
public sealed class GreenNodeCache
{
    private readonly GreenArena _arena;
    private int[] _table;
    private int _count;

    public GreenNodeCache(GreenArena arena, int capacity = 1024)
    {
        _arena = arena;
        _table = new int[BitOperations.RoundUpToPowerOf2((uint)Math.Max(capacity, 16))];
    }

    /// <summary>A cache that already knows every node of <paramref name="arena"/> (e.g. a fork of an earlier version).</summary>
    public static GreenNodeCache FromArena(GreenArena arena)
    {
        var cache = new GreenNodeCache(arena, arena.NodeCount * 2);
        var buffer = arena.AsSpan();
        for (var offset = GreenArena.ReservedBytes; offset < buffer.Length;)
        {
            var handle = new GreenNodeHandle(offset);
            cache.Insert(handle, HashOf(buffer, handle));
            offset += GreenNodeHeader.AlignedSize + MemoryMarshal.Read<GreenNodeHeader>(buffer[offset..]).ChildCount * sizeof(int);
        }
        return cache;
    }

    public int Hits { get; private set; }
    public int Misses { get; private set; }

    public GreenNodeHandle GetOrAdd(SyntaxKind kind, int fullWidth, ReadOnlySpan<GreenNodeHandle> children, GreenNodeFlags flags = GreenNodeFlags.None)
    {
        flags &= GreenNodeFlags.IntrinsicMask;
        var childBytes = MemoryMarshal.AsBytes(children);
        var hash = Hash(kind, fullWidth, flags, childBytes);

        var buffer = _arena.AsSpan();
        var mask = _table.Length - 1;
        for (var i = hash & mask; _table[i] != 0; i = (i + 1) & mask)
        {
            var header = MemoryMarshal.Read<GreenNodeHeader>(buffer[_table[i]..]);
            if (header.Kind == kind && header.FullWidth == fullWidth && (header.Flags & GreenNodeFlags.IntrinsicMask) == flags
                && header.ChildCount == children.Length && buffer.Slice(header.ChildrenOffset, childBytes.Length).SequenceEqual(childBytes))
            {
                Hits++;
                return new GreenNodeHandle(_table[i]);
            }
        }

        Misses++;
        var handle = _arena.WriteNodeUnchecked(kind, fullWidth, children, flags);
        Insert(handle, hash);
        return handle;
    }

    private void Insert(GreenNodeHandle handle, int hash)
    {
        if ((_count + 1) * 2 > _table.Length)
        {
            Grow();
        }
        var mask = _table.Length - 1;
        var i = hash & mask;
        while (_table[i] != 0)
        {
            i = (i + 1) & mask;
        }
        _table[i] = handle.Offset;
        _count++;
    }

    private void Grow()
    {
        var old = _table;
        _table = new int[old.Length * 2];
        var buffer = _arena.AsSpan();
        var mask = _table.Length - 1;
        foreach (var offset in old.Where(o => o != 0))
        {
            var i = HashOf(buffer, new GreenNodeHandle(offset)) & mask;
            while (_table[i] != 0)
            {
                i = (i + 1) & mask;
            }
            _table[i] = offset;
        }
    }

    private static int HashOf(ReadOnlySpan<byte> buffer, GreenNodeHandle handle)
    {
        var header = MemoryMarshal.Read<GreenNodeHeader>(buffer[handle.Offset..]);
        return Hash(header.Kind, header.FullWidth, header.Flags & GreenNodeFlags.IntrinsicMask,
            buffer.Slice(header.ChildrenOffset, header.ChildCount * sizeof(int)));
    }

    private static int Hash(SyntaxKind kind, int fullWidth, GreenNodeFlags flags, ReadOnlySpan<byte> childBytes)
    {
        var hash = new HashCode();
        hash.Add((ushort)kind);
        hash.Add(fullWidth);
        hash.Add((ushort)flags);
        hash.AddBytes(childBytes);
        return hash.ToHashCode();
    }
}
