namespace Dialekt.Green;

public static class SyntaxFacts
{
    /// <summary>
    /// Every punctuation/operator token of the language and its kind. 
    /// The scanner recognizes exactly these.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, SyntaxKind> SymbolKinds = new Dictionary<string, SyntaxKind>
    {
        [":"] = SyntaxKind.ColonToken,
        ["|"] = SyntaxKind.BarToken,
        [">"] = SyntaxKind.GreaterThanToken,
        ["="] = SyntaxKind.EqualsToken,
        ["<"] = SyntaxKind.LessThanToken,
        ["&"] = SyntaxKind.AmpersandToken,
        ["."] = SyntaxKind.DotToken,
        [".."] = SyntaxKind.DotDotToken,
        [","] = SyntaxKind.CommaToken,
        ["=>"] = SyntaxKind.EqualsGreaterThanToken,
        ["+"] = SyntaxKind.PlusToken,
        ["*"] = SyntaxKind.StarToken,
        ["?<="] = SyntaxKind.QuestionLessThanEqualsToken,
        ["?<!"] = SyntaxKind.QuestionLessThanBangToken,
        ["?"] = SyntaxKind.QuestionToken,
        ["?!"] = SyntaxKind.QuestionBangToken,
        ["->"] = SyntaxKind.MinusGreaterThanToken,
        ["("] = SyntaxKind.OpenParenToken,
        [")"] = SyntaxKind.CloseParenToken,
        ["{"] = SyntaxKind.OpenBraceToken,
        ["}"] = SyntaxKind.CloseBraceToken,
        ["["] = SyntaxKind.OpenBracketToken,
        ["]"] = SyntaxKind.CloseBracketToken,
        [";"] = SyntaxKind.SemicolonToken,
        ["!"] = SyntaxKind.BangToken,
        ["@"] = SyntaxKind.AtToken,
        ["+="] = SyntaxKind.PlusEqualsToken,
        ["?="] = SyntaxKind.QuestionEqualsToken,
    };
}
