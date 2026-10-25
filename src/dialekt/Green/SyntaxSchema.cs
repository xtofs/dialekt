namespace Dialekt.Green;

using C = SyntaxCategory;
using K = SyntaxKind;

public enum SlotShape : byte
{
    /// <summary>Exactly one child; a <see cref="SyntaxKind.Missing"/> placeholder if the parser did not find it.</summary>
    Required,
    /// <summary>At most one child; <see cref="SyntaxKind.Absent"/> if there is none.</summary>
    Optional,
    /// <summary>A <see cref="SyntaxKind.List"/> node with any number of children.</summary>
    List,
    /// <summary>A <see cref="SyntaxKind.SeparatedList"/> node: items alternating with separator tokens.</summary>
    SeparatedList,
}

/// <summary>Sets of node or token kinds that a slot can accept.</summary>
public enum SyntaxCategory : byte
{
    None,
    TypeDefinition,
    ValueLiteral,
    /// <summary>Everything that can be an element of a parser rule body.</summary>
    AbstractElement,
    /// <summary>The right-hand side of an assignment.</summary>
    AssignableTerminal,
    /// <summary>What may follow the separator in a cross reference.</summary>
    CrossReferenceTerminal,
    Condition,
    TerminalElement,
    /// <summary>A member of the grammar file (an Error node stands for a declaration that could not be parsed).</summary>
    Declaration,
    /// <summary>A member of an interface body.</summary>
    AttributeMember,
    AssignmentOperator,
    CardinalityOperator,
    PredicateOperator,
    LookaheadOperator,
    SeparatorOperator,
}

/// <param name="Name">Name of the slot, as used by <c>SyntaxNode.GetSlot</c>.</param>
/// <param name="Kind">The accepted kind when <paramref name="Category"/> is <see cref="SyntaxCategory.None"/>.</param>
/// <param name="Separator">For separated lists: the separator token kind.</param>
public readonly record struct SlotSpec(string Name, SlotShape Shape, SyntaxKind Kind, SyntaxCategory Category = SyntaxCategory.None, SyntaxKind Separator = SyntaxKind.None)
{
    public bool Accepts(SyntaxKind kind) => Category == SyntaxCategory.None ? kind == Kind : SyntaxSchema.IsInCategory(Category, kind);
}

/// <summary>
/// The shape of every node kind: an ordered, fixed list of slots. The parser slots the children of each node it builds
/// against this table, so slot <i>i</i> of a kind always means the same thing; slots are read by name with <c>SyntaxNode.GetSlot</c>.
/// </summary>
public static class SyntaxSchema
{
    private static readonly SlotSpec[]?[] Table = Build();

    /// <summary>The slots of <paramref name="kind"/>; empty for tokens, trivia, placeholders, lists and error nodes.</summary>
    public static ReadOnlySpan<SlotSpec> GetSlots(SyntaxKind kind) => Table[(int)kind];

    /// <summary>The index of the slot called <paramref name="slotName"/> in <paramref name="kind"/>, or -1.</summary>
    public static int IndexOf(SyntaxKind kind, string slotName)
    {
        var slots = GetSlots(kind);
        for (var i = 0; i < slots.Length; i++)
        {
            if (slots[i].Name == slotName) return i;
        }
        return -1;
    }

    /// <summary>All node kinds that have slots, with their slots.</summary>
    public static IEnumerable<(SyntaxKind Kind, SlotSpec[] Slots)> All()
    {
        for (var i = 0; i < Table.Length; i++)
        {
            if (Table[i] is { } slots)
            {
                yield return ((SyntaxKind)i, slots);
            }
        }
    }

    public static bool IsInCategory(SyntaxCategory category, SyntaxKind kind) => category switch
    {
        C.TypeDefinition => kind is K.UnionType or K.ArrayType or K.ReferenceType or K.SimpleType or K.StringType or K.ParenthesizedType,
        C.ValueLiteral => kind is K.StringLiteral or K.NumberLiteral or K.BooleanLiteral or K.ArrayLiteral,
        C.AbstractElement => kind is K.Alternatives or K.ConditionalBranch or K.UnorderedGroup or K.Group or K.Cardinality or K.Action
            or K.Assignment or K.PredicatedKeyword or K.PredicatedRuleCall or K.PredicatedGroup or K.EndOfFile or K.Keyword or K.RuleCall
            or K.ParenthesizedElement or K.CrossReference,
        C.AssignableTerminal => kind is K.Keyword or K.RuleCall or K.ParenthesizedElement or K.CrossReference,
        C.CrossReferenceTerminal => kind is K.Keyword or K.RuleCall,
        C.Condition => kind is K.Disjunction or K.Conjunction or K.Negation or K.ParameterReference or K.BooleanLiteral or K.ParenthesizedCondition,
        C.TerminalElement => kind is K.TerminalAlternatives or K.TerminalGroup or K.TerminalCardinality or K.CharacterRange or K.TerminalRuleCall
            or K.RegexToken or K.Wildcard or K.NegatedToken or K.UntilToken or K.ParenthesizedTerminal,
        C.Declaration => kind is K.ParserRule or K.InfixRule or K.TerminalRule or K.Interface or K.TypeAlias or K.Import or K.Error,
        C.AttributeMember => kind is K.Attribute or K.Error,
        C.AssignmentOperator => kind is K.EqualsToken or K.PlusEqualsToken or K.QuestionEqualsToken,
        C.CardinalityOperator => kind is K.QuestionToken or K.StarToken or K.PlusToken,
        C.PredicateOperator => kind is K.EqualsGreaterThanToken or K.MinusGreaterThanToken,
        C.LookaheadOperator => kind is K.QuestionEqualsToken or K.QuestionBangToken or K.QuestionLessThanEqualsToken or K.QuestionLessThanBangToken,
        C.SeparatorOperator => kind is K.BarToken or K.ColonToken,
        _ => false,
    };

    private static SlotSpec Req(string name, SyntaxKind kind) => new(name, SlotShape.Required, kind);
    private static SlotSpec Req(string name, SyntaxCategory category) => new(name, SlotShape.Required, K.None, category);
    private static SlotSpec Opt(string name, SyntaxKind kind) => new(name, SlotShape.Optional, kind);
    private static SlotSpec Opt(string name, SyntaxCategory category) => new(name, SlotShape.Optional, K.None, category);
    private static SlotSpec Many(string name, SyntaxKind kind) => new(name, SlotShape.List, kind);
    private static SlotSpec Many(string name, SyntaxCategory category) => new(name, SlotShape.List, K.None, category);
    private static SlotSpec Sep(string name, SyntaxKind item, SyntaxKind separator) => new(name, SlotShape.SeparatedList, item, C.None, separator);
    private static SlotSpec Sep(string name, SyntaxCategory item, SyntaxKind separator) => new(name, SlotShape.SeparatedList, K.None, item, separator);

    private static SlotSpec[]?[] Build()
    {
        var t = new SlotSpec[]?[(int)K.ParenthesizedTerminal + 1];
        const K Ident = K.IdentifierToken;

        // ---- file level
        t[(int)K.Grammar] = [Opt("Declaration", K.GrammarDeclaration), Many("Members", C.Declaration), Req("EndOfFile", K.EndOfFileToken)];
        t[(int)K.GrammarDeclaration] = [Req("GrammarKeyword", Ident), Req("Name", Ident)];
        t[(int)K.Import] = [Req("ImportKeyword", Ident), Req("Path", K.StringLiteralToken), Opt("Semicolon", K.SemicolonToken)];

        // ---- types
        t[(int)K.Interface] =
        [
            Req("InterfaceKeyword", Ident), Req("Name", Ident), Opt("ExtendsKeyword", Ident), Sep("SuperTypes", K.SuperType, K.CommaToken),
            Req("OpenBrace", K.OpenBraceToken), Many("Attributes", C.AttributeMember), Req("CloseBrace", K.CloseBraceToken), Opt("Semicolon", K.SemicolonToken),
        ];
        t[(int)K.SuperType] = [Req("Name", Ident)];
        t[(int)K.Attribute] =
        [
            Req("Name", Ident), Opt("Optional", K.Optional), Req("Colon", K.ColonToken), Req("Type", C.TypeDefinition),
            Opt("EqualsSign", K.EqualsToken), Opt("DefaultValue", C.ValueLiteral), Opt("Semicolon", K.SemicolonToken),
        ];
        t[(int)K.Optional] = [Req("QuestionMark", K.QuestionToken)];
        t[(int)K.TypeAlias] = [Req("TypeKeyword", Ident), Req("Name", Ident), Req("EqualsSign", K.EqualsToken), Req("Type", C.TypeDefinition), Opt("Semicolon", K.SemicolonToken)];
        t[(int)K.UnionType] = [Sep("Types", C.TypeDefinition, K.BarToken)];
        t[(int)K.ArrayType] = [Req("ElementType", C.TypeDefinition), Req("OpenBracket", K.OpenBracketToken), Req("CloseBracket", K.CloseBracketToken)];
        t[(int)K.ReferenceType] = [Req("At", K.AtToken), Req("Type", C.TypeDefinition), Opt("Multi", K.Multi)];
        t[(int)K.Multi] = [Req("Plus", K.PlusToken)];
        t[(int)K.SimpleType] = [Req("Name", Ident)];
        t[(int)K.StringType] = [Req("Value", K.StringLiteralToken)];
        t[(int)K.ParenthesizedType] = [Req("OpenParen", K.OpenParenToken), Req("Type", C.TypeDefinition), Req("CloseParen", K.CloseParenToken)];
        t[(int)K.StringLiteral] = [Req("Value", K.StringLiteralToken)];
        t[(int)K.NumberLiteral] = [Req("Value", K.NumberToken)];
        t[(int)K.BooleanLiteral] = [Req("Value", Ident)];
        t[(int)K.ArrayLiteral] = [Req("OpenBracket", K.OpenBracketToken), Sep("Elements", C.ValueLiteral, K.CommaToken), Req("CloseBracket", K.CloseBracketToken)];

        // ---- rules
        t[(int)K.ParserRule] =
        [
            Opt("Modifier", K.Modifier), Req("Name", Ident), Opt("Parameters", K.ParameterList), Opt("ReturnType", K.ReturnType),
            Opt("InferredType", K.InferredType), Req("Colon", K.ColonToken), Req("Definition", C.AbstractElement), Req("Semicolon", K.SemicolonToken),
        ];
        t[(int)K.InfixRule] =
        [
            Req("InfixKeyword", Ident), Req("Name", Ident), Opt("Parameters", K.ParameterList), Req("OnKeyword", Ident), Req("Call", K.RuleCall),
            Opt("ReturnType", K.ReturnType), Opt("InferredType", K.InferredType), Req("Colon", K.ColonToken), Req("Operators", K.InfixRuleOperators), Req("Semicolon", K.SemicolonToken),
        ];
        t[(int)K.TerminalRule] =
        [
            Opt("HiddenModifier", K.Modifier), Req("TerminalKeyword", Ident), Opt("FragmentModifier", K.Modifier), Req("Name", Ident),
            Opt("ReturnType", K.ReturnType), Req("Colon", K.ColonToken), Req("Definition", C.TerminalElement), Req("Semicolon", K.SemicolonToken),
        ];
        t[(int)K.Modifier] = [Req("Keyword", Ident)];
        t[(int)K.ParameterList] = [Req("LessThan", K.LessThanToken), Sep("Parameters", K.Parameter, K.CommaToken), Req("GreaterThan", K.GreaterThanToken)];
        t[(int)K.Parameter] = [Req("Name", Ident)];
        t[(int)K.ReturnType] = [Req("ReturnsKeyword", Ident), Req("Name", Ident)];
        t[(int)K.InferredType] = [Req("Keyword", Ident), Req("Name", Ident)];
        t[(int)K.InfixRuleOperators] = [Sep("Lists", K.InfixRuleOperatorList, K.GreaterThanToken)];
        t[(int)K.InfixRuleOperatorList] = [Opt("Associativity", K.Associativity), Sep("Operators", K.Keyword, K.BarToken)];
        t[(int)K.Associativity] = [Req("Direction", Ident), Req("AssocKeyword", Ident)];

        // ---- parser rule bodies
        t[(int)K.Alternatives] = [Sep("Items", C.AbstractElement, K.BarToken)];
        t[(int)K.ConditionalBranch] = [Req("Guard", K.Guard), Many("Elements", C.AbstractElement)];
        t[(int)K.Guard] = [Req("LessThan", K.LessThanToken), Req("Condition", C.Condition), Req("GreaterThan", K.GreaterThanToken)];
        t[(int)K.UnorderedGroup] = [Sep("Items", C.AbstractElement, K.AmpersandToken)];
        t[(int)K.Group] = [Many("Elements", C.AbstractElement)];
        t[(int)K.Cardinality] = [Req("Element", C.AbstractElement), Req("Operator", C.CardinalityOperator)];
        t[(int)K.Action] =
        [
            Req("OpenBrace", K.OpenBraceToken), Opt("Type", Ident), Opt("InferredType", K.InferredType), Opt("Dot", K.DotToken),
            Opt("Feature", K.Feature), Opt("Operator", K.Operator), Opt("CurrentKeyword", Ident), Req("CloseBrace", K.CloseBraceToken),
        ];
        t[(int)K.Feature] = [Req("Name", Ident)];
        t[(int)K.Operator] = [Req("Token", C.AssignmentOperator)];
        t[(int)K.Assignment] = [Opt("Predicate", K.Predicate), Req("Feature", K.Feature), Req("Operator", K.Operator), Req("Terminal", C.AssignableTerminal)];
        t[(int)K.Predicate] = [Req("Token", C.PredicateOperator)];
        t[(int)K.PredicatedKeyword] = [Req("Predicate", K.Predicate), Req("Keyword", K.Keyword)];
        t[(int)K.PredicatedRuleCall] = [Req("Predicate", K.Predicate), Req("Call", K.RuleCall)];
        t[(int)K.PredicatedGroup] = [Req("Predicate", K.Predicate), Req("OpenParen", K.OpenParenToken), Req("Content", C.AbstractElement), Req("CloseParen", K.CloseParenToken)];
        t[(int)K.CrossReference] =
        [
            Req("OpenBracket", K.OpenBracketToken), Opt("Multi", K.Multi), Req("Type", Ident), Opt("Separator", K.Separator),
            Opt("Terminal", C.CrossReferenceTerminal), Req("CloseBracket", K.CloseBracketToken),
        ];
        t[(int)K.Separator] = [Req("Token", C.SeparatorOperator)];
        t[(int)K.EndOfFile] = [Req("Keyword", Ident)];
        t[(int)K.Keyword] = [Req("Value", K.StringLiteralToken)];
        t[(int)K.RuleCall] = [Req("Name", Ident), Opt("Arguments", K.ArgumentList)];
        t[(int)K.ArgumentList] = [Req("LessThan", K.LessThanToken), Sep("Arguments", K.NamedArgument, K.CommaToken), Req("GreaterThan", K.GreaterThanToken)];
        t[(int)K.NamedArgument] = [Opt("Parameter", Ident), Opt("EqualsSign", K.EqualsToken), Req("Value", C.Condition)];
        t[(int)K.ParenthesizedElement] = [Req("OpenParen", K.OpenParenToken), Req("Content", C.AbstractElement), Req("CloseParen", K.CloseParenToken)];

        // ---- conditions
        t[(int)K.Disjunction] = [Req("Left", C.Condition), Req("Bar", K.BarToken), Req("Right", C.Condition)];
        t[(int)K.Conjunction] = [Req("Left", C.Condition), Req("Ampersand", K.AmpersandToken), Req("Right", C.Condition)];
        t[(int)K.Negation] = [Req("Bang", K.BangToken), Req("Operand", C.Condition)];
        t[(int)K.ParameterReference] = [Req("Name", Ident)];
        t[(int)K.ParenthesizedCondition] = [Req("OpenParen", K.OpenParenToken), Req("Condition", C.Condition), Req("CloseParen", K.CloseParenToken)];

        // ---- terminal rule bodies
        t[(int)K.TerminalAlternatives] = [Sep("Items", C.TerminalElement, K.BarToken)];
        t[(int)K.TerminalGroup] = [Many("Elements", C.TerminalElement)];
        t[(int)K.TerminalCardinality] = [Req("Element", C.TerminalElement), Req("Operator", C.CardinalityOperator)];
        t[(int)K.CharacterRange] = [Req("Left", K.Keyword), Opt("DotDot", K.DotDotToken), Opt("Right", K.Keyword)];
        t[(int)K.TerminalRuleCall] = [Req("Name", Ident)];
        t[(int)K.RegexToken] = [Req("Regex", K.RegexLiteralToken)];
        t[(int)K.Wildcard] = [Req("Dot", K.DotToken)];
        t[(int)K.NegatedToken] = [Req("Bang", K.BangToken), Req("Element", C.TerminalElement)];
        t[(int)K.UntilToken] = [Req("Arrow", K.MinusGreaterThanToken), Req("Element", C.TerminalElement)];
        t[(int)K.Lookahead] = [Req("Token", C.LookaheadOperator)];
        t[(int)K.ParenthesizedTerminal] = [Req("OpenParen", K.OpenParenToken), Opt("Lookahead", K.Lookahead), Req("Content", C.TerminalElement), Req("CloseParen", K.CloseParenToken)];

        return t;
    }
}
