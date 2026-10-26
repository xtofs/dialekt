namespace Dialekt.Grammar;

/// <summary>Small traversals over the element trees.</summary>
public static class ModelWalker
{
    public static IEnumerable<GrammarElement> Children(GrammarElement element) => element switch
    {
        AlternativesElement a => a.Branches,
        GroupElement g => g.Items,
        UnorderedGroupElement u => u.Items,
        GuardedElement g => g.Items,
        RepetitionElement r => [r.Inner],
        AssignmentElement a => [a.Terminal],
        PredicatedElement p => [p.Inner],
        CrossReferenceElement { Terminal: { } t } => [t],
        _ => [],
    };

    /// <summary>The element and everything below it, parents before children.</summary>
    public static IEnumerable<GrammarElement> DescendantsAndSelf(GrammarElement element)
    {
        yield return element;
        foreach (var child in Children(element))
        {
            foreach (var descendant in DescendantsAndSelf(child))
            {
                yield return descendant;
            }
        }
    }

    public static IEnumerable<string> Keywords(GrammarElement element) =>
        DescendantsAndSelf(element).OfType<KeywordElement>().Select(k => k.Value);

    public static IEnumerable<TerminalElement> Children(TerminalElement element) => element switch
    {
        TerminalAlternatives a => a.Branches,
        TerminalGroup g => g.Items,
        TerminalRepetition r => [r.Inner],
        NegatedTerminal n => [n.Inner],
        UntilTerminal u => [u.Inner],
        LookaheadTerminal l => [l.Inner],
        _ => [],
    };

    public static IEnumerable<TerminalElement> DescendantsAndSelf(TerminalElement element)
    {
        yield return element;
        foreach (var child in Children(element))
        {
            foreach (var descendant in DescendantsAndSelf(child))
            {
                yield return descendant;
            }
        }
    }
}

internal static class EnumerableExtensions
{
    /// <summary>The single matching item, or null when there are none or several.</summary>
    public static T? SingleOrDefaultIfMany<T>(this IEnumerable<T> items, Func<T, bool> predicate) where T : class
    {
        T? found = null;
        foreach (var item in items.Where(predicate))
        {
            if (found is not null) return null;
            found = item;
        }
        return found;
    }
}
