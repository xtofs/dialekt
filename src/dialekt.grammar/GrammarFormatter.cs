namespace Dialekt.Grammar;

using System.Text;

/// <summary>Writes the model back as grammar-like text: for diagnostics, for comments in generated code, and for looking at a model.</summary>
public static class GrammarFormatter
{
    public static string Format(GrammarElement element) => element switch
    {
        AlternativesElement a => string.Join(" | ", a.Branches.Select(Format)),
        GroupElement g => string.Join(" ", g.Items.Select(i => Format(i, nested: true))),
        UnorderedGroupElement u => string.Join(" & ", u.Items.Select(i => Format(i, nested: true))),
        GuardedElement g => "<guard> " + string.Join(" ", g.Items.Select(i => Format(i, nested: true))),
        RepetitionElement r => Format(r.Inner, nested: true) + CardinalityText(r.Cardinality),
        AssignmentElement a => a.Feature + OperatorText(a.Operator) + Format(a.Terminal, nested: true),
        KeywordElement k => Quote(k.Value),
        RuleCallElement c => c.RuleName + (c.Arguments.Count > 0 ? "<" + string.Join(", ", c.Arguments) + ">" : ""),
        CrossReferenceElement x => "[" + (x.IsMulti ? "+" : "") + x.TypeName + (x.Terminal is null ? "" : ":" + Format(x.Terminal)) + "]",
        EndOfFileElement => "EOF",
        ActionElement a => "{" + (a.InferredType is not null ? "infer " + a.InferredType : a.TypeName) + (a.Feature is not null ? "." + a.Feature + "=current" : "") + "}",
        PredicatedElement p => "=>" + Format(p.Inner, nested: true),
        _ => element.ToString()!,
    };

    /// <summary>Parenthesizes alternatives and groups where they are an operand.</summary>
    private static string Format(GrammarElement element, bool nested) =>
        nested && element is AlternativesElement or GroupElement { Items.Count: > 1 } or UnorderedGroupElement ? "(" + Format(element) + ")" : Format(element);

    public static string Format(TerminalElement element) => element switch
    {
        TerminalAlternatives a => string.Join(" | ", a.Branches.Select(Format)),
        TerminalGroup g => string.Join(" ", g.Items.Select(i => Format(i, nested: true))),
        TerminalRepetition r => Format(r.Inner, nested: true) + CardinalityText(r.Cardinality),
        CharacterRange c => Quote(c.Left) + (c.Right is null ? "" : ".." + Quote(c.Right)),
        TerminalRuleCall c => c.RuleName,
        RegexTerminal r => "/" + r.Pattern + "/" + r.Flags,
        WildcardTerminal => ".",
        NegatedTerminal n => "!" + Format(n.Inner, nested: true),
        UntilTerminal u => "->" + Format(u.Inner, nested: true),
        LookaheadTerminal l => "(" + l.Operator + Format(l.Inner) + ")",
        _ => element.ToString()!,
    };

    private static string Format(TerminalElement element, bool nested) =>
        nested && element is TerminalAlternatives or TerminalGroup { Items.Count: > 1 } ? "(" + Format(element) + ")" : Format(element);

    public static string Format(GrammarRule rule) => rule switch
    {
        ParserRule p => (p.IsEntry ? "entry " : "") + (p.IsFragment ? "fragment " : "") + p.Name
            + (p.Parameters.Count > 0 ? "<" + string.Join(", ", p.Parameters) + ">" : "")
            + (p.ReturnType is not null ? " returns " + p.ReturnType : "") + (p.InferredType is not null ? " infers " + p.InferredType : "")
            + ": " + Format(p.Body) + ";",
        TerminalRule t => (t.IsHidden ? "hidden " : "") + "terminal " + (t.IsFragment ? "fragment " : "") + t.Name
            + (t.ReturnType is not null ? " returns " + t.ReturnType : "") + ": " + Format(t.Definition) + ";",
        InfixRule i => "infix " + i.Name + " ...;",
        _ => rule.ToString()!,
    };

    /// <summary>A readable listing of the whole model.</summary>
    public static string Describe(GrammarModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"grammar {model.Name ?? "(unnamed)"}  entry: {model.EntryRule?.Name ?? "(none)"}");
        sb.AppendLine("keywords: " + string.Join(" ", model.Keywords.Select(Quote)));
        sb.AppendLine("hidden terminals: " + string.Join(", ", model.HiddenTerminals.Select(t => t.Name)));
        foreach (var rule in model.Rules)
        {
            sb.AppendLine("  " + Format(rule));
        }
        foreach (var type in model.Types)
        {
            sb.AppendLine("  " + (type is InterfaceDeclaration i ? $"interface {i.Name} ({i.Attributes.Count} attributes)" : $"type {type.Name}"));
        }
        return sb.ToString();
    }

    private static string CardinalityText(Cardinality cardinality) => cardinality switch { Cardinality.Optional => "?", Cardinality.ZeroOrMore => "*", _ => "+" };

    private static string OperatorText(AssignmentOperator op) => op switch { AssignmentOperator.Assign => "=", AssignmentOperator.Append => "+=", _ => "?=" };

    private static string Quote(string value) => "'" + value.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
}
