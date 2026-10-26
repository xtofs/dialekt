namespace Dialekt.Langium;

using System.Text.RegularExpressions;

internal static partial class TokenPatterns
{
    private const string NumberPattern = @"(?:NaN|-?(?:(?:\d*\.\d+|\d+)(?:[Ee][+-]?\d+)?|Infinity))(?!\w)";

    private const string WhitespacePattern = @"\s+";

    private const string IdentifierPattern = @"\^?[_a-zA-Z][\w_]*";

    private const string SingleLineCommentPattern = @"\/\/[^\n\r]*";

    private const string MultiLineCommentPattern = @"\/\*[\s\S]*?\*\/";

    // @"\/(?![*+?])(\\.|[^\/\\])*\/"
    // @"/\/(?![*+?])(?:[^\r\n\[/\\]|\\.|\[(?:[^\r\n\]\\]|\\.)*\])+\/[a-z]*/"
    private const string RegexLiteralPattern = @"\/(?![*+?])(?:\\.|[^\r\n\[/\\]|\[(?:[^\r\n\]\\]|\\.)*\])+\/[a-z]*";

    private const string StringLiteralPattern = @"""(\\.|[^""\\])*""|'(\\.|[^'\\])*'";

    // Order matters: the first alternative that matches wins, so literals and comments come before identifiers.
    // The group names are the token kinds. Symbols are not part of this pattern (see Scanner.TryMatchSymbol).
    private const string TokenPattern = @"\G(?:" +
        $"(?<Whitespace>{WhitespacePattern})" +
        $"|(?<SingleLineComment>{SingleLineCommentPattern})" +
        $"|(?<MultiLineComment>{MultiLineCommentPattern})" +
        $"|(?<StringLiteral>{StringLiteralPattern})" +
        $"|(?<Number>{NumberPattern})" +
        $"|(?<Identifier>{IdentifierPattern})" +
        $"|(?<RegexLiteral>{RegexLiteralPattern})" +
        ")";

    [GeneratedRegex(TokenPattern, RegexOptions.ExplicitCapture)]
    public static partial Regex TokenRegex();
}
