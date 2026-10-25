namespace Dialekt.Green;

public static class SyntaxFacts
{
    /// <summary>Length of the longest symbol in <see cref="TryGetSymbolKind"/>.</summary>
    public const int MaxSymbolLength = 3;

    /// <summary>
    /// Every punctuation/operator token of the language and its kind; the scanner recognizes exactly these.
    /// A switch rather than a dictionary: it needs no start-up work, which matters when the parser runs once per process.
    /// </summary>
    public static bool TryGetSymbolKind(ReadOnlySpan<char> text, out SyntaxKind kind)
    {
        kind = text switch
        {
            ":" => SyntaxKind.ColonToken,
            "|" => SyntaxKind.BarToken,
            ">" => SyntaxKind.GreaterThanToken,
            "=" => SyntaxKind.EqualsToken,
            "<" => SyntaxKind.LessThanToken,
            "&" => SyntaxKind.AmpersandToken,
            "." => SyntaxKind.DotToken,
            ".." => SyntaxKind.DotDotToken,
            "," => SyntaxKind.CommaToken,
            "=>" => SyntaxKind.EqualsGreaterThanToken,
            "+" => SyntaxKind.PlusToken,
            "*" => SyntaxKind.StarToken,
            "?<=" => SyntaxKind.QuestionLessThanEqualsToken,
            "?<!" => SyntaxKind.QuestionLessThanBangToken,
            "?" => SyntaxKind.QuestionToken,
            "?!" => SyntaxKind.QuestionBangToken,
            "->" => SyntaxKind.MinusGreaterThanToken,
            "(" => SyntaxKind.OpenParenToken,
            ")" => SyntaxKind.CloseParenToken,
            "{" => SyntaxKind.OpenBraceToken,
            "}" => SyntaxKind.CloseBraceToken,
            "[" => SyntaxKind.OpenBracketToken,
            "]" => SyntaxKind.CloseBracketToken,
            ";" => SyntaxKind.SemicolonToken,
            "!" => SyntaxKind.BangToken,
            "@" => SyntaxKind.AtToken,
            "+=" => SyntaxKind.PlusEqualsToken,
            "?=" => SyntaxKind.QuestionEqualsToken,
            _ => SyntaxKind.None,
        };
        return kind != SyntaxKind.None;
    }
}
