namespace Dialekt;

using System.Diagnostics;
using System.Runtime.InteropServices;
using Dialekt.Green;
using Dialekt.Scanning;

/// <summary>
/// Recursive-descent parser for Langium grammar and type files. Builds a lossless green tree (every token and all
/// whitespace/comments are in it) directly into a <see cref="GreenArena"/>.
/// Syntax errors are recorded as diagnostics; the parser then skips to the next declaration (or, inside an
/// interface body, the next attribute) so that later errors are still reported.
/// </summary>
public sealed class Parser
{
    private sealed class ParseException : Exception;

    private readonly GreenArena _arena;
    private readonly GreenNodeCache _cache;

    // One entry per non-trivia token: the scanner token and its green node (leading trivia + text).
    private readonly List<Token> _tokens = [];
    private readonly List<GreenNodeHandle> _tokenNodes = [];
    private readonly List<int> _tokenWidths = [];

    // Completed nodes and consumed tokens not yet owned by a node; Close(mark, kind) turns everything after `mark` into a node.
    private readonly List<GreenNodeHandle> _children = [];
    private readonly List<int> _widths = [];

    private readonly List<Diagnostic> _diagnostics = [];
    private int _pos;

    private Parser(string content, GreenTree? previous)
    {
        // Forking keeps all nodes of the previous version addressable, so identical subtrees are shared with it.
        _arena = previous is null ? new GreenArena(content.Length * 4) : previous.Arena.Fork();
        _cache = previous is null ? new GreenNodeCache(_arena) : GreenNodeCache.FromArena(_arena);

        var trivia = new List<GreenNodeHandle>();
        var triviaWidth = 0;
        foreach (var token in new Scanner().Scan(content))
        {
            if (token.Kind == Token.ErrorKind)
            {
                _diagnostics.Add(new Diagnostic(token.Error!, token.Span.Start.Line, token.Span.Start.Column, token.Text.Length, true));
            }
            if (token.IsTrivia)
            {
                trivia.Add(_cache.GetOrAdd(TriviaKind(token), token.Text.Length, [], GreenNodeFlags.IsTrivia));
                triviaWidth += token.Text.Length;
                continue;
            }

            var flags = GreenNodeFlags.IsToken | (token.Kind == Token.ErrorKind ? GreenNodeFlags.IsError : GreenNodeFlags.None);
            var width = triviaWidth + token.Text.Length;
            _tokens.Add(token);
            _tokenNodes.Add(_cache.GetOrAdd(TokenKind(token), width, CollectionsMarshal.AsSpan(trivia), flags));
            _tokenWidths.Add(width);
            trivia.Clear();
            triviaWidth = 0;
        }
    }

    public static ParseResult Parse(string content, GreenTree? previous = null)
    {
        var parser = new Parser(content, previous);
        var root = parser.ParseGrammar();
        parser._arena.Seal();
        var tree = new GreenTree(parser._arena, root);
        if (tree.FullWidth != content.Length)
        {
            throw new UnreachableException($"The green tree covers {tree.FullWidth} of {content.Length} characters.");
        }
        return new ParseResult(tree, parser._diagnostics.OrderBy(d => d.Line).ThenBy(d => d.Column).ToList());
    }

    private static SyntaxKind TriviaKind(Token token) => token.Kind switch
    {
        "Whitespace" => SyntaxKind.WhitespaceTrivia,
        "SingleLineComment" => SyntaxKind.SingleLineCommentTrivia,
        _ => SyntaxKind.MultiLineCommentTrivia,
    };

    private static SyntaxKind TokenKind(Token token) => token.Kind switch
    {
        "Identifier" => SyntaxKind.IdentifierToken,
        "StringLiteral" => SyntaxKind.StringLiteralToken,
        "Number" => SyntaxKind.NumberToken,
        "RegexLiteral" => SyntaxKind.RegexLiteralToken,
        Token.ErrorKind => SyntaxKind.ErrorToken,
        Token.Eof => SyntaxKind.EndOfFileToken,
        _ => SymbolKinds[token.Text],
    };

    private static readonly Dictionary<string, SyntaxKind> SymbolKinds = new()
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

    // ---------------------------------------------------------------- token helpers

    private Token Cur => _tokens[_pos];
    private Token Peek(int n) => _tokens[Math.Min(_pos + n, _tokens.Count - 1)];
    private bool AtEof => Cur.Kind == Token.Eof;

    private static bool IsSym(Token t, string text) => t.IsSymbol && t.Text == text;
    private static bool IsKw(Token t, string text) => t.Kind == "Identifier" && t.Text == text;
    private bool IsSym(string text, int n = 0) => IsSym(Peek(n), text);
    private bool IsKw(string text, int n = 0) => IsKw(Peek(n), text);
    private bool IsIdent(int n = 0) => Peek(n).Kind == "Identifier";
    private bool IsString(int n = 0) => Peek(n).Kind == "StringLiteral";

    /// <summary>Consumes the current token (never the end of file) and hands its green node to the enclosing node.</summary>
    private Token Advance()
    {
        var t = Cur;
        if (!AtEof)
        {
            _children.Add(_tokenNodes[_pos]);
            _widths.Add(_tokenWidths[_pos]);
            _pos++;
        }
        return t;
    }

    private bool Accept(string symbol)
    {
        if (!IsSym(symbol)) return false;
        Advance();
        return true;
    }

    private bool AcceptKw(string keyword)
    {
        if (!IsKw(keyword)) return false;
        Advance();
        return true;
    }

    private Token ExpectSym(string symbol) => IsSym(symbol) ? Advance() : throw Fail($"'{symbol}'");
    private Token ExpectKw(string keyword) => IsKw(keyword) ? Advance() : throw Fail($"'{keyword}'");
    private Token ExpectIdent(string what) => IsIdent() ? Advance() : throw Fail(what);
    private Token ExpectString(string what) => IsString() ? Advance() : throw Fail(what);

    private ParseException Fail(string expected)
    {
        var t = Cur;
        // Error tokens were already reported by the scanner.
        if (t.Kind != Token.ErrorKind)
        {
            var found = t.Kind == Token.Eof ? "end of file" : $"'{Shorten(t.Text)}'";
            var (line, column) = (t.Span.Start.Line, t.Span.Start.Column);
            if (!_diagnostics.Any(d => d.Line == line && d.Column == column))
            {
                _diagnostics.Add(new Diagnostic($"Expected {expected} but found {found}", line, column, Math.Max(t.Text.Length, 1), false));
            }
        }
        return new ParseException();
    }

    private static string Shorten(string text) => text.Length > 20 ? text[..20] + "…" : text;

    // ---------------------------------------------------------------- node construction

    /// <summary>Position in the child list; everything added after it can later be closed into a node.</summary>
    private int Mark() => _children.Count;

    /// <summary>Makes all children added since <paramref name="mark"/> the children of a new node of the given kind.</summary>
    private void Close(int mark, SyntaxKind kind)
    {
        var count = _children.Count - mark;
        var width = 0;
        for (var i = mark; i < _widths.Count; i++)
        {
            width += _widths[i];
        }
        var handle = _cache.GetOrAdd(kind, width, CollectionsMarshal.AsSpan(_children).Slice(mark, count));
        _children.RemoveRange(mark, count);
        _widths.RemoveRange(mark, count);
        _children.Add(handle);
        _widths.Add(width);
    }

    // ---------------------------------------------------------------- file level

    private GreenNodeHandle ParseGrammar()
    {
        var root = Mark();
        if (IsKw("grammar") && IsIdent(1))
        {
            var m = Mark();
            Advance();
            Advance();
            Close(m, SyntaxKind.GrammarDeclaration);
        }

        while (!AtEof)
        {
            var (mark, before) = (Mark(), _pos);
            try
            {
                ParseDeclaration();
            }
            catch (ParseException)
            {
                Recover(mark, before);
            }
        }

        // The end-of-file token carries the trailing whitespace and comments.
        _children.Add(_tokenNodes[_pos]);
        _widths.Add(_tokenWidths[_pos]);
        Close(root, SyntaxKind.Grammar);
        return _children[0];
    }

    private void ParseDeclaration()
    {
        if (IsKw("import")) ParseImport();
        else if (IsKw("interface")) ParseInterface();
        else if (IsKw("type") && IsIdent(1)) ParseTypeAlias();
        else if (IsKw("infix") && IsIdent(1)) ParseInfixRule();
        else if (IsKw("terminal") || (IsKw("hidden") && IsKw("terminal", 1))) ParseTerminalRule();
        else if (IsKw("entry") || IsKw("fragment") || IsIdent()) ParseParserRule();
        else throw Fail("a rule, interface, type or import");
    }

    /// <summary>
    /// Skips to the end of the broken declaration: past the next top-level ';', or up to a token in column 1
    /// (where the next declaration most likely starts). Always makes progress. Everything consumed since
    /// <paramref name="mark"/>, including what the failed parse had already consumed, ends up in one Error node.
    /// </summary>
    private void Recover(int mark, int before)
    {
        if (_pos == before && !AtEof)
        {
            Advance();
        }
        var depth = 0;
        while (!AtEof)
        {
            var t = Cur;
            if (_pos > before && t.Span.Start.Column == 1 && depth <= 0 && (t.Kind == "Identifier"))
            {
                break;
            }
            Advance();
            if (t.IsSymbol)
            {
                switch (t.Text)
                {
                    case "(" or "[" or "{": depth++; break;
                    case ")" or "]" or "}": depth--; break;
                    case ";" when depth <= 0: Close(mark, SyntaxKind.Error); return;
                }
            }
        }
        Close(mark, SyntaxKind.Error);
    }

    private void ParseImport()
    {
        var m = Mark();
        Advance();
        ExpectString("an import path");
        Accept(";");
        Close(m, SyntaxKind.Import);
    }

    // ---------------------------------------------------------------- types

    private void ParseInterface()
    {
        var m = Mark();
        Advance();
        ExpectIdent("an interface name");
        if (AcceptKw("extends"))
        {
            do
            {
                var s = Mark();
                ExpectIdent("a super type name");
                Close(s, SyntaxKind.SuperType);
            } while (Accept(","));
        }
        ExpectSym("{");
        while (!IsSym("}") && !AtEof)
        {
            var (mark, before) = (Mark(), _pos);
            try
            {
                ParseTypeAttribute();
            }
            catch (ParseException)
            {
                RecoverAttribute(mark, before);
            }
        }
        ExpectSym("}");
        Accept(";");
        Close(m, SyntaxKind.Interface);
    }

    private void RecoverAttribute(int mark, int before)
    {
        if (_pos == before && !AtEof && !IsSym("}"))
        {
            Advance();
        }
        while (!AtEof && !IsSym("}"))
        {
            if (Advance() is { IsSymbol: true, Text: ";" })
            {
                break;
            }
        }
        Close(mark, SyntaxKind.Error);
    }

    private void ParseTypeAttribute()
    {
        var m = Mark();
        ExpectIdent("an attribute name");
        if (IsSym("?"))
        {
            var o = Mark();
            Advance();
            Close(o, SyntaxKind.Optional);
        }
        ExpectSym(":");
        ParseTypeDefinition();
        if (Accept("="))
        {
            ParseValueLiteral();
        }
        Accept(";");
        Close(m, SyntaxKind.Attribute);
    }

    private void ParseValueLiteral()
    {
        var m = Mark();
        if (IsString())
        {
            Advance();
            Close(m, SyntaxKind.StringLiteral);
        }
        else if (Cur.Kind == "Number")
        {
            Advance();
            Close(m, SyntaxKind.NumberLiteral);
        }
        else if (IsKw("true") || IsKw("false"))
        {
            Advance();
            Close(m, SyntaxKind.BooleanLiteral);
        }
        else if (IsSym("["))
        {
            Advance();
            if (!IsSym("]"))
            {
                do
                {
                    ParseValueLiteral();
                } while (Accept(","));
            }
            ExpectSym("]");
            Close(m, SyntaxKind.ArrayLiteral);
        }
        else
        {
            throw Fail("a value (string, number, boolean or array)");
        }
    }

    private void ParseTypeAlias()
    {
        var m = Mark();
        Advance();
        ExpectIdent("a type name");
        ExpectSym("=");
        ParseTypeDefinition();
        Accept(";");
        Close(m, SyntaxKind.TypeAlias);
    }

    // union < array < reference/simple
    private void ParseTypeDefinition()
    {
        var m = Mark();
        ParseArrayType();
        if (!IsSym("|")) return;
        while (Accept("|"))
        {
            ParseArrayType();
        }
        Close(m, SyntaxKind.UnionType);
    }

    private void ParseArrayType()
    {
        var m = Mark();
        ParseReferenceType();
        while (IsSym("[") && IsSym("]", 1))
        {
            Advance();
            Advance();
            Close(m, SyntaxKind.ArrayType);
        }
    }

    private void ParseReferenceType()
    {
        if (!IsSym("@"))
        {
            ParseSimpleType();
            return;
        }
        var m = Mark();
        Advance();
        ParseSimpleType();
        if (IsSym("+"))
        {
            var multi = Mark();
            Advance();
            Close(multi, SyntaxKind.Multi);
        }
        Close(m, SyntaxKind.ReferenceType);
    }

    private void ParseSimpleType()
    {
        var m = Mark();
        if (IsSym("("))
        {
            Advance();
            ParseTypeDefinition();
            ExpectSym(")");
            Close(m, SyntaxKind.Parenthesized);
        }
        else if (IsIdent())
        {
            Advance();
            Close(m, SyntaxKind.SimpleType);
        }
        else if (IsString())
        {
            Advance();
            Close(m, SyntaxKind.StringType);
        }
        else
        {
            throw Fail("a type");
        }
    }

    // ---------------------------------------------------------------- rules

    private void ParseParserRule()
    {
        var m = Mark();
        if (IsKw("entry") || (IsKw("fragment") && IsIdent(1)))
        {
            var modifier = Mark();
            Advance();
            Close(modifier, SyntaxKind.Modifier);
        }
        ExpectIdent("a rule name");
        ParseParameters();
        ParseReturnsOrInfers();
        ExpectSym(":");
        ParseAlternatives();
        ExpectSym(";");
        Close(m, SyntaxKind.ParserRule);
    }

    private void ParseInfixRule()
    {
        var m = Mark();
        Advance();
        ExpectIdent("a rule name");
        ParseParameters();
        ExpectKw("on");
        ParseRuleCall();
        ParseReturnsOrInfers();
        ExpectSym(":");

        var operators = Mark();
        do
        {
            var list = Mark();
            if (IsKw("left") || IsKw("right"))
            {
                var assoc = Mark();
                Advance();
                ExpectKw("assoc");
                Close(assoc, SyntaxKind.Associativity);
            }
            do
            {
                ParseKeyword();
            } while (Accept("|"));
            Close(list, SyntaxKind.InfixRuleOperatorList);
        } while (Accept(">"));
        Close(operators, SyntaxKind.InfixRuleOperators);

        ExpectSym(";");
        Close(m, SyntaxKind.InfixRule);
    }

    private void ParseParameters()
    {
        if (!IsSym("<")) return;
        var m = Mark();
        Advance();
        if (!IsSym(">"))
        {
            do
            {
                var p = Mark();
                ExpectIdent("a parameter name");
                Close(p, SyntaxKind.Parameter);
            } while (Accept(","));
        }
        ExpectSym(">");
        Close(m, SyntaxKind.ParameterList);
    }

    private void ParseReturnsOrInfers()
    {
        if (IsKw("returns"))
        {
            var m = Mark();
            Advance();
            ExpectIdent("a return type");
            Close(m, SyntaxKind.ReturnType);
        }
        else if (IsKw("infers"))
        {
            var m = Mark();
            Advance();
            ExpectIdent("a type name");
            Close(m, SyntaxKind.InferredType);
        }
    }

    private void ParseTerminalRule()
    {
        var m = Mark();
        if (IsKw("hidden"))
        {
            var modifier = Mark();
            Advance();
            Close(modifier, SyntaxKind.Modifier);
        }
        ExpectKw("terminal");
        if (IsKw("fragment"))
        {
            var modifier = Mark();
            Advance();
            Close(modifier, SyntaxKind.Modifier);
            ExpectIdent("a rule name");
        }
        else
        {
            ExpectIdent("a rule name");
            if (IsKw("returns"))
            {
                var r = Mark();
                Advance();
                ExpectIdent("a return type");
                Close(r, SyntaxKind.ReturnType);
            }
        }
        ExpectSym(":");
        ParseTerminalAlternatives();
        ExpectSym(";");
        Close(m, SyntaxKind.TerminalRule);
    }

    // ---------------------------------------------------------------- parser rule bodies
    // Precedence (loosest to tightest): alternatives '|' < unordered group '&' < group (juxtaposition) < cardinality

    private void ParseAlternatives()
    {
        var m = Mark();
        ParseConditionalBranch();
        if (!IsSym("|")) return;
        while (Accept("|"))
        {
            ParseConditionalBranch();
        }
        Close(m, SyntaxKind.Alternatives);
    }

    private void ParseConditionalBranch()
    {
        if (!IsSym("<"))
        {
            ParseUnorderedGroup();
            return;
        }
        var m = Mark();
        var guard = Mark();
        Advance();
        ParseDisjunction();
        ExpectSym(">");
        Close(guard, SyntaxKind.Guard);
        do
        {
            ParseAbstractToken();
        } while (IsAbstractTokenStart());
        Close(m, SyntaxKind.ConditionalBranch);
    }

    private void ParseUnorderedGroup()
    {
        var m = Mark();
        ParseGroup();
        if (!IsSym("&")) return;
        while (Accept("&"))
        {
            ParseGroup();
        }
        Close(m, SyntaxKind.UnorderedGroup);
    }

    private void ParseGroup()
    {
        var m = Mark();
        ParseAbstractToken();
        if (!IsAbstractTokenStart()) return;
        while (IsAbstractTokenStart())
        {
            ParseAbstractToken();
        }
        Close(m, SyntaxKind.Group);
    }

    private bool IsAbstractTokenStart() =>
        Cur.Kind is "Identifier" or "StringLiteral" || IsSym("(") || IsSym("{") || IsSym("=>") || IsSym("->");

    private static bool IsAssignmentOperator(Token t) => t.IsSymbol && t.Text is "=" or "+=" or "?=";

    private void ParseAbstractToken()
    {
        if (IsSym("{"))
        {
            ParseAction();
            return;
        }

        var m = Mark();
        var i = IsSym("=>") || IsSym("->") ? 1 : 0;
        if (IsIdent(i) && IsAssignmentOperator(Peek(i + 1)))
        {
            ParseAssignment();
        }
        else
        {
            ParseAbstractTerminal();
        }

        if (IsSym("?") || IsSym("*") || IsSym("+"))
        {
            Advance();
            Close(m, SyntaxKind.Cardinality);
        }
    }

    private void ParseAction()
    {
        var m = Mark();
        Advance();
        if (IsKw("infer") && IsIdent(1))
        {
            var inferred = Mark();
            Advance();
            Advance();
            Close(inferred, SyntaxKind.InferredType);
        }
        else
        {
            ExpectIdent("a type name");
        }
        if (IsSym("."))
        {
            Advance();
            var feature = Mark();
            ExpectIdent("a feature name");
            Close(feature, SyntaxKind.Feature);
            if (!IsAssignmentOperator(Cur) || IsSym("?="))
            {
                throw Fail("'=' or '+='");
            }
            var op = Mark();
            Advance();
            Close(op, SyntaxKind.Operator);
            ExpectKw("current");
        }
        ExpectSym("}");
        Close(m, SyntaxKind.Action);
    }

    private void ParseAssignment()
    {
        var m = Mark();
        if (IsSym("=>") || IsSym("->"))
        {
            var predicate = Mark();
            Advance();
            Close(predicate, SyntaxKind.Predicate);
        }
        var feature = Mark();
        Advance();
        Close(feature, SyntaxKind.Feature);
        var op = Mark();
        Advance();
        Close(op, SyntaxKind.Operator);
        ParseAssignableTerminal();
        Close(m, SyntaxKind.Assignment);
    }

    private void ParseAssignableTerminal()
    {
        if (IsString())
        {
            ParseKeyword();
        }
        else if (IsIdent())
        {
            ParseRuleCall();
        }
        else if (IsSym("("))
        {
            var m = Mark();
            Advance();
            var alternatives = Mark();
            ParseAssignableTerminal();
            if (IsSym("|"))
            {
                while (Accept("|"))
                {
                    ParseAssignableTerminal();
                }
                Close(alternatives, SyntaxKind.Alternatives);
            }
            ExpectSym(")");
            Close(m, SyntaxKind.Parenthesized);
        }
        else if (IsSym("["))
        {
            ParseCrossReference();
        }
        else
        {
            throw Fail("a keyword, rule call, '(' or cross reference");
        }
    }

    private void ParseCrossReference()
    {
        var m = Mark();
        Advance();
        if (IsSym("+"))
        {
            var multi = Mark();
            Advance();
            Close(multi, SyntaxKind.Multi);
        }
        ExpectIdent("a type name");
        if (IsSym("|") || IsSym(":"))
        {
            var separator = Mark();
            Advance();
            Close(separator, SyntaxKind.Separator);
            if (IsString()) ParseKeyword();
            else ParseRuleCall();
        }
        ExpectSym("]");
        Close(m, SyntaxKind.CrossReference);
    }

    private void ParseAbstractTerminal()
    {
        var m = Mark();
        if (IsSym("=>") || IsSym("->"))
        {
            var predicate = Mark();
            Advance();
            Close(predicate, SyntaxKind.Predicate);
            if (IsSym("("))
            {
                Advance();
                ParseAlternatives();
                ExpectSym(")");
                Close(m, SyntaxKind.PredicatedGroup);
            }
            else if (IsString())
            {
                ParseKeyword();
                Close(m, SyntaxKind.PredicatedKeyword);
            }
            else if (IsIdent())
            {
                ParseRuleCall();
                Close(m, SyntaxKind.PredicatedRuleCall);
            }
            else
            {
                throw Fail("a keyword, rule call or '('");
            }
        }
        else if (IsString())
        {
            ParseKeyword();
        }
        else if (IsKw("EOF"))
        {
            Advance();
            Close(m, SyntaxKind.EndOfFile);
        }
        else if (IsIdent())
        {
            ParseRuleCall();
        }
        else if (IsSym("("))
        {
            Advance();
            ParseAlternatives();
            ExpectSym(")");
            Close(m, SyntaxKind.Parenthesized);
        }
        else
        {
            throw Fail("a keyword, rule call or '('");
        }
    }

    private void ParseKeyword()
    {
        var m = Mark();
        ExpectString("a keyword string");
        Close(m, SyntaxKind.Keyword);
    }

    private void ParseRuleCall()
    {
        var m = Mark();
        ExpectIdent("a rule name");
        if (IsSym("<"))
        {
            var arguments = Mark();
            Advance();
            do
            {
                ParseNamedArgument();
            } while (Accept(","));
            ExpectSym(">");
            Close(arguments, SyntaxKind.ArgumentList);
        }
        Close(m, SyntaxKind.RuleCall);
    }

    private void ParseNamedArgument()
    {
        var m = Mark();
        if (IsIdent() && IsSym("=", 1))
        {
            Advance();
            Advance();
        }
        ParseDisjunction();
        Close(m, SyntaxKind.NamedArgument);
    }

    // ---------------------------------------------------------------- conditions: '|' < '&' < '!' < atom

    private void ParseDisjunction() => ParseBinaryCondition(SyntaxKind.Disjunction, "|", ParseConjunction);
    private void ParseConjunction() => ParseBinaryCondition(SyntaxKind.Conjunction, "&", ParseNegation);

    // Left associative: each operator closes everything parsed so far into a new node.
    private void ParseBinaryCondition(SyntaxKind kind, string op, Action operand)
    {
        var m = Mark();
        operand();
        while (IsSym(op))
        {
            Advance();
            operand();
            Close(m, kind);
        }
    }

    private void ParseNegation()
    {
        if (!IsSym("!"))
        {
            ParseAtom();
            return;
        }
        var m = Mark();
        Advance();
        ParseNegation();
        Close(m, SyntaxKind.Negation);
    }

    private void ParseAtom()
    {
        var m = Mark();
        if (IsKw("true") || IsKw("false"))
        {
            Advance();
            Close(m, SyntaxKind.BooleanLiteral);
        }
        else if (IsIdent())
        {
            Advance();
            Close(m, SyntaxKind.ParameterReference);
        }
        else if (IsSym("("))
        {
            Advance();
            ParseDisjunction();
            ExpectSym(")");
            Close(m, SyntaxKind.Parenthesized);
        }
        else
        {
            throw Fail("a condition");
        }
    }

    // ---------------------------------------------------------------- terminal rule bodies

    private void ParseTerminalAlternatives()
    {
        var m = Mark();
        ParseTerminalGroup();
        if (!IsSym("|")) return;
        while (Accept("|"))
        {
            ParseTerminalGroup();
        }
        Close(m, SyntaxKind.TerminalAlternatives);
    }

    private void ParseTerminalGroup()
    {
        var m = Mark();
        ParseTerminalToken();
        if (!IsTerminalTokenStart()) return;
        while (IsTerminalTokenStart())
        {
            ParseTerminalToken();
        }
        Close(m, SyntaxKind.TerminalGroup);
    }

    private bool IsTerminalTokenStart() =>
        Cur.Kind is "Identifier" or "StringLiteral" or "RegexLiteral" || IsSym("(") || IsSym("!") || IsSym("->") || IsSym(".");

    private void ParseTerminalToken()
    {
        var m = Mark();
        ParseTerminalTokenElement();
        if (IsSym("?") || IsSym("*") || IsSym("+"))
        {
            Advance();
            Close(m, SyntaxKind.Cardinality);
        }
    }

    private void ParseTerminalTokenElement()
    {
        var m = Mark();
        if (IsString())
        {
            ParseKeyword();
            if (Accept(".."))
            {
                ParseKeyword();
            }
            Close(m, SyntaxKind.CharacterRange);
        }
        else if (IsIdent())
        {
            Advance();
            Close(m, SyntaxKind.TerminalRuleCall);
        }
        else if (Cur.Kind == "RegexLiteral")
        {
            Advance();
            Close(m, SyntaxKind.RegexToken);
        }
        else if (IsSym("."))
        {
            Advance();
            Close(m, SyntaxKind.Wildcard);
        }
        else if (IsSym("!") || IsSym("->"))
        {
            var kind = IsSym("!") ? SyntaxKind.NegatedToken : SyntaxKind.UntilToken;
            Advance();
            ParseTerminalTokenElement();
            Close(m, kind);
        }
        else if (IsSym("("))
        {
            Advance();
            if (IsSym("?=") || IsSym("?!") || IsSym("?<=") || IsSym("?<!"))
            {
                var lookahead = Mark();
                Advance();
                Close(lookahead, SyntaxKind.Lookahead);
            }
            ParseTerminalAlternatives();
            ExpectSym(")");
            Close(m, SyntaxKind.Parenthesized);
        }
        else
        {
            throw Fail("a terminal element");
        }
    }
}
