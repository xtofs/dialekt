namespace Dialekt.Scanning;

using System.Text.RegularExpressions;
using Dialekt.Green;

internal class Scanner
{
    /// <summary>
    /// Scans the whole input. Text that no pattern recognizes is returned as <see cref="Token.ErrorKind"/> tokens
    /// (carrying a message) instead of silently ending the scan; the sequence always ends with an <see cref="Token.Eof"/> token.
    /// </summary>
    public static IEnumerable<Token> Scan(string content)
    {
        var pos = new Position(0, 1, 1);
        while (pos.Offset < content.Length)
        {
            Token token;
            if (TryMatch(content, pos.Offset, out var kind, out var text))
            {
                token = MakeToken(kind, text, ref pos);
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

    private static bool TryMatch(string content, int offset, out string kind, out string text)
    {
        var match = TokenPatterns.TokenRegex().Match(content, offset);
        if (match.Success && match.Length > 0)
        {
            // var alternative = match.Groups.Cast<Group>().Skip(1).Single(g => g.Success);
            var succeeding = GetSucceedingGroup(match);
            (kind, text) = (succeeding.Name, succeeding.Value);
            return true;
        }
        return TryMatchSymbol(content, offset, out kind, out text);
    }

    private static Group GetSucceedingGroup(Match match)
    {
        // return match.Groups.Cast<Group>().Skip(1).Single(g => g.Success);
        for (var i = 1; i < match.Groups.Count; i++)
        {
            var group = match.Groups[i];
            if (group.Success)
            {
                return group;
            }
        }
        throw new InvalidOperationException("No succeeding group found.");
    }

    // Longest first so that e.g. `=>` and `?<=` are not split into shorter symbols.
    private static bool TryMatchSymbol(string content, int offset, out string kind, out string text)
    {
        int maxLength = Math.Min(SyntaxFacts.MaxSymbolLength, content.Length - offset);
        

        for (var length = maxLength; length > 0; length--)
        {
            if (SyntaxFacts.TryGetSymbolKind(content.AsSpan(offset, length), out _))
            {
                (kind, text) = (Token.SymbolKind, content.Substring(offset, length));
                return true;
            }
        }
        (kind, text) = ("", "");
        return false;
    }

    private static (int Length, string Message) MeasureError(string content, int offset)
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
        while (end < content.Length && !char.IsWhiteSpace(content[end]) && !TryMatch(content, end, out _, out _))
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

    /// <summary>
    /// Creates a new token with the specified kind, text, and position.
    /// Updates the position to reflect the consumed text.
    /// </summary>
    /// <param name="kind">The kind of the token.</param>
    /// <param name="text">The text of the token.</param>
    /// <param name="pos">The current position, which will be updated.</param>
    /// <param name="error">An optional error message.</param>
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
