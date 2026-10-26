namespace Dialekt.Scanning;

using Dialekt.Green;

/// <summary>A piece of source text with its kind. Trivia (whitespace, comments) and tokens are both tokens at this stage.</summary>
/// <param name="Kind">The kind of the green node the token becomes; <see cref="SyntaxKind.ErrorToken"/> for text no pattern recognizes
/// (then <paramref name="Error"/> says what is wrong).</param>
public sealed record Token(SyntaxKind Kind, string Text, SourceSpan Span, string? Error = null);

/// <summary>Splits source text into tokens. The sequence must cover the whole text and end with a zero-width <see cref="SyntaxKind.EndOfFileToken"/>.</summary>
public interface ITokenizer
{
    IEnumerable<Token> Scan(string content);
}
