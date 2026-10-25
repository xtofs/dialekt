namespace Dialekt.Green;

/// <summary>
/// Scalar tag that distinguishes every kind of green node. The green layer itself is untyped: a node is only
/// its kind, its width and its positional child slots.
/// </summary>
public enum SyntaxKind : ushort
{
    None = 0,

    // ---- structure (zero-width placeholders and child collections)
    /// <summary>An optional part that is not there. One shared zero-width node.</summary>
    Absent,
    /// <summary>A required part the parser expected but did not find (flagged <see cref="GreenNodeFlags.IsMissing"/>).</summary>
    Missing,
    /// <summary>Zero or more consecutive children of the same slot.</summary>
    List,
    /// <summary>Items alternating with separator tokens (<c>a | b | c</c>); never starts or ends with a separator.</summary>
    SeparatedList,

    // ---- trivia (leaf nodes, attached to the following token)
    WhitespaceTrivia,
    SingleLineCommentTrivia,
    MultiLineCommentTrivia,

    // ---- tokens (children are the token's leading trivia; the token text is the rest of the width)
    IdentifierToken,
    StringLiteralToken,
    NumberToken,
    RegexLiteralToken,
    ErrorToken,
    EndOfFileToken,

    ColonToken,
    BarToken,
    GreaterThanToken,
    EqualsToken,
    LessThanToken,
    AmpersandToken,
    DotToken,
    DotDotToken,
    CommaToken,
    EqualsGreaterThanToken,
    PlusToken,
    StarToken,
    QuestionLessThanEqualsToken,
    QuestionLessThanBangToken,
    QuestionToken,
    QuestionBangToken,
    MinusGreaterThanToken,
    OpenParenToken,
    CloseParenToken,
    OpenBraceToken,
    CloseBraceToken,
    OpenBracketToken,
    CloseBracketToken,
    SemicolonToken,
    BangToken,
    AtToken,
    PlusEqualsToken,
    QuestionEqualsToken,

    // ---- file level
    Grammar,
    GrammarDeclaration,
    Import,
    Error,

    // ---- types
    Interface,
    SuperType,
    Attribute,
    Optional,
    TypeAlias,
    UnionType,
    ArrayType,
    ReferenceType,
    Multi,
    SimpleType,
    StringType,
    ParenthesizedType,
    StringLiteral,
    NumberLiteral,
    BooleanLiteral,
    ArrayLiteral,

    // ---- rules
    ParserRule,
    InfixRule,
    TerminalRule,
    Modifier,
    ParameterList,
    Parameter,
    ReturnType,
    InferredType,
    InfixRuleOperators,
    InfixRuleOperatorList,
    Associativity,

    // ---- parser rule bodies
    Alternatives,
    ConditionalBranch,
    Guard,
    UnorderedGroup,
    Group,
    Cardinality,
    Action,
    Feature,
    Operator,
    Assignment,
    Predicate,
    PredicatedKeyword,
    PredicatedRuleCall,
    PredicatedGroup,
    CrossReference,
    Separator,
    ParenthesizedElement,
    EndOfFile,
    Keyword,
    RuleCall,
    ArgumentList,
    NamedArgument,
    Disjunction,
    Conjunction,
    Negation,
    ParameterReference,
    ParenthesizedCondition,

    // ---- terminal rule bodies
    TerminalAlternatives,
    TerminalGroup,
    TerminalCardinality,
    CharacterRange,
    TerminalRuleCall,
    RegexToken,
    Wildcard,
    NegatedToken,
    UntilToken,
    Lookahead,
    ParenthesizedTerminal,
}
