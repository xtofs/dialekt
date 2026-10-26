namespace Dialekt.Langium;

using Dialekt.Green;

/// <summary>
/// The syntax kinds of the Langium grammar language. They are constants of the runtime's <see cref="SyntaxKind"/> enum,
/// numbered from <see cref="SyntaxKind.FirstLanguageKind"/> so that they can be used in patterns and switches like enum members.
/// A new kind takes the next unused number (a test checks that the numbers are unique).
/// </summary>
public static class LangiumKinds
{
    // ---- trivia (leaf nodes, attached to the following token)
    public const SyntaxKind WhitespaceTrivia = (SyntaxKind)16;
    public const SyntaxKind SingleLineCommentTrivia = (SyntaxKind)17;
    public const SyntaxKind MultiLineCommentTrivia = (SyntaxKind)18;

    // ---- tokens (children are the token's leading trivia; the token text is the rest of the width)
    public const SyntaxKind IdentifierToken = (SyntaxKind)19;
    public const SyntaxKind StringLiteralToken = (SyntaxKind)20;
    public const SyntaxKind NumberToken = (SyntaxKind)21;
    public const SyntaxKind RegexLiteralToken = (SyntaxKind)22;

    public const SyntaxKind ColonToken = (SyntaxKind)23;
    public const SyntaxKind BarToken = (SyntaxKind)24;
    public const SyntaxKind GreaterThanToken = (SyntaxKind)25;
    public const SyntaxKind EqualsToken = (SyntaxKind)26;
    public const SyntaxKind LessThanToken = (SyntaxKind)27;
    public const SyntaxKind AmpersandToken = (SyntaxKind)28;
    public const SyntaxKind DotToken = (SyntaxKind)29;
    public const SyntaxKind DotDotToken = (SyntaxKind)30;
    public const SyntaxKind CommaToken = (SyntaxKind)31;
    public const SyntaxKind EqualsGreaterThanToken = (SyntaxKind)32;
    public const SyntaxKind PlusToken = (SyntaxKind)33;
    public const SyntaxKind StarToken = (SyntaxKind)34;
    public const SyntaxKind QuestionLessThanEqualsToken = (SyntaxKind)35;
    public const SyntaxKind QuestionLessThanBangToken = (SyntaxKind)36;
    public const SyntaxKind QuestionToken = (SyntaxKind)37;
    public const SyntaxKind QuestionBangToken = (SyntaxKind)38;
    public const SyntaxKind MinusGreaterThanToken = (SyntaxKind)39;
    public const SyntaxKind OpenParenToken = (SyntaxKind)40;
    public const SyntaxKind CloseParenToken = (SyntaxKind)41;
    public const SyntaxKind OpenBraceToken = (SyntaxKind)42;
    public const SyntaxKind CloseBraceToken = (SyntaxKind)43;
    public const SyntaxKind OpenBracketToken = (SyntaxKind)44;
    public const SyntaxKind CloseBracketToken = (SyntaxKind)45;
    public const SyntaxKind SemicolonToken = (SyntaxKind)46;
    public const SyntaxKind BangToken = (SyntaxKind)47;
    public const SyntaxKind AtToken = (SyntaxKind)48;
    public const SyntaxKind PlusEqualsToken = (SyntaxKind)49;
    public const SyntaxKind QuestionEqualsToken = (SyntaxKind)50;

    // ---- file level
    public const SyntaxKind Grammar = (SyntaxKind)51;
    public const SyntaxKind GrammarDeclaration = (SyntaxKind)52;
    public const SyntaxKind Import = (SyntaxKind)53;

    // ---- types
    public const SyntaxKind Interface = (SyntaxKind)54;
    public const SyntaxKind SuperType = (SyntaxKind)55;
    public const SyntaxKind Attribute = (SyntaxKind)56;
    public const SyntaxKind Optional = (SyntaxKind)57;
    public const SyntaxKind TypeAlias = (SyntaxKind)58;
    public const SyntaxKind UnionType = (SyntaxKind)59;
    public const SyntaxKind ArrayType = (SyntaxKind)60;
    public const SyntaxKind ReferenceType = (SyntaxKind)61;
    public const SyntaxKind Multi = (SyntaxKind)62;
    public const SyntaxKind SimpleType = (SyntaxKind)63;
    public const SyntaxKind StringType = (SyntaxKind)64;
    public const SyntaxKind ParenthesizedType = (SyntaxKind)65;
    public const SyntaxKind StringLiteral = (SyntaxKind)66;
    public const SyntaxKind NumberLiteral = (SyntaxKind)67;
    public const SyntaxKind BooleanLiteral = (SyntaxKind)68;
    public const SyntaxKind ArrayLiteral = (SyntaxKind)69;

    // ---- rules
    public const SyntaxKind ParserRule = (SyntaxKind)70;
    public const SyntaxKind InfixRule = (SyntaxKind)71;
    public const SyntaxKind TerminalRule = (SyntaxKind)72;
    public const SyntaxKind Modifier = (SyntaxKind)73;
    public const SyntaxKind ParameterList = (SyntaxKind)74;
    public const SyntaxKind Parameter = (SyntaxKind)75;
    public const SyntaxKind ReturnType = (SyntaxKind)76;
    public const SyntaxKind InferredType = (SyntaxKind)77;
    public const SyntaxKind InfixRuleOperators = (SyntaxKind)78;
    public const SyntaxKind InfixRuleOperatorList = (SyntaxKind)79;
    public const SyntaxKind Associativity = (SyntaxKind)80;

    // ---- parser rule bodies
    public const SyntaxKind Alternatives = (SyntaxKind)81;
    public const SyntaxKind ConditionalBranch = (SyntaxKind)82;
    public const SyntaxKind Guard = (SyntaxKind)83;
    public const SyntaxKind UnorderedGroup = (SyntaxKind)84;
    public const SyntaxKind Group = (SyntaxKind)85;
    public const SyntaxKind Cardinality = (SyntaxKind)86;
    public const SyntaxKind Action = (SyntaxKind)87;
    public const SyntaxKind Feature = (SyntaxKind)88;
    public const SyntaxKind Operator = (SyntaxKind)89;
    public const SyntaxKind Assignment = (SyntaxKind)90;
    public const SyntaxKind Predicate = (SyntaxKind)91;
    public const SyntaxKind PredicatedKeyword = (SyntaxKind)92;
    public const SyntaxKind PredicatedRuleCall = (SyntaxKind)93;
    public const SyntaxKind PredicatedGroup = (SyntaxKind)94;
    public const SyntaxKind CrossReference = (SyntaxKind)95;
    public const SyntaxKind Separator = (SyntaxKind)96;
    public const SyntaxKind ParenthesizedElement = (SyntaxKind)97;
    public const SyntaxKind EndOfFile = (SyntaxKind)98;
    public const SyntaxKind Keyword = (SyntaxKind)99;
    public const SyntaxKind RuleCall = (SyntaxKind)100;
    public const SyntaxKind ArgumentList = (SyntaxKind)101;
    public const SyntaxKind NamedArgument = (SyntaxKind)102;
    public const SyntaxKind Disjunction = (SyntaxKind)103;
    public const SyntaxKind Conjunction = (SyntaxKind)104;
    public const SyntaxKind Negation = (SyntaxKind)105;
    public const SyntaxKind ParameterReference = (SyntaxKind)106;
    public const SyntaxKind ParenthesizedCondition = (SyntaxKind)107;

    // ---- terminal rule bodies
    public const SyntaxKind TerminalAlternatives = (SyntaxKind)108;
    public const SyntaxKind TerminalGroup = (SyntaxKind)109;
    public const SyntaxKind TerminalCardinality = (SyntaxKind)110;
    public const SyntaxKind CharacterRange = (SyntaxKind)111;
    public const SyntaxKind TerminalRuleCall = (SyntaxKind)112;
    public const SyntaxKind RegexToken = (SyntaxKind)113;
    public const SyntaxKind Wildcard = (SyntaxKind)114;
    public const SyntaxKind NegatedToken = (SyntaxKind)115;
    public const SyntaxKind UntilToken = (SyntaxKind)116;
    public const SyntaxKind Lookahead = (SyntaxKind)117;
    public const SyntaxKind ParenthesizedTerminal = (SyntaxKind)118;
}
