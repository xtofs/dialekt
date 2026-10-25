namespace Dialekt.Green;

using System.Runtime.InteropServices;

/// <summary>
/// Allocation-free view of one node inside an arena buffer. It only reads; it knows neither its parent nor its
/// absolute position, so callers that need those pass them down while walking.
/// </summary>
public readonly ref struct GreenNodeReader
{
    private readonly ReadOnlySpan<byte> _buffer;
    private readonly int _offset;

    public GreenNodeReader(ReadOnlySpan<byte> buffer, GreenNodeHandle handle)
    {
        if (handle.Offset < GreenArena.ReservedBytes || handle.Offset + GreenNodeHeader.Size > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(handle));
        }
        _buffer = buffer;
        _offset = handle.Offset;
    }

    public GreenNodeHandle Handle => new(_offset);
    public GreenNodeHeader Header => MemoryMarshal.Read<GreenNodeHeader>(_buffer[_offset..]);

    public SyntaxKind Kind => Header.Kind;
    public int FullWidth => Header.FullWidth;
    public GreenNodeFlags Flags => Header.Flags;
    public int ChildCount => Header.ChildCount;

    public bool IsToken => (Flags & GreenNodeFlags.IsToken) != 0;
    public bool IsTrivia => (Flags & GreenNodeFlags.IsTrivia) != 0;
    public bool ContainsError => (Flags & GreenNodeFlags.ContainsError) != 0;

    /// <summary>Handle in slot <paramref name="slotIndex"/>.</summary>
    public GreenNodeHandle GetChildHandle(int slotIndex)
    {
        var header = Header;
        if ((uint)slotIndex >= header.ChildCount) throw new ArgumentOutOfRangeException(nameof(slotIndex));
        return new GreenNodeHandle(MemoryMarshal.Read<int>(_buffer[(header.ChildrenOffset + slotIndex * sizeof(int))..]));
    }

    /// <summary>Same as <see cref="GetChildHandle"/>; the positional slot is the only way to address a child.</summary>
    public GreenNodeHandle GetSlot(int slotIndex) => GetChildHandle(slotIndex);

    public GreenNodeReader GetChild(int slotIndex) => new(_buffer, GetChildHandle(slotIndex));

    /// <summary>
    /// The first token below this node that is really there (skipping absent and missing placeholders), or false if the
    /// node has none. Used wherever "the first token" matters (leading trivia, reuse of declarations).
    /// </summary>
    public bool TryGetFirstToken(out GreenNodeReader token)
    {
        if (IsToken)
        {
            token = this;
            return (Flags & GreenNodeFlags.IsMissing) == 0;
        }
        for (var i = 0; i < ChildCount; i++)
        {
            if (GetChild(i).TryGetFirstToken(out token)) return true;
        }
        token = default;
        return false;
    }

    /// <summary>The last token below this node that is really there; see <see cref="TryGetFirstToken"/>.</summary>
    public bool TryGetLastToken(out GreenNodeReader token)
    {
        if (IsToken)
        {
            token = this;
            return (Flags & GreenNodeFlags.IsMissing) == 0;
        }
        for (var i = ChildCount - 1; i >= 0; i--)
        {
            if (GetChild(i).TryGetLastToken(out token)) return true;
        }
        token = default;
        return false;
    }

    /// <summary>For a token: the width of its leading trivia, i.e. the offset of the token text within the node.</summary>
    public int LeadingTriviaWidth
    {
        get
        {
            var sum = 0;
            for (var i = 0; i < ChildCount; i++)
            {
                sum += GetChild(i).FullWidth;
            }
            return sum;
        }
    }

    /// <summary>For a token: the width of the token text itself.</summary>
    public int TextWidth => FullWidth - LeadingTriviaWidth;
}
