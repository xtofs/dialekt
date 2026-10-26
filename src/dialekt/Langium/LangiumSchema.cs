namespace Dialekt.Langium;

using Dialekt.Green;
using C = SyntaxCategory;

/// <summary>Sets of node or token kinds that a slot of the Langium language can accept (the <see cref="SlotSpec.Category"/> numbers).</summary>
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

/// <summary>
/// The shape of every node kind: an ordered, fixed list of slots. The parser slots the children of each node it builds
/// against this table, so slot <i>i</i> of a kind always means the same thing; slots are read by name with <c>SyntaxNode.GetSlot</c>.
/// </summary>
internal static class LangiumSchema
{
    private static readonly SlotSpec[]?[] Table = Build();

    /// <summary>The slots of <paramref name="kind"/>; empty for tokens, trivia, placeholders, lists and error nodes.</summary>
    public static ReadOnlySpan<SlotSpec> GetSlots(SyntaxKind kind) => (int)kind < Table.Length ? Table[(int)kind] : default;

    /// <summary>All node kinds that have slots, with their slots.</summary>
    public static IEnumerable<SyntaxKind> KindsWithSlots()
    {
        for (var i = 0; i < Table.Length; i++)
        {
            if (Table[i] is not null)
            {
                yield return (SyntaxKind)i;
            }
        }
    }

    public static bool IsInCategory(int category, SyntaxKind kind) => (SyntaxCategory)category switch
    {
        C.TypeDefinition => kind is LangiumKinds.UnionType or LangiumKinds.ArrayType or LangiumKinds.ReferenceType or LangiumKinds.SimpleType or LangiumKinds.StringType or LangiumKinds.ParenthesizedType,
        C.ValueLiteral => kind is LangiumKinds.StringLiteral or LangiumKinds.NumberLiteral or LangiumKinds.BooleanLiteral or LangiumKinds.ArrayLiteral,
        C.AbstractElement => kind is LangiumKinds.Alternatives or LangiumKinds.ConditionalBranch or LangiumKinds.UnorderedGroup or LangiumKinds.Group or LangiumKinds.Cardinality or LangiumKinds.Action
            or LangiumKinds.Assignment or LangiumKinds.PredicatedKeyword or LangiumKinds.PredicatedRuleCall or LangiumKinds.PredicatedGroup or LangiumKinds.EndOfFile or LangiumKinds.Keyword or LangiumKinds.RuleCall
            or LangiumKinds.ParenthesizedElement or LangiumKinds.CrossReference,
        C.AssignableTerminal => kind is LangiumKinds.Keyword or LangiumKinds.RuleCall or LangiumKinds.ParenthesizedElement or LangiumKinds.CrossReference,
        C.CrossReferenceTerminal => kind is LangiumKinds.Keyword or LangiumKinds.RuleCall,
        C.Condition => kind is LangiumKinds.Disjunction or LangiumKinds.Conjunction or LangiumKinds.Negation or LangiumKinds.ParameterReference or LangiumKinds.BooleanLiteral or LangiumKinds.ParenthesizedCondition,
        C.TerminalElement => kind is LangiumKinds.TerminalAlternatives or LangiumKinds.TerminalGroup or LangiumKinds.TerminalCardinality or LangiumKinds.CharacterRange or LangiumKinds.TerminalRuleCall
            or LangiumKinds.RegexToken or LangiumKinds.Wildcard or LangiumKinds.NegatedToken or LangiumKinds.UntilToken or LangiumKinds.ParenthesizedTerminal,
        C.Declaration => kind is LangiumKinds.ParserRule or LangiumKinds.InfixRule or LangiumKinds.TerminalRule or LangiumKinds.Interface or LangiumKinds.TypeAlias or LangiumKinds.Import or SyntaxKind.Error,
        C.AttributeMember => kind is LangiumKinds.Attribute or SyntaxKind.Error,
        C.AssignmentOperator => kind is LangiumKinds.EqualsToken or LangiumKinds.PlusEqualsToken or LangiumKinds.QuestionEqualsToken,
        C.CardinalityOperator => kind is LangiumKinds.QuestionToken or LangiumKinds.StarToken or LangiumKinds.PlusToken,
        C.PredicateOperator => kind is LangiumKinds.EqualsGreaterThanToken or LangiumKinds.MinusGreaterThanToken,
        C.LookaheadOperator => kind is LangiumKinds.QuestionEqualsToken or LangiumKinds.QuestionBangToken or LangiumKinds.QuestionLessThanEqualsToken or LangiumKinds.QuestionLessThanBangToken,
        C.SeparatorOperator => kind is LangiumKinds.BarToken or LangiumKinds.ColonToken,
        _ => false,
    };

    private static SlotSpec Req(string name, SyntaxKind kind) => new(name, SlotShape.Required, kind);
    private static SlotSpec Req(string name, SyntaxCategory category) => new(name, SlotShape.Required, SyntaxKind.None, (int)category);
    private static SlotSpec Opt(string name, SyntaxKind kind) => new(name, SlotShape.Optional, kind);
    private static SlotSpec Opt(string name, SyntaxCategory category) => new(name, SlotShape.Optional, SyntaxKind.None, (int)category);
    private static SlotSpec Many(string name, SyntaxKind kind) => new(name, SlotShape.List, kind);
    private static SlotSpec Many(string name, SyntaxCategory category) => new(name, SlotShape.List, SyntaxKind.None, (int)category);
    private static SlotSpec Sep(string name, SyntaxKind item, SyntaxKind separator) => new(name, SlotShape.SeparatedList, item, 0, separator);
    private static SlotSpec Sep(string name, SyntaxCategory item, SyntaxKind separator) => new(name, SlotShape.SeparatedList, SyntaxKind.None, (int)item, separator);

    private static SlotSpec[]?[] Build()
    {
        var t = new SlotSpec[]?[(int)LangiumKinds.ParenthesizedTerminal + 1];
        const SyntaxKind Ident = LangiumKinds.IdentifierToken;

        // ---- file level
        t[(int)LangiumKinds.Grammar] = [Opt("Declaration", LangiumKinds.GrammarDeclaration), Many("Members", C.Declaration), Req("EndOfFile", SyntaxKind.EndOfFileToken)];
        t[(int)LangiumKinds.GrammarDeclaration] = [Req("GrammarKeyword", Ident), Req("Name", Ident)];
        t[(int)LangiumKinds.Import] = [Req("ImportKeyword", Ident), Req("Path", LangiumKinds.StringLiteralToken), Opt("Semicolon", LangiumKinds.SemicolonToken)];

        // ---- types
        t[(int)LangiumKinds.Interface] =
        [
            Req("InterfaceKeyword", Ident), Req("Name", Ident), Opt("ExtendsKeyword", Ident), Sep("SuperTypes", LangiumKinds.SuperType, LangiumKinds.CommaToken),
            Req("OpenBrace", LangiumKinds.OpenBraceToken), Many("Attributes", C.AttributeMember), Req("CloseBrace", LangiumKinds.CloseBraceToken), Opt("Semicolon", LangiumKinds.SemicolonToken),
        ];
        t[(int)LangiumKinds.SuperType] = [Req("Name", Ident)];
        t[(int)LangiumKinds.Attribute] =
        [
            Req("Name", Ident), Opt("Optional", LangiumKinds.Optional), Req("Colon", LangiumKinds.ColonToken), Req("Type", C.TypeDefinition),
            Opt("EqualsSign", LangiumKinds.EqualsToken), Opt("DefaultValue", C.ValueLiteral), Opt("Semicolon", LangiumKinds.SemicolonToken),
        ];
        t[(int)LangiumKinds.Optional] = [Req("QuestionMark", LangiumKinds.QuestionToken)];
        t[(int)LangiumKinds.TypeAlias] = [Req("TypeKeyword", Ident), Req("Name", Ident), Req("EqualsSign", LangiumKinds.EqualsToken), Req("Type", C.TypeDefinition), Opt("Semicolon", LangiumKinds.SemicolonToken)];
        t[(int)LangiumKinds.UnionType] = [Sep("Types", C.TypeDefinition, LangiumKinds.BarToken)];
        t[(int)LangiumKinds.ArrayType] = [Req("ElementType", C.TypeDefinition), Req("OpenBracket", LangiumKinds.OpenBracketToken), Req("CloseBracket", LangiumKinds.CloseBracketToken)];
        t[(int)LangiumKinds.ReferenceType] = [Req("At", LangiumKinds.AtToken), Req("Type", C.TypeDefinition), Opt("Multi", LangiumKinds.Multi)];
        t[(int)LangiumKinds.Multi] = [Req("Plus", LangiumKinds.PlusToken)];
        t[(int)LangiumKinds.SimpleType] = [Req("Name", Ident)];
        t[(int)LangiumKinds.StringType] = [Req("Value", LangiumKinds.StringLiteralToken)];
        t[(int)LangiumKinds.ParenthesizedType] = [Req("OpenParen", LangiumKinds.OpenParenToken), Req("Type", C.TypeDefinition), Req("CloseParen", LangiumKinds.CloseParenToken)];
        t[(int)LangiumKinds.StringLiteral] = [Req("Value", LangiumKinds.StringLiteralToken)];
        t[(int)LangiumKinds.NumberLiteral] = [Req("Value", LangiumKinds.NumberToken)];
        t[(int)LangiumKinds.BooleanLiteral] = [Req("Value", Ident)];
        t[(int)LangiumKinds.ArrayLiteral] = [Req("OpenBracket", LangiumKinds.OpenBracketToken), Sep("Elements", C.ValueLiteral, LangiumKinds.CommaToken), Req("CloseBracket", LangiumKinds.CloseBracketToken)];

        // ---- rules
        t[(int)LangiumKinds.ParserRule] =
        [
            Opt("Modifier", LangiumKinds.Modifier), Req("Name", Ident), Opt("Parameters", LangiumKinds.ParameterList), Opt("ReturnType", LangiumKinds.ReturnType),
            Opt("InferredType", LangiumKinds.InferredType), Req("Colon", LangiumKinds.ColonToken), Req("Definition", C.AbstractElement), Req("Semicolon", LangiumKinds.SemicolonToken),
        ];
        t[(int)LangiumKinds.InfixRule] =
        [
            Req("InfixKeyword", Ident), Req("Name", Ident), Opt("Parameters", LangiumKinds.ParameterList), Req("OnKeyword", Ident), Req("Call", LangiumKinds.RuleCall),
            Opt("ReturnType", LangiumKinds.ReturnType), Opt("InferredType", LangiumKinds.InferredType), Req("Colon", LangiumKinds.ColonToken), Req("Operators", LangiumKinds.InfixRuleOperators), Req("Semicolon", LangiumKinds.SemicolonToken),
        ];
        t[(int)LangiumKinds.TerminalRule] =
        [
            Opt("HiddenModifier", LangiumKinds.Modifier), Req("TerminalKeyword", Ident), Opt("FragmentModifier", LangiumKinds.Modifier), Req("Name", Ident),
            Opt("ReturnType", LangiumKinds.ReturnType), Req("Colon", LangiumKinds.ColonToken), Req("Definition", C.TerminalElement), Req("Semicolon", LangiumKinds.SemicolonToken),
        ];
        t[(int)LangiumKinds.Modifier] = [Req("Keyword", Ident)];
        t[(int)LangiumKinds.ParameterList] = [Req("LessThan", LangiumKinds.LessThanToken), Sep("Parameters", LangiumKinds.Parameter, LangiumKinds.CommaToken), Req("GreaterThan", LangiumKinds.GreaterThanToken)];
        t[(int)LangiumKinds.Parameter] = [Req("Name", Ident)];
        t[(int)LangiumKinds.ReturnType] = [Req("ReturnsKeyword", Ident), Req("Name", Ident)];
        t[(int)LangiumKinds.InferredType] = [Req("Keyword", Ident), Req("Name", Ident)];
        t[(int)LangiumKinds.InfixRuleOperators] = [Sep("Lists", LangiumKinds.InfixRuleOperatorList, LangiumKinds.GreaterThanToken)];
        t[(int)LangiumKinds.InfixRuleOperatorList] = [Opt("Associativity", LangiumKinds.Associativity), Sep("Operators", LangiumKinds.Keyword, LangiumKinds.BarToken)];
        t[(int)LangiumKinds.Associativity] = [Req("Direction", Ident), Req("AssocKeyword", Ident)];

        // ---- parser rule bodies
        t[(int)LangiumKinds.Alternatives] = [Sep("Items", C.AbstractElement, LangiumKinds.BarToken)];
        t[(int)LangiumKinds.ConditionalBranch] = [Req("Guard", LangiumKinds.Guard), Many("Elements", C.AbstractElement)];
        t[(int)LangiumKinds.Guard] = [Req("LessThan", LangiumKinds.LessThanToken), Req("Condition", C.Condition), Req("GreaterThan", LangiumKinds.GreaterThanToken)];
        t[(int)LangiumKinds.UnorderedGroup] = [Sep("Items", C.AbstractElement, LangiumKinds.AmpersandToken)];
        t[(int)LangiumKinds.Group] = [Many("Elements", C.AbstractElement)];
        t[(int)LangiumKinds.Cardinality] = [Req("Element", C.AbstractElement), Req("Operator", C.CardinalityOperator)];
        t[(int)LangiumKinds.Action] =
        [
            Req("OpenBrace", LangiumKinds.OpenBraceToken), Opt("Type", Ident), Opt("InferredType", LangiumKinds.InferredType), Opt("Dot", LangiumKinds.DotToken),
            Opt("Feature", LangiumKinds.Feature), Opt("Operator", LangiumKinds.Operator), Opt("CurrentKeyword", Ident), Req("CloseBrace", LangiumKinds.CloseBraceToken),
        ];
        t[(int)LangiumKinds.Feature] = [Req("Name", Ident)];
        t[(int)LangiumKinds.Operator] = [Req("Token", C.AssignmentOperator)];
        t[(int)LangiumKinds.Assignment] = [Opt("Predicate", LangiumKinds.Predicate), Req("Feature", LangiumKinds.Feature), Req("Operator", LangiumKinds.Operator), Req("Terminal", C.AssignableTerminal)];
        t[(int)LangiumKinds.Predicate] = [Req("Token", C.PredicateOperator)];
        t[(int)LangiumKinds.PredicatedKeyword] = [Req("Predicate", LangiumKinds.Predicate), Req("Keyword", LangiumKinds.Keyword)];
        t[(int)LangiumKinds.PredicatedRuleCall] = [Req("Predicate", LangiumKinds.Predicate), Req("Call", LangiumKinds.RuleCall)];
        t[(int)LangiumKinds.PredicatedGroup] = [Req("Predicate", LangiumKinds.Predicate), Req("OpenParen", LangiumKinds.OpenParenToken), Req("Content", C.AbstractElement), Req("CloseParen", LangiumKinds.CloseParenToken)];
        t[(int)LangiumKinds.CrossReference] =
        [
            Req("OpenBracket", LangiumKinds.OpenBracketToken), Opt("Multi", LangiumKinds.Multi), Req("Type", Ident), Opt("Separator", LangiumKinds.Separator),
            Opt("Terminal", C.CrossReferenceTerminal), Req("CloseBracket", LangiumKinds.CloseBracketToken),
        ];
        t[(int)LangiumKinds.Separator] = [Req("Token", C.SeparatorOperator)];
        t[(int)LangiumKinds.EndOfFile] = [Req("Keyword", Ident)];
        t[(int)LangiumKinds.Keyword] = [Req("Value", LangiumKinds.StringLiteralToken)];
        t[(int)LangiumKinds.RuleCall] = [Req("Name", Ident), Opt("Arguments", LangiumKinds.ArgumentList)];
        t[(int)LangiumKinds.ArgumentList] = [Req("LessThan", LangiumKinds.LessThanToken), Sep("Arguments", LangiumKinds.NamedArgument, LangiumKinds.CommaToken), Req("GreaterThan", LangiumKinds.GreaterThanToken)];
        t[(int)LangiumKinds.NamedArgument] = [Opt("Parameter", Ident), Opt("EqualsSign", LangiumKinds.EqualsToken), Req("Value", C.Condition)];
        t[(int)LangiumKinds.ParenthesizedElement] = [Req("OpenParen", LangiumKinds.OpenParenToken), Req("Content", C.AbstractElement), Req("CloseParen", LangiumKinds.CloseParenToken)];

        // ---- conditions
        t[(int)LangiumKinds.Disjunction] = [Req("Left", C.Condition), Req("Bar", LangiumKinds.BarToken), Req("Right", C.Condition)];
        t[(int)LangiumKinds.Conjunction] = [Req("Left", C.Condition), Req("Ampersand", LangiumKinds.AmpersandToken), Req("Right", C.Condition)];
        t[(int)LangiumKinds.Negation] = [Req("Bang", LangiumKinds.BangToken), Req("Operand", C.Condition)];
        t[(int)LangiumKinds.ParameterReference] = [Req("Name", Ident)];
        t[(int)LangiumKinds.ParenthesizedCondition] = [Req("OpenParen", LangiumKinds.OpenParenToken), Req("Condition", C.Condition), Req("CloseParen", LangiumKinds.CloseParenToken)];

        // ---- terminal rule bodies
        t[(int)LangiumKinds.TerminalAlternatives] = [Sep("Items", C.TerminalElement, LangiumKinds.BarToken)];
        t[(int)LangiumKinds.TerminalGroup] = [Many("Elements", C.TerminalElement)];
        t[(int)LangiumKinds.TerminalCardinality] = [Req("Element", C.TerminalElement), Req("Operator", C.CardinalityOperator)];
        t[(int)LangiumKinds.CharacterRange] = [Req("Left", LangiumKinds.Keyword), Opt("DotDot", LangiumKinds.DotDotToken), Opt("Right", LangiumKinds.Keyword)];
        t[(int)LangiumKinds.TerminalRuleCall] = [Req("Name", Ident)];
        t[(int)LangiumKinds.RegexToken] = [Req("Regex", LangiumKinds.RegexLiteralToken)];
        t[(int)LangiumKinds.Wildcard] = [Req("Dot", LangiumKinds.DotToken)];
        t[(int)LangiumKinds.NegatedToken] = [Req("Bang", LangiumKinds.BangToken), Req("Element", C.TerminalElement)];
        t[(int)LangiumKinds.UntilToken] = [Req("Arrow", LangiumKinds.MinusGreaterThanToken), Req("Element", C.TerminalElement)];
        t[(int)LangiumKinds.Lookahead] = [Req("Token", C.LookaheadOperator)];
        t[(int)LangiumKinds.ParenthesizedTerminal] = [Req("OpenParen", LangiumKinds.OpenParenToken), Opt("Lookahead", LangiumKinds.Lookahead), Req("Content", C.TerminalElement), Req("CloseParen", LangiumKinds.CloseParenToken)];

        return t;
    }
}
