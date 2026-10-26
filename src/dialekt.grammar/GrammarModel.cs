namespace Dialekt.Grammar;

/// <summary>
/// The meaning of a Langium grammar file, without positions: records with value equality, so two models are equal when the
/// grammars say the same thing, however they are laid out. Names are plain strings; <see cref="RuleKind"/> on a reference says
/// what it resolved to (the target is found with <see cref="GrammarModel.FindRule"/>).
/// </summary>
public sealed record GrammarModel(
    string? Name,
    EquatableArray<GrammarRule> Rules,
    EquatableArray<TypeDeclaration> Types,
    EquatableArray<string> Imports)
{
    public GrammarRule? FindRule(string name) => Rules.FirstOrDefault(r => r.Name == name);

    /// <summary>The rule marked <c>entry</c>, or null if there is none (or more than one: see the diagnostics).</summary>
    public ParserRule? EntryRule => Rules.OfType<ParserRule>().SingleOrDefaultIfMany(r => r.IsEntry);

    public IEnumerable<TerminalRule> HiddenTerminals => Rules.OfType<TerminalRule>().Where(r => r.IsHidden);

    /// <summary>The distinct keywords used in parser rules, in order of first appearance.</summary>
    public IEnumerable<string> Keywords => Rules.OfType<ParserRule>().SelectMany(r => ModelWalker.Keywords(r.Body)).Distinct();
}

public enum RuleKind
{
    /// <summary>No rule of that name.</summary>
    Unresolved,
    ParserRule,
    ParserFragment,
    TerminalRule,
    TerminalFragment,
    InfixRule,
}

// ---------------------------------------------------------------- rules

public abstract record GrammarRule(string Name);

/// <param name="ReturnType">From <c>returns T</c>; a primitive type (<c>string</c>, <c>number</c>, ...) makes it a data type rule.</param>
/// <param name="InferredType">From <c>infers T</c>.</param>
public sealed record ParserRule(
    string Name,
    bool IsEntry,
    bool IsFragment,
    EquatableArray<string> Parameters,
    string? ReturnType,
    string? InferredType,
    GrammarElement Body) : GrammarRule(Name);

public sealed record TerminalRule(string Name, bool IsHidden, bool IsFragment, string? ReturnType, TerminalElement Definition) : GrammarRule(Name);

/// <summary>An infix rule (operator precedence). Only its name is modelled; it is not supported yet.</summary>
public sealed record InfixRule(string Name) : GrammarRule(Name);

// ---------------------------------------------------------------- types (declared interfaces and aliases)

public abstract record TypeDeclaration(string Name);

public sealed record InterfaceDeclaration(string Name, EquatableArray<string> SuperTypes, EquatableArray<AttributeDeclaration> Attributes) : TypeDeclaration(Name);

public sealed record AttributeDeclaration(string Name, bool IsOptional, string Type);

public sealed record TypeAlias(string Name, string Definition) : TypeDeclaration(Name);

// ---------------------------------------------------------------- parser rule bodies

public abstract record GrammarElement;

public enum Cardinality
{
    /// <summary>?</summary>
    Optional,
    /// <summary>*</summary>
    ZeroOrMore,
    /// <summary>+</summary>
    OneOrMore,
}

public enum AssignmentOperator
{
    /// <summary>=</summary>
    Assign,
    /// <summary>+=</summary>
    Append,
    /// <summary>?=</summary>
    BooleanAssign,
}

public sealed record AlternativesElement(EquatableArray<GrammarElement> Branches) : GrammarElement;

public sealed record GroupElement(EquatableArray<GrammarElement> Items) : GrammarElement;

/// <summary>a &amp; b &amp; c. Not supported yet.</summary>
public sealed record UnorderedGroupElement(EquatableArray<GrammarElement> Items) : GrammarElement;

public sealed record RepetitionElement(GrammarElement Inner, Cardinality Cardinality) : GrammarElement;

public sealed record AssignmentElement(string Feature, AssignmentOperator Operator, GrammarElement Terminal) : GrammarElement;

/// <param name="Value">The keyword without quotes and with escapes resolved.</param>
public sealed record KeywordElement(string Value) : GrammarElement;

/// <param name="Target">What the name resolved to.</param>
/// <param name="Arguments">Arguments for rule parameters (<c>Rule&lt;arg&gt;</c>); not supported yet.</param>
public sealed record RuleCallElement(string RuleName, RuleKind Target, EquatableArray<string> Arguments) : GrammarElement;

/// <param name="TypeName">The referenced type, e.g. <c>Person</c> in <c>[Person:ID]</c>.</param>
/// <param name="TypeKind">What <paramref name="TypeName"/> resolved to (a rule, or Unresolved for an interface or type).</param>
/// <param name="Terminal">The keyword or rule that is parsed to write the reference; null means the default (<c>ID</c>).</param>
public sealed record CrossReferenceElement(string TypeName, RuleKind TypeKind, bool IsMulti, GrammarElement? Terminal) : GrammarElement;

public sealed record EndOfFileElement : GrammarElement;

/// <summary><c>{Type}</c>, <c>{infer Type}</c> or <c>{Type.feature=current}</c>. Not supported yet.</summary>
public sealed record ActionElement(string? TypeName, string? InferredType, string? Feature) : GrammarElement;

/// <summary>An element marked with <c>=&gt;</c> or <c>-&gt;</c>. Not supported yet; the inner element is kept.</summary>
public sealed record PredicatedElement(GrammarElement Inner) : GrammarElement;

/// <summary>A guarded alternative (<c>&lt;cond&gt; ...</c>). Not supported yet; the guard is dropped.</summary>
public sealed record GuardedElement(EquatableArray<GrammarElement> Items) : GrammarElement;

// ---------------------------------------------------------------- terminal rule bodies

public abstract record TerminalElement;

public sealed record TerminalAlternatives(EquatableArray<TerminalElement> Branches) : TerminalElement;

public sealed record TerminalGroup(EquatableArray<TerminalElement> Items) : TerminalElement;

public sealed record TerminalRepetition(TerminalElement Inner, Cardinality Cardinality) : TerminalElement;

/// <summary><c>'a'</c> or <c>'a'..'z'</c>.</summary>
public sealed record CharacterRange(string Left, string? Right) : TerminalElement;

public sealed record TerminalRuleCall(string RuleName, RuleKind Target) : TerminalElement;

/// <param name="Pattern">The regular expression between the slashes.</param>
/// <param name="Flags">The letters after the closing slash.</param>
public sealed record RegexTerminal(string Pattern, string Flags) : TerminalElement;

public sealed record WildcardTerminal : TerminalElement;

public sealed record NegatedTerminal(TerminalElement Inner) : TerminalElement;

public sealed record UntilTerminal(TerminalElement Inner) : TerminalElement;

/// <param name="Operator">One of <c>?=</c>, <c>?!</c>, <c>?&lt;=</c>, <c>?&lt;!</c>.</param>
public sealed record LookaheadTerminal(string Operator, TerminalElement Inner) : TerminalElement;
