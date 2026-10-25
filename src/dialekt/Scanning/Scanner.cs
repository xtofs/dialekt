namespace Dialekt.Scanning;

using System.Reflection;
using System.Text.RegularExpressions;
using Dialekt.Green;

internal class Scanner
{
    // Order matters: the first alternative that matches wins, so literals and comments come before
    // identifiers and the (longest-first) symbols.
    private static readonly string[] PatternOrder =
    [
        "Whitespace", "SingleLineComment", "MultiLineComment", "StringLiteral", "Number", "Identifier", "RegexLiteral",
    ];

    private readonly Regex _regex;

    public Scanner()
    {
        var methods = typeof(TokenPatterns).GetMethods().Where(m => m.IsStatic)
            .ToDictionary(m => m.Name.StripSuffix("Regex"), m => m.GetCustomAttribute<GeneratedRegexAttribute>()!.Pattern);

        var alternatives = string.Join("|", PatternOrder.Select(name => $"(?<{name}>{methods[name]})"));
        // Longest first so that e.g. `=>` and `?<=` are not split into shorter symbols.
        var symbols = string.Join("|", SyntaxFacts.SymbolKinds.Keys.OrderByDescending(sym => sym.Length).Select(Regex.Escape));
        _regex = new Regex($@"\G(?:{alternatives}|(?<{Token.SymbolKind}>{symbols}))", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
    }

    /// <summary>
    /// Scans the whole input. Text that no pattern recognizes is returned as <see cref="Token.ErrorKind"/> tokens
    /// (carrying a message) instead of silently ending the scan; the sequence always ends with an <see cref="Token.Eof"/> token.
    /// </summary>
    public IEnumerable<Token> Scan(string content)
    {
        var pos = new Position(0, 1, 1);
        while (pos.Offset < content.Length)
        {
            var match = _regex.Match(content, pos.Offset);
            Token token;
            if (match.Success && match.Length > 0)
            {
                var alternative = match.Groups.Cast<Group>().Skip(1).Single(g => g.Success);
                token = MakeToken(alternative.Name, alternative.Value, ref pos);
            }
            else
            {
                var (length, message) = MeasureError(content, pos.Offset);
                token = MakeToken(Token.ErrorKind, content.Substring(pos.Offset, length), ref pos, message);
            }
            yield return token;
        }
        yield return new Token(Token.Eof, "", new SourceSpan(pos, pos));
    }

    private (int Length, string Message) MeasureError(string content, int offset)
    {
        var c = content[offset];
        if (c is '"' or '\'')
        {
            return (LineLength(content, offset), "Unterminated string literal");
        }
        if (c == '/' && offset + 1 < content.Length && content[offset + 1] == '*')
        {
            return (content.Length - offset, "Unterminated comment");
        }
        if (c == '/')
        {
            return (LineLength(content, offset), "Invalid or unterminated regular expression literal");
        }

        var end = offset + 1;
        while (end < content.Length && !char.IsWhiteSpace(content[end]) && !_regex.Match(content, end).Success)
        {
            end++;
        }
        var text = content[offset..end];
        return (end - offset, end - offset == 1 ? $"Unexpected character '{text}'" : $"Unexpected text '{text}'");
    }

    private static int LineLength(string content, int offset)
    {
        var end = content.IndexOfAny(['\r', '\n'], offset);
        return (end < 0 ? content.Length : end) - offset;
    }

    private static Token MakeToken(string kind, string text, ref Position pos, string? error = null)
    {
        var start = pos;
        var (offset, line, column) = (pos.Offset + text.Length, pos.Line, pos.Column);
        foreach (var c in text)
        {
            if (c == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }
        pos = new Position(offset, line, column);
        return new Token(kind, text, new SourceSpan(start, pos), error);
    }
}
