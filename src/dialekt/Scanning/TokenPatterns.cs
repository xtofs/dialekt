
namespace Dialekt.Scanning;

using System.Text.RegularExpressions;

static partial class TokenPatterns
{

    [GeneratedRegex(@"(?:NaN|-?(?:(?:\d*\.\d+|\d+)(?:[Ee][+-]?\d+)?|Infinity))(?!\w)")]
    public static partial Regex NumberRegex();

    [GeneratedRegex(@"\s+")]
    public static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\^?[_a-zA-Z][\w_]*")]
    public static partial Regex IdentifierRegex();

    [GeneratedRegex(@"\/\/[^\n\r]*")]
    public static partial Regex SingleLineCommentRegex();

    [GeneratedRegex(@"\/\*[\s\S]*?\*\/")]
    public static partial Regex MultiLineCommentRegex();

    // [GeneratedRegex(@"\/(?![*+?])(\\.|[^\/\\])*\/")]
    // [GeneratedRegex(@"/\/(?![*+?])(?:[^\r\n\[/\\]|\\.|\[(?:[^\r\n\]\\]|\\.)*\])+\/[a-z]*/")]
    [GeneratedRegex(@"\/(?![*+?])(?:\\.|[^\r\n\[/\\]|\[(?:[^\r\n\]\\]|\\.)*\])+\/[a-z]*")]
    public static partial Regex RegexLiteralRegex();


    [GeneratedRegex(@"""(\\.|[^""\\])*""|'(\\.|[^'\\])*'")]
    public static partial Regex StringLiteralRegex();

}
