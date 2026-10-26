namespace Dialekt.Green;

using System.Buffers;
using System.Runtime.InteropServices;

/// <summary>
/// Append-only byte arena holding green nodes back to back (header, padding, child handle table). Nodes are
/// addressed by <see cref="GreenNodeHandle"/> and never move, so handles stay valid when the buffer grows.
/// Call <see cref="Seal"/> when building is done; a sealed arena is immutable. A new version of a tree is built in
/// a <see cref="Fork"/>, which keeps every existing node (and thus every unchanged subtree) addressable.
/// </summary>
public sealed class GreenArena : IDisposable
{
    /// <summary>Bytes at the start that are never a node, so that offset 0 can mean "null".</summary>
    public const int ReservedBytes = 16;

    private byte[] _buffer;
    private int _length = ReservedBytes;
    private int _nodeCount;

    public GreenArena(int initialCapacity = 4096)
    {
        _buffer = Rent(Math.Max(initialCapacity, ReservedBytes));
        _buffer.AsSpan(0, ReservedBytes).Clear();
    }

    private GreenArena(GreenArena source)
    {
        _buffer = Rent(source._length);
        source.AsSpan().CopyTo(_buffer);
        _length = source._length;
        _nodeCount = source._nodeCount;
    }

    public bool IsSealed { get; private set; }
    /// <summary>Bytes in use.</summary>
    public int Length => _length;
    public int NodeCount => _nodeCount;

    public void Seal() => IsSealed = true;

    /// <summary>Starts a new, writable version that contains all nodes of this sealed arena at the same handles.</summary>
    public GreenArena Fork()
    {
        if (!IsSealed) throw new InvalidOperationException("Only a sealed arena can be forked.");
        return new GreenArena(this);
    }

    /// <summary>
    /// The used part of the buffer. Only valid until the next write (which may move the buffer); a sealed arena never changes.
    /// </summary>
    public ReadOnlySpan<byte> AsSpan() => _buffer.AsSpan(0, _length);

    public GreenNodeReader GetReader(GreenNodeHandle handle) => new(AsSpan(), handle);

    public GreenNodeHandle WriteNode(SyntaxKind kind, int fullWidth, ReadOnlySpan<GreenNodeHandle> children) =>
        WriteNode(kind, fullWidth, children, GreenNodeFlags.None);

    /// <summary>
    /// Appends a node. Only the intrinsic part of <paramref name="flags"/> is used; the propagated flags are derived from
    /// the children. For anything but a token the width must equal the sum of the children's widths; a token's
    /// children are its leading trivia, so its width may be larger.
    /// </summary>
    public GreenNodeHandle WriteNode(SyntaxKind kind, int fullWidth, ReadOnlySpan<GreenNodeHandle> children, GreenNodeFlags flags)
    {
        long sum = 0;
        foreach (var child in children)
        {
            if (child.Offset < ReservedBytes || child.Offset >= _length || (child.Offset & 3) != 0)
            {
                throw new ArgumentException($"Invalid child handle {child}.", nameof(children));
            }
            sum += GetReader(child).FullWidth;
        }
        var isToken = (flags & GreenNodeFlags.IsToken) != 0;
        if (isToken ? sum > fullWidth : sum != fullWidth)
        {
            throw new ArgumentException($"Width {fullWidth} does not match the children's width {sum}.", nameof(fullWidth));
        }
        return WriteNodeUnchecked(kind, fullWidth, children, flags);
    }

    internal GreenNodeHandle WriteNodeUnchecked(SyntaxKind kind, int fullWidth, ReadOnlySpan<GreenNodeHandle> children, GreenNodeFlags flags)
    {
        if (IsSealed) throw new InvalidOperationException("The arena is sealed.");
        if (children.Length > ushort.MaxValue) throw new ArgumentException("Too many children.", nameof(children));

        flags &= GreenNodeFlags.IntrinsicMask;
        if ((flags & GreenNodeFlags.IsError) != 0) flags |= GreenNodeFlags.ContainsError;
        if ((flags & GreenNodeFlags.IsTrivia) != 0) flags |= GreenNodeFlags.ContainsTrivia;
        foreach (var child in children)
        {
            flags |= GetReader(child).Flags & GreenNodeFlags.PropagatedMask;
        }

        var offset = _length;
        var size = GreenNodeHeader.AlignedSize + children.Length * sizeof(int);
        EnsureCapacity(offset + size);

        var header = new GreenNodeHeader(kind, fullWidth, flags, (ushort)children.Length, offset + GreenNodeHeader.AlignedSize);
        var span = _buffer.AsSpan(offset, size);
        MemoryMarshal.Write(span, in header);
        span[GreenNodeHeader.Size..GreenNodeHeader.AlignedSize].Clear();
        MemoryMarshal.AsBytes(children).CopyTo(span[GreenNodeHeader.AlignedSize..]);

        _length += size;
        _nodeCount++;
        return new GreenNodeHandle(offset);
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _buffer.Length) return;
        var bigger = Rent(Math.Max(required, _buffer.Length * 2));
        _buffer.AsSpan(0, _length).CopyTo(bigger);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = bigger;
    }

    private static byte[] Rent(int size) => ArrayPool<byte>.Shared.Rent(size);

    public void Dispose()
    {
        if (_buffer.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = [];
            _length = 0;
        }
    }
}
