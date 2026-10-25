namespace Dialekt.Parsing;

/// <summary>A single edit: <paramref name="OldLength"/> characters at <paramref name="Start"/> were replaced by <paramref name="NewLength"/> new ones.</summary>
public readonly record struct TextChange(int Start, int OldLength, int NewLength)
{
    public int OldEnd => Start + OldLength;
    public int NewEnd => Start + NewLength;
    public int Delta => NewLength - OldLength;

    /// <summary>The smallest single change that turns <paramref name="oldText"/> into <paramref name="newText"/> (common prefix and suffix removed).</summary>
    public static TextChange Between(string oldText, string newText)
    {
        var max = Math.Min(oldText.Length, newText.Length);
        var prefix = 0;
        while (prefix < max && oldText[prefix] == newText[prefix])
        {
            prefix++;
        }
        var suffix = 0;
        while (suffix < max - prefix && oldText[^(suffix + 1)] == newText[^(suffix + 1)])
        {
            suffix++;
        }
        return new TextChange(prefix, oldText.Length - prefix - suffix, newText.Length - prefix - suffix);
    }
}
