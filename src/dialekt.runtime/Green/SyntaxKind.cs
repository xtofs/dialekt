namespace Dialekt.Green;

/// <summary>
/// Scalar tag that distinguishes every kind of green node. The green layer is untyped and language-agnostic: it only knows
/// the handful of kinds below. A language defines its own kinds as constants of this type, numbered from
/// <see cref="FirstLanguageKind"/> (e.g. <c>public const SyntaxKind Person = (SyntaxKind)16;</c>), which keeps them usable
/// in patterns and switches.
/// </summary>
public enum SyntaxKind : ushort
{
    None = 0,

    /// <summary>An optional part that is not there. One shared zero-width node.</summary>
    Absent,
    /// <summary>A required part the parser expected but did not find (flagged <see cref="GreenNodeFlags.IsMissing"/>).</summary>
    Missing,
    /// <summary>Zero or more consecutive children of the same slot.</summary>
    List,
    /// <summary>Items alternating with separator tokens (<c>a | b | c</c>); never starts or ends with a separator.</summary>
    SeparatedList,
    /// <summary>A stretch of source the parser had to skip to recover from a syntax error; its children are free-form.</summary>
    Error,

    /// <summary>Text no pattern of the scanner recognizes.</summary>
    ErrorToken,
    /// <summary>Zero-width token at the end of the input; carries the trailing whitespace and comments.</summary>
    EndOfFileToken,

    /// <summary>The first value a language may use for its own kinds.</summary>
    FirstLanguageKind = 16,
}
