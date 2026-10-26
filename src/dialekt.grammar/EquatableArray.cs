namespace Dialekt.Grammar;

using System.Collections;
using System.Collections.Immutable;

/// <summary>
/// An immutable array with value equality (same items in the same order), so that records containing it compare by value.
/// The model is compared this way to find out which parts of a grammar changed.
/// </summary>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
{
    private readonly ImmutableArray<T> _items;

    public EquatableArray(IEnumerable<T> items) => _items = [.. items];

    private ImmutableArray<T> Items => _items.IsDefault ? [] : _items;

    public int Count => Items.Length;
    public T this[int index] => Items[index];

    public bool Equals(EquatableArray<T> other) => Items.AsSpan().SequenceEqual(other.Items.AsSpan());
    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in Items)
        {
            hash.Add(item);
        }
        return hash.ToHashCode();
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
}

public static class EquatableArrayExtensions
{
    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> items) => new(items);
}
