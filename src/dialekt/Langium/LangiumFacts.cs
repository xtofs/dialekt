namespace Dialekt.Langium;

using Dialekt.Green;

public static class LangiumFacts
{
    /// <summary>Whether <paramref name="kind"/> is a token kind (identifiers, literals, symbols, error and end of file).</summary>
    public static bool IsToken(SyntaxKind kind) => kind is SyntaxKind.ErrorToken or SyntaxKind.EndOfFileToken or (>= LangiumKinds.IdentifierToken and <= LangiumKinds.QuestionEqualsToken);

    /// <summary>Whether <paramref name="kind"/> is trivia (whitespace or a comment).</summary>
    public static bool IsTrivia(SyntaxKind kind) => kind is LangiumKinds.WhitespaceTrivia or LangiumKinds.SingleLineCommentTrivia or LangiumKinds.MultiLineCommentTrivia;

    /// <summary>Whether <paramref name="kind"/> is one of the punctuation/operator tokens recognized by <see cref="TryGetSymbolKind"/>.</summary>
    public static bool IsSymbol(SyntaxKind kind) => kind is >= LangiumKinds.ColonToken and <= LangiumKinds.QuestionEqualsToken;

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
            ":" => LangiumKinds.ColonToken,
            "|" => LangiumKinds.BarToken,
            ">" => LangiumKinds.GreaterThanToken,
            "=" => LangiumKinds.EqualsToken,
            "<" => LangiumKinds.LessThanToken,
            "&" => LangiumKinds.AmpersandToken,
            "." => LangiumKinds.DotToken,
            ".." => LangiumKinds.DotDotToken,
            "," => LangiumKinds.CommaToken,
            "=>" => LangiumKinds.EqualsGreaterThanToken,
            "+" => LangiumKinds.PlusToken,
            "*" => LangiumKinds.StarToken,
            "?<=" => LangiumKinds.QuestionLessThanEqualsToken,
            "?<!" => LangiumKinds.QuestionLessThanBangToken,
            "?" => LangiumKinds.QuestionToken,
            "?!" => LangiumKinds.QuestionBangToken,
            "->" => LangiumKinds.MinusGreaterThanToken,
            "(" => LangiumKinds.OpenParenToken,
            ")" => LangiumKinds.CloseParenToken,
            "{" => LangiumKinds.OpenBraceToken,
            "}" => LangiumKinds.CloseBraceToken,
            "[" => LangiumKinds.OpenBracketToken,
            "]" => LangiumKinds.CloseBracketToken,
            ";" => LangiumKinds.SemicolonToken,
            "!" => LangiumKinds.BangToken,
            "@" => LangiumKinds.AtToken,
            "+=" => LangiumKinds.PlusEqualsToken,
            "?=" => LangiumKinds.QuestionEqualsToken,
            _ => SyntaxKind.None,
        };
        return kind != SyntaxKind.None;
    }
}
