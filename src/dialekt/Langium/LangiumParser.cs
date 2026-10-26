namespace Dialekt.Langium;

using Dialekt.Green;
using Dialekt.Parsing;
using Dialekt.Scanning;
using Dialekt.Syntax;

/// <summary>
/// Recursive-descent parser for Langium grammar and type files. The machinery (tokens, nodes, slots, reuse) is in
/// <see cref="ParserBase"/>; this class has one method per construct of the language.
/// Syntax errors are recorded as diagnostics; the parser then skips to the next declaration (or, inside an
/// interface body, the next attribute) so that later errors are still reported.
/// </summary>
public sealed class LangiumParser : ParserBase
{
    private LangiumParser(string content, SyntaxTree? previous, TextChange? change)
        : base(content, LangiumLanguage.Instance, LangiumScanner.Instance, previous, change)
    {
    }

    /// <summary>Parses the whole text. With <paramref name="previous"/>, identical nodes are shared with that tree.</summary>
    public static ParseResult Parse(string content, SyntaxTree? previous = null) => new LangiumParser(content, previous, null).Run();

    /// <summary>
    /// Parses <paramref name="content"/>, the text of <paramref name="previous"/> with <paramref name="change"/> applied,
    /// reusing the declarations of the previous tree that the change did not affect.
    /// </summary>
    public static ParseResult ReParse(string content, SyntaxTree previous, TextChange change) => new LangiumParser(content, previous, change).Run();

    protected override GreenNodeHandle ParseRoot() => ParseGrammar();

    // Grammar = [GrammarDeclaration?, Members, EndOfFile]; the declarations in Members can be reused.
    protected override int ReusableListSlot => 1;

    // Without a closing ';' the parser looked at the following token to decide that the declaration was over
    // (e.g. a type alias continues with '|', '[' or ';'). Only a token that starts the next declaration is safe.
    protected override bool IsSafeToReuse(SyntaxKind lastTokenKind, Token next) =>
        lastTokenKind == LangiumKinds.SemicolonToken || next.Kind is LangiumKinds.IdentifierToken or SyntaxKind.EndOfFileToken;

    // ---------------------------------------------------------------- token helpers

    private static bool IsSym(Token t, string text) => LangiumFacts.IsSymbol(t.Kind) && t.Text == text;
    private static bool IsKw(Token t, string text) => t.Kind == LangiumKinds.IdentifierToken && t.Text == text;
    private bool IsSym(string text, int n = 0) => IsSym(Peek(n), text);
    private bool IsKw(string text, int n = 0) => IsKw(Peek(n), text);
    private bool IsIdent(int n = 0) => Peek(n).Kind == LangiumKinds.IdentifierToken;
    private bool IsString(int n = 0) => Peek(n).Kind == LangiumKinds.StringLiteralToken;

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

    // ---------------------------------------------------------------- file level

    private GreenNodeHandle ParseGrammar()
    {
        var root = Mark();
        if (IsKw("grammar") && IsIdent(1))
        {
            var m = Mark();
            Advance();
            Advance();
            Close(m, LangiumKinds.GrammarDeclaration);
        }

        while (!AtEof)
        {
            if (TryReuse()) continue;

            CountReparsed();
            var (mark, before) = (Mark(), Pos);
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
        AppendEndOfFile();
        Close(root, LangiumKinds.Grammar);
        return LastNode;
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
        if (Pos == before && !AtEof)
        {
            Advance();
        }
        var depth = 0;
        while (!AtEof)
        {
            var t = Cur;
            if (Pos > before && t.Span.Start.Column == 1 && depth <= 0 && (t.Kind == LangiumKinds.IdentifierToken))
            {
                break;
            }
            Advance();
            if (LangiumFacts.IsSymbol(t.Kind))
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
        Close(m, LangiumKinds.Import);
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
                Close(s, LangiumKinds.SuperType);
            } while (Accept(","));
        }
        ExpectSym("{");
        while (!IsSym("}") && !AtEof)
        {
            var (mark, before) = (Mark(), Pos);
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
        Close(m, LangiumKinds.Interface);
    }

    private void RecoverAttribute(int mark, int before)
    {
        if (Pos == before && !AtEof && !IsSym("}"))
        {
            Advance();
        }
        while (!AtEof && !IsSym("}"))
        {
            if (IsSym(Advance(), ";"))
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
            Close(o, LangiumKinds.Optional);
        }
        ExpectSym(":");
        ParseTypeDefinition();
        if (Accept("="))
        {
            ParseValueLiteral();
        }
        Accept(";");
        Close(m, LangiumKinds.Attribute);
    }

    private void ParseValueLiteral()
    {
        var m = Mark();
        if (IsString())
        {
            Advance();
            Close(m, LangiumKinds.StringLiteral);
        }
        else if (Cur.Kind == LangiumKinds.NumberToken)
        {
            Advance();
            Close(m, LangiumKinds.NumberLiteral);
        }
        else if (IsKw("true") || IsKw("false"))
        {
            Advance();
            Close(m, LangiumKinds.BooleanLiteral);
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
            Close(m, LangiumKinds.ArrayLiteral);
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
        Close(m, LangiumKinds.TypeAlias);
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
        Close(m, LangiumKinds.UnionType);
    }

    private void ParseArrayType()
    {
        var m = Mark();
        ParseReferenceType();
        while (IsSym("[") && IsSym("]", 1))
        {
            Advance();
            Advance();
            Close(m, LangiumKinds.ArrayType);
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
            Close(multi, LangiumKinds.Multi);
        }
        Close(m, LangiumKinds.ReferenceType);
    }

    private void ParseSimpleType()
    {
        var m = Mark();
        if (IsSym("("))
        {
            Advance();
            ParseTypeDefinition();
            ExpectSym(")");
            Close(m, LangiumKinds.ParenthesizedType);
        }
        else if (IsIdent())
        {
            Advance();
            Close(m, LangiumKinds.SimpleType);
        }
        else if (IsString())
        {
            Advance();
            Close(m, LangiumKinds.StringType);
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
            Close(modifier, LangiumKinds.Modifier);
        }
        ExpectIdent("a rule name");
        ParseParameters();
        ParseReturnsOrInfers();
        ExpectSym(":");
        ParseAlternatives();
        ExpectSym(";");
        Close(m, LangiumKinds.ParserRule);
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
                Close(assoc, LangiumKinds.Associativity);
            }
            do
            {
                ParseKeyword();
            } while (Accept("|"));
            Close(list, LangiumKinds.InfixRuleOperatorList);
        } while (Accept(">"));
        Close(operators, LangiumKinds.InfixRuleOperators);

        ExpectSym(";");
        Close(m, LangiumKinds.InfixRule);
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
                Close(p, LangiumKinds.Parameter);
            } while (Accept(","));
        }
        ExpectSym(">");
        Close(m, LangiumKinds.ParameterList);
    }

    private void ParseReturnsOrInfers()
    {
        if (IsKw("returns"))
        {
            var m = Mark();
            Advance();
            ExpectIdent("a return type");
            Close(m, LangiumKinds.ReturnType);
        }
        else if (IsKw("infers"))
        {
            var m = Mark();
            Advance();
            ExpectIdent("a type name");
            Close(m, LangiumKinds.InferredType);
        }
    }

    private void ParseTerminalRule()
    {
        var m = Mark();
        if (IsKw("hidden"))
        {
            var modifier = Mark();
            Advance();
            Close(modifier, LangiumKinds.Modifier);
        }
        ExpectKw("terminal");
        if (IsKw("fragment"))
        {
            var modifier = Mark();
            Advance();
            Close(modifier, LangiumKinds.Modifier);
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
                Close(r, LangiumKinds.ReturnType);
            }
        }
        ExpectSym(":");
        ParseTerminalAlternatives();
        ExpectSym(";");
        Close(m, LangiumKinds.TerminalRule);
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
        Close(m, LangiumKinds.Alternatives);
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
        Close(guard, LangiumKinds.Guard);
        do
        {
            ParseAbstractToken();
        } while (IsAbstractTokenStart());
        Close(m, LangiumKinds.ConditionalBranch);
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
        Close(m, LangiumKinds.UnorderedGroup);
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
        Close(m, LangiumKinds.Group);
    }

    private bool IsAbstractTokenStart() =>
        Cur.Kind is LangiumKinds.IdentifierToken or LangiumKinds.StringLiteralToken || IsSym("(") || IsSym("{") || IsSym("=>") || IsSym("->");

    private static bool IsAssignmentOperator(Token t) => t.Kind is LangiumKinds.EqualsToken or LangiumKinds.PlusEqualsToken or LangiumKinds.QuestionEqualsToken;

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
            Close(m, LangiumKinds.Cardinality);
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
            Close(inferred, LangiumKinds.InferredType);
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
            Close(feature, LangiumKinds.Feature);
            if (!IsAssignmentOperator(Cur) || IsSym("?="))
            {
                throw Fail("'=' or '+='");
            }
            var op = Mark();
            Advance();
            Close(op, LangiumKinds.Operator);
            ExpectKw("current");
        }
        ExpectSym("}");
        Close(m, LangiumKinds.Action);
    }

    private void ParseAssignment()
    {
        var m = Mark();
        if (IsSym("=>") || IsSym("->"))
        {
            var predicate = Mark();
            Advance();
            Close(predicate, LangiumKinds.Predicate);
        }
        var feature = Mark();
        Advance();
        Close(feature, LangiumKinds.Feature);
        var op = Mark();
        Advance();
        Close(op, LangiumKinds.Operator);
        ParseAssignableTerminal();
        Close(m, LangiumKinds.Assignment);
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
                Close(alternatives, LangiumKinds.Alternatives);
            }
            ExpectSym(")");
            Close(m, LangiumKinds.ParenthesizedElement);
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
            Close(multi, LangiumKinds.Multi);
        }
        ExpectIdent("a type name");
        if (IsSym("|") || IsSym(":"))
        {
            var separator = Mark();
            Advance();
            Close(separator, LangiumKinds.Separator);
            if (IsString()) ParseKeyword();
            else ParseRuleCall();
        }
        ExpectSym("]");
        Close(m, LangiumKinds.CrossReference);
    }

    private void ParseAbstractTerminal()
    {
        var m = Mark();
        if (IsSym("=>") || IsSym("->"))
        {
            var predicate = Mark();
            Advance();
            Close(predicate, LangiumKinds.Predicate);
            if (IsSym("("))
            {
                Advance();
                ParseAlternatives();
                ExpectSym(")");
                Close(m, LangiumKinds.PredicatedGroup);
            }
            else if (IsString())
            {
                ParseKeyword();
                Close(m, LangiumKinds.PredicatedKeyword);
            }
            else if (IsIdent())
            {
                ParseRuleCall();
                Close(m, LangiumKinds.PredicatedRuleCall);
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
            Close(m, LangiumKinds.EndOfFile);
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
            Close(m, LangiumKinds.ParenthesizedElement);
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
        Close(m, LangiumKinds.Keyword);
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
            Close(arguments, LangiumKinds.ArgumentList);
        }
        Close(m, LangiumKinds.RuleCall);
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
        Close(m, LangiumKinds.NamedArgument);
    }

    // ---------------------------------------------------------------- conditions: '|' < '&' < '!' < atom

    private void ParseDisjunction() => ParseBinaryCondition(LangiumKinds.Disjunction, "|", ParseConjunction);
    private void ParseConjunction() => ParseBinaryCondition(LangiumKinds.Conjunction, "&", ParseNegation);

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
        Close(m, LangiumKinds.Negation);
    }

    private void ParseAtom()
    {
        var m = Mark();
        if (IsKw("true") || IsKw("false"))
        {
            Advance();
            Close(m, LangiumKinds.BooleanLiteral);
        }
        else if (IsIdent())
        {
            Advance();
            Close(m, LangiumKinds.ParameterReference);
        }
        else if (IsSym("("))
        {
            Advance();
            ParseDisjunction();
            ExpectSym(")");
            Close(m, LangiumKinds.ParenthesizedCondition);
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
        Close(m, LangiumKinds.TerminalAlternatives);
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
        Close(m, LangiumKinds.TerminalGroup);
    }

    private bool IsTerminalTokenStart() =>
        Cur.Kind is LangiumKinds.IdentifierToken or LangiumKinds.StringLiteralToken or LangiumKinds.RegexLiteralToken || IsSym("(") || IsSym("!") || IsSym("->") || IsSym(".");

    private void ParseTerminalToken()
    {
        var m = Mark();
        ParseTerminalTokenElement();
        if (IsSym("?") || IsSym("*") || IsSym("+"))
        {
            Advance();
            Close(m, LangiumKinds.TerminalCardinality);
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
            Close(m, LangiumKinds.CharacterRange);
        }
        else if (IsIdent())
        {
            Advance();
            Close(m, LangiumKinds.TerminalRuleCall);
        }
        else if (Cur.Kind == LangiumKinds.RegexLiteralToken)
        {
            Advance();
            Close(m, LangiumKinds.RegexToken);
        }
        else if (IsSym("."))
        {
            Advance();
            Close(m, LangiumKinds.Wildcard);
        }
        else if (IsSym("!") || IsSym("->"))
        {
            var kind = IsSym("!") ? LangiumKinds.NegatedToken : LangiumKinds.UntilToken;
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
                Close(lookahead, LangiumKinds.Lookahead);
            }
            ParseTerminalAlternatives();
            ExpectSym(")");
            Close(m, LangiumKinds.ParenthesizedTerminal);
        }
        else
        {
            throw Fail("a terminal element");
        }
    }
}
