namespace Dialekt;

using Dialekt.Scanning;

/// <summary>
/// Recursive-descent parser for Langium grammar and type files. Produces a generic <see cref="Node"/> tree.
/// Syntax errors are recorded as diagnostics; the parser then skips to the next declaration (or, inside an
/// interface body, the next attribute) so that later errors are still reported.
/// </summary>
public sealed class Parser
{
    private sealed class ParseException : Exception;

    private readonly List<Token> _tokens = [];
    private readonly List<Diagnostic> _diagnostics = [];
    private int _pos;

    private Parser(string content)
    {
        foreach (var token in new Scanner().Scan(content))
        {
            if (token.Kind == Token.ErrorKind)
            {
                _diagnostics.Add(new Diagnostic(token.Error!, token.Span.Start.Line, token.Span.Start.Column, token.Text.Length, true));
            }
            if (!token.IsTrivia)
            {
                _tokens.Add(token);
            }
        }
    }

    public static ParseResult Parse(string content)
    {
        var parser = new Parser(content);
        var root = parser.ParseGrammar();
        return new ParseResult(root, parser._diagnostics.OrderBy(d => d.Line).ThenBy(d => d.Column).ToList());
    }

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

    private Token Advance()
    {
        var t = Cur;
        if (!AtEof)
        {
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

    private Node Begin(string kind, string? name = null, Token? at = null) => new(kind, (at ?? Cur).Span with { End = (at ?? Cur).Span.Start }, name);

    private Node Finish(Node node)
    {
        var end = _pos > 0 ? _tokens[_pos - 1].Span.End : node.Span.Start;
        node.Span = node.Span with { End = end.Offset < node.Span.Start.Offset ? node.Span.Start : end };
        return node;
    }

    private static Node Leaf(string kind, Token token, string? name = null) => new(kind, token.Span, name ?? token.Text);

    // ---------------------------------------------------------------- file level

    private Node ParseGrammar()
    {
        var root = Begin("Grammar");
        try
        {
            if (IsKw("grammar") && IsIdent(1))
            {
                var decl = Begin("GrammarDeclaration");
                Advance();
                decl.Name = Advance().Text;
                root.Add(Finish(decl));
            }
        }
        catch (ParseException) { }

        while (!AtEof)
        {
            var before = _pos;
            try
            {
                root.Add(ParseDeclaration());
            }
            catch (ParseException)
            {
                root.Add(Recover(before));
            }
        }
        if (root.Children.Count > 0)
        {
            Finish(root);
        }
        else
        {
            root.Span = root.Span with { End = Cur.Span.End };
        }
        return root;
    }

    private Node ParseDeclaration()
    {
        if (IsKw("import")) return ParseImport();
        if (IsKw("interface")) return ParseInterface();
        if (IsKw("type") && IsIdent(1)) return ParseTypeAlias();
        if (IsKw("infix") && IsIdent(1)) return ParseInfixRule();
        if (IsKw("terminal") || (IsKw("hidden") && IsKw("terminal", 1))) return ParseTerminalRule();
        if (IsKw("entry") || IsKw("fragment") || IsIdent()) return ParseParserRule();
        throw Fail("a rule, interface, type or import");
    }

    /// <summary>
    /// Skips to the end of the broken declaration: past the next top-level ';', or up to a token in column 1
    /// (where the next declaration most likely starts). Always makes progress.
    /// </summary>
    private Node Recover(int before)
    {
        var error = Begin("Error", at: _tokens[before]);
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
                    case ";" when depth <= 0: return Finish(error);
                }
            }
        }
        return Finish(error);
    }

    private Node ParseImport()
    {
        var node = Begin("Import");
        Advance();
        node.Name = ExpectString("an import path").Text;
        Accept(";");
        return Finish(node);
    }

    // ---------------------------------------------------------------- types

    private Node ParseInterface()
    {
        var node = Begin("Interface");
        Advance();
        node.Name = ExpectIdent("an interface name").Text;
        if (AcceptKw("extends"))
        {
            do
            {
                var t = ExpectIdent("a super type name");
                node.Add(Leaf("SuperType", t));
            } while (Accept(","));
        }
        ExpectSym("{");
        while (!IsSym("}") && !AtEof)
        {
            var before = _pos;
            try
            {
                node.Add(ParseTypeAttribute());
            }
            catch (ParseException)
            {
                node.Add(RecoverAttribute(before));
            }
        }
        ExpectSym("}");
        Accept(";");
        return Finish(node);
    }

    private Node RecoverAttribute(int before)
    {
        var error = Begin("Error", at: _tokens[before]);
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
        return Finish(error);
    }

    private Node ParseTypeAttribute()
    {
        var node = Begin("Attribute");
        node.Name = ExpectIdent("an attribute name").Text;
        if (IsSym("?"))
        {
            node.Add(Leaf("Optional", Advance()));
        }
        ExpectSym(":");
        node.Add(ParseTypeDefinition());
        if (Accept("="))
        {
            node.Add(ParseValueLiteral());
        }
        Accept(";");
        return Finish(node);
    }

    private Node ParseValueLiteral()
    {
        var node = Begin("ValueLiteral");
        if (IsString())
        {
            return Leaf("StringLiteral", Advance());
        }
        if (Cur.Kind == "Number")
        {
            return Leaf("NumberLiteral", Advance());
        }
        if (IsKw("true") || IsKw("false"))
        {
            return Leaf("BooleanLiteral", Advance());
        }
        if (IsSym("["))
        {
            node = Begin("ArrayLiteral");
            Advance();
            if (!IsSym("]"))
            {
                do
                {
                    node.Add(ParseValueLiteral());
                } while (Accept(","));
            }
            ExpectSym("]");
            return Finish(node);
        }
        throw Fail("a value (string, number, boolean or array)");
    }

    private Node ParseTypeAlias()
    {
        var node = Begin("Type");
        Advance();
        node.Name = ExpectIdent("a type name").Text;
        ExpectSym("=");
        node.Add(ParseTypeDefinition());
        Accept(";");
        return Finish(node);
    }

    // union < array < reference/simple
    private Node ParseTypeDefinition()
    {
        var start = Cur;
        var first = ParseArrayType();
        if (!IsSym("|")) return first;
        var union = Begin("UnionType", at: start).Add(first);
        while (Accept("|"))
        {
            union.Add(ParseArrayType());
        }
        return Finish(union);
    }

    private Node ParseArrayType()
    {
        var start = Cur;
        var type = ParseReferenceType();
        while (IsSym("[") && IsSym("]", 1))
        {
            var array = Begin("ArrayType", at: start).Add(type);
            Advance();
            Advance();
            type = Finish(array);
        }
        return type;
    }

    private Node ParseReferenceType()
    {
        if (!IsSym("@")) return ParseSimpleType();
        var node = Begin("ReferenceType");
        Advance();
        node.Add(ParseSimpleType());
        if (IsSym("+"))
        {
            node.Add(Leaf("Multi", Advance()));
        }
        return Finish(node);
    }

    private Node ParseSimpleType()
    {
        if (IsSym("("))
        {
            var node = Begin("Parenthesized");
            Advance();
            node.Add(ParseTypeDefinition());
            ExpectSym(")");
            return Finish(node);
        }
        if (IsIdent()) return Leaf("SimpleType", Advance());
        if (IsString()) return Leaf("StringType", Advance());
        throw Fail("a type");
    }

    // ---------------------------------------------------------------- rules

    private Node ParseParserRule()
    {
        var node = Begin("ParserRule");
        if (IsKw("entry") || (IsKw("fragment") && IsIdent(1)))
        {
            node.Add(Leaf("Modifier", Advance()));
        }
        node.Name = ExpectIdent("a rule name").Text;
        ParseParameters(node);
        ParseReturnsOrInfers(node);
        ExpectSym(":");
        node.Add(ParseAlternatives());
        ExpectSym(";");
        return Finish(node);
    }

    private Node ParseInfixRule()
    {
        var node = Begin("InfixRule");
        Advance();
        node.Name = ExpectIdent("a rule name").Text;
        ParseParameters(node);
        ExpectKw("on");
        node.Add(ParseRuleCall());
        ParseReturnsOrInfers(node);
        ExpectSym(":");

        var operators = Begin("InfixRuleOperators");
        do
        {
            var list = Begin("InfixRuleOperatorList");
            if (IsKw("left") || IsKw("right"))
            {
                list.Add(Leaf("Associativity", Advance()));
                ExpectKw("assoc");
            }
            do
            {
                list.Add(ParseKeyword());
            } while (Accept("|"));
            operators.Add(Finish(list));
        } while (Accept(">"));
        node.Add(Finish(operators));

        ExpectSym(";");
        return Finish(node);
    }

    private void ParseParameters(Node rule)
    {
        if (!Accept("<")) return;
        if (!IsSym(">"))
        {
            do
            {
                rule.Add(Leaf("Parameter", ExpectIdent("a parameter name")));
            } while (Accept(","));
        }
        ExpectSym(">");
    }

    private void ParseReturnsOrInfers(Node rule)
    {
        if (IsKw("returns"))
        {
            Advance();
            rule.Add(Leaf("ReturnType", ExpectIdent("a return type")));
        }
        else if (IsKw("infers"))
        {
            Advance();
            rule.Add(Leaf("InferredType", ExpectIdent("a type name")));
        }
    }

    private Node ParseTerminalRule()
    {
        var node = Begin("TerminalRule");
        if (IsKw("hidden"))
        {
            node.Add(Leaf("Modifier", Advance()));
        }
        ExpectKw("terminal");
        if (IsKw("fragment"))
        {
            node.Add(Leaf("Modifier", Advance()));
            node.Name = ExpectIdent("a rule name").Text;
        }
        else
        {
            node.Name = ExpectIdent("a rule name").Text;
            if (AcceptKw("returns"))
            {
                node.Add(Leaf("ReturnType", ExpectIdent("a return type")));
            }
        }
        ExpectSym(":");
        node.Add(ParseTerminalAlternatives());
        ExpectSym(";");
        return Finish(node);
    }

    // ---------------------------------------------------------------- parser rule bodies
    // Precedence (loosest to tightest): alternatives '|' < unordered group '&' < group (juxtaposition) < cardinality

    private Node ParseAlternatives()
    {
        var start = Cur;
        var first = ParseConditionalBranch();
        if (!IsSym("|")) return first;
        var node = Begin("Alternatives", at: start).Add(first);
        while (Accept("|"))
        {
            node.Add(ParseConditionalBranch());
        }
        return Finish(node);
    }

    private Node ParseConditionalBranch()
    {
        if (!IsSym("<")) return ParseUnorderedGroup();
        var node = Begin("ConditionalBranch");
        Advance();
        var guard = Begin("Guard");
        guard.Add(ParseDisjunction());
        node.Add(Finish(guard));
        ExpectSym(">");
        do
        {
            node.Add(ParseAbstractToken());
        } while (IsAbstractTokenStart());
        return Finish(node);
    }

    private Node ParseUnorderedGroup()
    {
        var start = Cur;
        var first = ParseGroup();
        if (!IsSym("&")) return first;
        var node = Begin("UnorderedGroup", at: start).Add(first);
        while (Accept("&"))
        {
            node.Add(ParseGroup());
        }
        return Finish(node);
    }

    private Node ParseGroup()
    {
        var start = Cur;
        var first = ParseAbstractToken();
        if (!IsAbstractTokenStart()) return first;
        var node = Begin("Group", at: start).Add(first);
        while (IsAbstractTokenStart())
        {
            node.Add(ParseAbstractToken());
        }
        return Finish(node);
    }

    private bool IsAbstractTokenStart() =>
        Cur.Kind is "Identifier" or "StringLiteral" || IsSym("(") || IsSym("{") || IsSym("=>") || IsSym("->");

    private static bool IsAssignmentOperator(Token t) => t.IsSymbol && t.Text is "=" or "+=" or "?=";

    private Node ParseAbstractToken()
    {
        if (IsSym("{")) return ParseAction();

        var start = Cur;
        Node element;
        var hasPredicate = IsSym("=>") || IsSym("->");
        var i = hasPredicate ? 1 : 0;
        if (IsIdent(i) && IsAssignmentOperator(Peek(i + 1)))
        {
            element = ParseAssignment();
        }
        else
        {
            element = ParseAbstractTerminal();
        }

        if (IsSym("?") || IsSym("*") || IsSym("+"))
        {
            var card = Begin("Cardinality", Cur.Text, start).Add(element);
            Advance();
            return Finish(card);
        }
        return element;
    }

    private Node ParseAction()
    {
        var node = Begin("Action");
        Advance();
        if (IsKw("infer") && IsIdent(1))
        {
            var inferred = Begin("InferredType");
            Advance();
            inferred.Name = Advance().Text;
            node.Add(Finish(inferred));
        }
        else
        {
            node.Name = ExpectIdent("a type name").Text;
        }
        if (Accept("."))
        {
            node.Add(Leaf("Feature", ExpectIdent("a feature name")));
            if (!IsAssignmentOperator(Cur) || IsSym("?="))
            {
                throw Fail("'=' or '+='");
            }
            node.Add(Leaf("Operator", Advance()));
            ExpectKw("current");
        }
        ExpectSym("}");
        return Finish(node);
    }

    private Node ParseAssignment()
    {
        var node = Begin("Assignment");
        if (IsSym("=>") || IsSym("->"))
        {
            node.Add(Leaf("Predicate", Advance()));
        }
        node.Name = Advance().Text;
        node.Add(Leaf("Operator", Advance()));
        node.Add(ParseAssignableTerminal());
        return Finish(node);
    }

    private Node ParseAssignableTerminal()
    {
        if (IsString()) return ParseKeyword();
        if (IsIdent()) return ParseRuleCall();
        if (IsSym("("))
        {
            var node = Begin("Parenthesized");
            Advance();
            var start = Cur;
            var first = ParseAssignableTerminal();
            if (IsSym("|"))
            {
                var alts = Begin("Alternatives", at: start).Add(first);
                while (Accept("|"))
                {
                    alts.Add(ParseAssignableTerminal());
                }
                first = Finish(alts);
            }
            node.Add(first);
            ExpectSym(")");
            return Finish(node);
        }
        if (IsSym("[")) return ParseCrossReference();
        throw Fail("a keyword, rule call, '(' or cross reference");
    }

    private Node ParseCrossReference()
    {
        var node = Begin("CrossReference");
        Advance();
        if (IsSym("+"))
        {
            node.Add(Leaf("Multi", Advance()));
        }
        node.Name = ExpectIdent("a type name").Text;
        if (IsSym("|") || IsSym(":"))
        {
            node.Add(Leaf("Separator", Advance()));
            node.Add(IsString() ? ParseKeyword() : ParseRuleCall());
        }
        ExpectSym("]");
        return Finish(node);
    }

    private Node ParseAbstractTerminal()
    {
        if (IsSym("=>") || IsSym("->"))
        {
            var predicate = Leaf("Predicate", Cur);
            var node = IsSym("(", 1) ? Begin("PredicatedGroup") : null;
            Advance();
            if (node is not null)
            {
                Advance();
                node.Add(predicate).Add(ParseAlternatives());
                ExpectSym(")");
                return Finish(node);
            }
            var inner = IsString() ? ParseKeyword() : IsIdent() ? ParseRuleCall() : throw Fail("a keyword, rule call or '('");
            inner.Children.Insert(0, predicate);
            inner.Span = inner.Span with { Start = predicate.Span.Start };
            return inner;
        }
        if (IsString()) return ParseKeyword();
        if (IsKw("EOF")) return Leaf("EndOfFile", Advance());
        if (IsIdent()) return ParseRuleCall();
        if (IsSym("("))
        {
            var node = Begin("Parenthesized");
            Advance();
            node.Add(ParseAlternatives());
            ExpectSym(")");
            return Finish(node);
        }
        throw Fail("a keyword, rule call or '('");
    }

    private Node ParseKeyword() => Leaf("Keyword", ExpectString("a keyword string"));

    private Node ParseRuleCall()
    {
        var node = Begin("RuleCall");
        node.Name = ExpectIdent("a rule name").Text;
        if (Accept("<"))
        {
            do
            {
                node.Add(ParseNamedArgument());
            } while (Accept(","));
            ExpectSym(">");
        }
        return Finish(node);
    }

    private Node ParseNamedArgument()
    {
        var node = Begin("NamedArgument");
        if (IsIdent() && IsSym("=", 1))
        {
            node.Name = Advance().Text;
            Advance();
        }
        node.Add(ParseDisjunction());
        return Finish(node);
    }

    // ---------------------------------------------------------------- conditions: '|' < '&' < '!' < atom

    private Node ParseDisjunction() => ParseBinaryCondition("Disjunction", "|", ParseConjunction);
    private Node ParseConjunction() => ParseBinaryCondition("Conjunction", "&", ParseNegation);

    private Node ParseBinaryCondition(string kind, string op, Func<Node> operand)
    {
        var start = Cur;
        var left = operand();
        while (IsSym(op))
        {
            var node = Begin(kind, at: start).Add(left);
            Advance();
            node.Add(operand());
            left = Finish(node);
        }
        return left;
    }

    private Node ParseNegation()
    {
        if (!IsSym("!")) return ParseAtom();
        var node = Begin("Negation");
        Advance();
        node.Add(ParseNegation());
        return Finish(node);
    }

    private Node ParseAtom()
    {
        if (IsKw("true") || IsKw("false")) return Leaf("BooleanLiteral", Advance());
        if (IsIdent()) return Leaf("ParameterReference", Advance());
        if (IsSym("("))
        {
            var node = Begin("Parenthesized");
            Advance();
            node.Add(ParseDisjunction());
            ExpectSym(")");
            return Finish(node);
        }
        throw Fail("a condition");
    }

    // ---------------------------------------------------------------- terminal rule bodies

    private Node ParseTerminalAlternatives()
    {
        var start = Cur;
        var first = ParseTerminalGroup();
        if (!IsSym("|")) return first;
        var node = Begin("TerminalAlternatives", at: start).Add(first);
        while (Accept("|"))
        {
            node.Add(ParseTerminalGroup());
        }
        return Finish(node);
    }

    private Node ParseTerminalGroup()
    {
        var start = Cur;
        var first = ParseTerminalToken();
        if (!IsTerminalTokenStart()) return first;
        var node = Begin("TerminalGroup", at: start).Add(first);
        while (IsTerminalTokenStart())
        {
            node.Add(ParseTerminalToken());
        }
        return Finish(node);
    }

    private bool IsTerminalTokenStart() =>
        Cur.Kind is "Identifier" or "StringLiteral" or "RegexLiteral" || IsSym("(") || IsSym("!") || IsSym("->") || IsSym(".");

    private Node ParseTerminalToken()
    {
        var start = Cur;
        var element = ParseTerminalTokenElement();
        if (IsSym("?") || IsSym("*") || IsSym("+"))
        {
            var card = Begin("Cardinality", Cur.Text, start).Add(element);
            Advance();
            return Finish(card);
        }
        return element;
    }

    private Node ParseTerminalTokenElement()
    {
        if (IsString())
        {
            var range = Begin("CharacterRange").Add(ParseKeyword());
            if (Accept(".."))
            {
                range.Add(ParseKeyword());
            }
            return Finish(range);
        }
        if (IsIdent()) return Leaf("TerminalRuleCall", Advance());
        if (Cur.Kind == "RegexLiteral") return Leaf("RegexToken", Advance());
        if (IsSym(".")) return Leaf("Wildcard", Advance());
        if (IsSym("!") || IsSym("->"))
        {
            var node = Begin(IsSym("!") ? "NegatedToken" : "UntilToken");
            Advance();
            node.Add(ParseTerminalTokenElement());
            return Finish(node);
        }
        if (IsSym("("))
        {
            var node = Begin("Parenthesized");
            Advance();
            if (IsSym("?=") || IsSym("?!") || IsSym("?<=") || IsSym("?<!"))
            {
                node.Add(Leaf("Lookahead", Advance()));
            }
            node.Add(ParseTerminalAlternatives());
            ExpectSym(")");
            return Finish(node);
        }
        throw Fail("a terminal element");
    }
}
