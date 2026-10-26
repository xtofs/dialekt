namespace Dialekt.Grammar;

using Dialekt.Green;
using Dialekt.Langium;
using Dialekt.Syntax;

/// <summary>
/// Builds the <see cref="GrammarModel"/> from the syntax tree of a Langium grammar. Everything is read by slot name (see
/// <c>LangiumSchema</c>), so a part that is not there shows up as an absent node and is simply skipped.
/// </summary>
public static class GrammarModelBuilder
{
    private static readonly HashSet<string> PrimitiveTypes = ["string", "number", "boolean", "Date", "bigint"];

    public static GrammarBuildResult Build(SyntaxTree tree) => new Builder(tree).Build();

    private sealed class Builder(SyntaxTree tree)
    {
        private readonly List<GrammarDiagnostic> _diagnostics = [];
        private readonly Dictionary<string, RuleKind> _ruleKinds = [];
        private readonly HashSet<string> _typeNames = [];
        private bool _hasImports;

        public GrammarBuildResult Build()
        {
            var root = tree.Root;
            var members = root.GetSlot("Members").Children.ToList();
            _hasImports = members.Any(m => m.Kind == LangiumKinds.Import);

            // Rule names are known before any body is read, so that a rule may call one that is declared later.
            var declared = new HashSet<string>();
            foreach (var member in members)
            {
                if (RuleNameOf(member) is { } name)
                {
                    if (!declared.Add(name.Text))
                    {
                        Report(GrammarSeverity.Error, GrammarDiagnosticCodes.DuplicateRule, $"The rule '{name.Text}' is declared more than once.", name);
                    }
                    else
                    {
                        _ruleKinds[name.Text] = KindOfRule(member);
                    }
                }
                else if (member.Kind is LangiumKinds.Interface or LangiumKinds.TypeAlias)
                {
                    _typeNames.Add(member.GetSlot("Name").Text);
                }
            }

            var rules = new List<GrammarRule>();
            var types = new List<TypeDeclaration>();
            var imports = new List<string>();
            var seen = new HashSet<string>();
            foreach (var member in members)
            {
                switch (member.Kind)
                {
                    case LangiumKinds.ParserRule:
                        if (seen.Add(member.GetSlot("Name").Text)) rules.Add(ParserRuleOf(member));
                        break;
                    case LangiumKinds.TerminalRule:
                        if (seen.Add(member.GetSlot("Name").Text)) rules.Add(TerminalRuleOf(member));
                        break;
                    case LangiumKinds.InfixRule:
                        Report(GrammarSeverity.Warning, GrammarDiagnosticCodes.Unsupported, "Infix rules are not supported yet.", member);
                        if (seen.Add(member.GetSlot("Name").Text)) rules.Add(new InfixRule(member.GetSlot("Name").Text));
                        break;
                    case LangiumKinds.Interface:
                        types.Add(InterfaceOf(member));
                        break;
                    case LangiumKinds.TypeAlias:
                        types.Add(new TypeAlias(member.GetSlot("Name").Text, member.GetSlot("Type").Text));
                        break;
                    case LangiumKinds.Import:
                        Report(GrammarSeverity.Warning, GrammarDiagnosticCodes.Unsupported, "Imports are not supported yet.", member);
                        imports.Add(Unquote(member.GetSlot("Path").Text));
                        break;
                    default: // SyntaxKind.Error
                        Report(GrammarSeverity.Error, GrammarDiagnosticCodes.SyntaxError, "This declaration has syntax errors and is ignored.", member);
                        break;
                }
            }

            var declaration = root.GetSlot("Declaration");
            var model = new GrammarModel(
                declaration.IsAbsent ? null : declaration.GetSlot("Name").Text,
                rules.ToEquatableArray(), types.ToEquatableArray(), imports.ToEquatableArray());
            CheckEntryRule(model, root);
            return new GrammarBuildResult(model, _diagnostics.OrderBy(d => d.Line).ThenBy(d => d.Column).ToList());
        }

        private void CheckEntryRule(GrammarModel model, SyntaxNode root)
        {
            var entries = model.Rules.OfType<ParserRule>().Where(r => r.IsEntry).ToList();
            if (entries.Count == 0)
            {
                // Only the types file of a grammar has no entry rule; whoever needs one (the generator) decides if that is an error.
                Report(GrammarSeverity.Warning, GrammarDiagnosticCodes.NoEntryRule, "The grammar has no entry rule.", root);
            }
            else if (entries.Count > 1)
            {
                Report(GrammarSeverity.Error, GrammarDiagnosticCodes.MultipleEntryRules,
                    $"The grammar has more than one entry rule: {string.Join(", ", entries.Select(e => e.Name))}.", root);
            }
        }

        // ------------------------------------------------------------ declarations

        private static SyntaxNode? RuleNameOf(SyntaxNode member) =>
            member.Kind is LangiumKinds.ParserRule or LangiumKinds.TerminalRule or LangiumKinds.InfixRule ? member.GetSlot("Name") : null;

        private static RuleKind KindOfRule(SyntaxNode member)
        {
            switch (member.Kind)
            {
                case LangiumKinds.ParserRule:
                    return ModifierIs(member.GetSlot("Modifier"), "fragment") ? RuleKind.ParserFragment : RuleKind.ParserRule;
                case LangiumKinds.TerminalRule:
                    return member.GetSlot("FragmentModifier").IsAbsent ? RuleKind.TerminalRule : RuleKind.TerminalFragment;
                default:
                    return RuleKind.InfixRule;
            }
        }

        private static bool ModifierIs(SyntaxNode modifier, string keyword) => !modifier.IsAbsent && modifier.GetSlot("Keyword").Text == keyword;

        private ParserRule ParserRuleOf(SyntaxNode node)
        {
            var modifier = node.GetSlot("Modifier");
            var parameters = node.GetSlot("Parameters");
            var names = parameters.IsAbsent ? [] : Items(parameters.GetSlot("Parameters")).Select(p => p.GetSlot("Name").Text).ToList();
            if (names.Count > 0)
            {
                Report(GrammarSeverity.Warning, GrammarDiagnosticCodes.Unsupported, "Rule parameters are not supported yet.", parameters);
            }
            var returnType = node.GetSlot("ReturnType");
            var inferredType = node.GetSlot("InferredType");
            return new ParserRule(
                node.GetSlot("Name").Text,
                ModifierIs(modifier, "entry"),
                ModifierIs(modifier, "fragment"),
                names.ToEquatableArray(),
                returnType.IsAbsent ? null : returnType.GetSlot("Name").Text,
                inferredType.IsAbsent ? null : inferredType.GetSlot("Name").Text,
                ElementOf(node.GetSlot("Definition")));
        }

        private TerminalRule TerminalRuleOf(SyntaxNode node)
        {
            var returnType = node.GetSlot("ReturnType");
            return new TerminalRule(
                node.GetSlot("Name").Text,
                !node.GetSlot("HiddenModifier").IsAbsent,
                !node.GetSlot("FragmentModifier").IsAbsent,
                returnType.IsAbsent ? null : returnType.GetSlot("Name").Text,
                TerminalOf(node.GetSlot("Definition")));
        }

        private static InterfaceDeclaration InterfaceOf(SyntaxNode node) => new(
            node.GetSlot("Name").Text,
            Items(node.GetSlot("SuperTypes")).Select(s => s.GetSlot("Name").Text).ToEquatableArray(),
            node.GetSlot("Attributes").Children
                .Where(a => a.Kind == LangiumKinds.Attribute)
                .Select(a => new AttributeDeclaration(a.GetSlot("Name").Text, !a.GetSlot("Optional").IsAbsent, a.GetSlot("Type").Text))
                .ToEquatableArray());

        // ------------------------------------------------------------ parser rule bodies

        private GrammarElement ElementOf(SyntaxNode node)
        {
            switch (node.Kind)
            {
                case LangiumKinds.Alternatives:
                    return new AlternativesElement(Items(node.GetSlot("Items")).Select(ElementOf).ToEquatableArray());

                case LangiumKinds.Group:
                    return new GroupElement(node.GetSlot("Elements").Children.Select(ElementOf).ToEquatableArray());

                case LangiumKinds.UnorderedGroup:
                    Report(GrammarSeverity.Warning, GrammarDiagnosticCodes.Unsupported, "Unordered groups (&) are not supported yet.", node);
                    return new UnorderedGroupElement(Items(node.GetSlot("Items")).Select(ElementOf).ToEquatableArray());

                case LangiumKinds.ConditionalBranch:
                    Report(GrammarSeverity.Warning, GrammarDiagnosticCodes.Unsupported, "Guarded alternatives (<condition> ...) are not supported yet.", node);
                    return new GuardedElement(node.GetSlot("Elements").Children.Select(ElementOf).ToEquatableArray());

                case LangiumKinds.Cardinality:
                    return new RepetitionElement(ElementOf(node.GetSlot("Element")), CardinalityOf(node.GetSlot("Operator")));

                case LangiumKinds.Assignment:
                    {
                        var terminal = ElementOf(node.GetSlot("Terminal"));
                        var assignment = new AssignmentElement(
                            node.GetSlot("Feature").GetSlot("Name").Text, AssignmentOperatorOf(node.GetSlot("Operator").GetSlot("Token")), terminal);
                        if (node.GetSlot("Predicate").IsAbsent) return assignment;
                        Report(GrammarSeverity.Warning, GrammarDiagnosticCodes.Unsupported, "Syntactic predicates (=>, ->) are not supported yet.", node);
                        return new PredicatedElement(assignment);
                    }

                case LangiumKinds.PredicatedKeyword:
                    Report(GrammarSeverity.Warning, GrammarDiagnosticCodes.Unsupported, "Syntactic predicates (=>, ->) are not supported yet.", node);
                    return new PredicatedElement(ElementOf(node.GetSlot("Keyword")));

                case LangiumKinds.PredicatedRuleCall:
                    Report(GrammarSeverity.Warning, GrammarDiagnosticCodes.Unsupported, "Syntactic predicates (=>, ->) are not supported yet.", node);
                    return new PredicatedElement(ElementOf(node.GetSlot("Call")));

                case LangiumKinds.PredicatedGroup:
                    Report(GrammarSeverity.Warning, GrammarDiagnosticCodes.Unsupported, "Syntactic predicates (=>, ->) are not supported yet.", node);
                    return new PredicatedElement(ElementOf(node.GetSlot("Content")));

                case LangiumKinds.Action:
                    {
                        Report(GrammarSeverity.Warning, GrammarDiagnosticCodes.Unsupported, "Actions ({...}) are not supported yet.", node);
                        var type = node.GetSlot("Type");
                        var inferred = node.GetSlot("InferredType");
                        var feature = node.GetSlot("Feature");
                        return new ActionElement(
                            type.IsAbsent ? null : type.Text,
                            inferred.IsAbsent ? null : inferred.GetSlot("Name").Text,
                            feature.IsAbsent ? null : feature.GetSlot("Name").Text);
                    }

                case LangiumKinds.CrossReference:
                    {
                        var typeName = node.GetSlot("Type").Text;
                        var terminal = node.GetSlot("Terminal");
                        if (!_ruleKinds.ContainsKey(typeName) && !_typeNames.Contains(typeName))
                        {
                            ReportUnresolved(GrammarDiagnosticCodes.UnresolvedType, $"The type '{typeName}' is not declared.", node.GetSlot("Type"));
                        }
                        return new CrossReferenceElement(
                            typeName,
                            _ruleKinds.GetValueOrDefault(typeName, RuleKind.Unresolved),
                            !node.GetSlot("Multi").IsAbsent,
                            terminal.IsAbsent ? null : ElementOf(terminal));
                    }

                case LangiumKinds.EndOfFile:
                    return new EndOfFileElement();

                case LangiumKinds.Keyword:
                    return new KeywordElement(Unquote(node.GetSlot("Value").Text));

                case LangiumKinds.RuleCall:
                    {
                        var arguments = node.GetSlot("Arguments");
                        var texts = arguments.IsAbsent ? [] : Items(arguments.GetSlot("Arguments")).Select(a => a.Text).ToList();
                        if (texts.Count > 0)
                        {
                            Report(GrammarSeverity.Warning, GrammarDiagnosticCodes.Unsupported, "Rule arguments are not supported yet.", arguments);
                        }
                        return new RuleCallElement(ResolveRule(node.GetSlot("Name")), KindOf(node.GetSlot("Name").Text), texts.ToEquatableArray());
                    }

                case LangiumKinds.ParenthesizedElement:
                    return ElementOf(node.GetSlot("Content"));

                default:
                    throw new InvalidOperationException($"Unexpected {tree.Language.GetKindName(node.Kind)} in a rule body.");
            }
        }

        // ------------------------------------------------------------ terminal rule bodies

        private TerminalElement TerminalOf(SyntaxNode node)
        {
            switch (node.Kind)
            {
                case LangiumKinds.TerminalAlternatives:
                    return new TerminalAlternatives(Items(node.GetSlot("Items")).Select(TerminalOf).ToEquatableArray());

                case LangiumKinds.TerminalGroup:
                    return new TerminalGroup(node.GetSlot("Elements").Children.Select(TerminalOf).ToEquatableArray());

                case LangiumKinds.TerminalCardinality:
                    return new TerminalRepetition(TerminalOf(node.GetSlot("Element")), CardinalityOf(node.GetSlot("Operator")));

                case LangiumKinds.CharacterRange:
                    {
                        var right = node.GetSlot("Right");
                        return new CharacterRange(Unquote(node.GetSlot("Left").GetSlot("Value").Text), right.IsAbsent ? null : Unquote(right.GetSlot("Value").Text));
                    }

                case LangiumKinds.TerminalRuleCall:
                    return new TerminalRuleCall(ResolveRule(node.GetSlot("Name")), KindOf(node.GetSlot("Name").Text));

                case LangiumKinds.RegexToken:
                    {
                        var literal = node.GetSlot("Regex").Text;
                        var closing = literal.LastIndexOf('/');
                        return new RegexTerminal(literal[1..closing], literal[(closing + 1)..]);
                    }

                case LangiumKinds.Wildcard:
                    return new WildcardTerminal();

                case LangiumKinds.NegatedToken:
                    return new NegatedTerminal(TerminalOf(node.GetSlot("Element")));

                case LangiumKinds.UntilToken:
                    return new UntilTerminal(TerminalOf(node.GetSlot("Element")));

                case LangiumKinds.ParenthesizedTerminal:
                    {
                        var content = TerminalOf(node.GetSlot("Content"));
                        var lookahead = node.GetSlot("Lookahead");
                        return lookahead.IsAbsent ? content : new LookaheadTerminal(lookahead.GetSlot("Token").Text, content);
                    }

                default:
                    throw new InvalidOperationException($"Unexpected {tree.Language.GetKindName(node.Kind)} in a terminal rule.");
            }
        }

        // ------------------------------------------------------------ helpers

        /// <summary>The name of the called rule; reports a rule that does not exist.</summary>
        private string ResolveRule(SyntaxNode nameToken)
        {
            var name = nameToken.Text;
            if (!_ruleKinds.ContainsKey(name))
            {
                ReportUnresolved(GrammarDiagnosticCodes.UnresolvedRule, $"The rule '{name}' is not declared.", nameToken);
            }
            return name;
        }

        private RuleKind KindOf(string ruleName) => _ruleKinds.GetValueOrDefault(ruleName, RuleKind.Unresolved);

        /// <summary>The items of a (separated) list; for a separated list the separator tokens are left out.</summary>
        private static IEnumerable<SyntaxNode> Items(SyntaxNode list) =>
            list.Kind == SyntaxKind.SeparatedList ? list.Children.Where((_, index) => index % 2 == 0) : list.Children;

        private static Cardinality CardinalityOf(SyntaxNode token) => token.Text switch
        {
            "?" => Cardinality.Optional,
            "*" => Cardinality.ZeroOrMore,
            _ => Cardinality.OneOrMore,
        };

        private static AssignmentOperator AssignmentOperatorOf(SyntaxNode token) => token.Text switch
        {
            "=" => AssignmentOperator.Assign,
            "+=" => AssignmentOperator.Append,
            _ => AssignmentOperator.BooleanAssign,
        };

        /// <summary>
        /// A name nobody declares is an error, unless the grammar imports other files: the name may be declared there, and
        /// imports are not followed yet.
        /// </summary>
        private void ReportUnresolved(string code, string message, SyntaxNode at)
        {
            if (_hasImports)
            {
                Report(GrammarSeverity.Warning, code, message + " It may come from an import, which is not supported yet.", at);
            }
            else
            {
                Report(GrammarSeverity.Error, code, message, at);
            }
        }

        private void Report(GrammarSeverity severity, string code, string message, SyntaxNode at)
        {
            var (line, column) = at.StartLineAndColumn;
            _diagnostics.Add(new GrammarDiagnostic(severity, code, message, line, column));
        }
    }

    /// <summary>The text of a string literal token without its quotes and with escape sequences resolved.</summary>
    internal static string Unquote(string literal)
    {
        var body = literal.AsSpan(1, literal.Length - 2);
        var result = new System.Text.StringBuilder(body.Length);
        for (var i = 0; i < body.Length; i++)
        {
            if (body[i] != '\\' || i + 1 == body.Length)
            {
                result.Append(body[i]);
                continue;
            }
            var c = body[++i];
            switch (c)
            {
                case 'n': result.Append('\n'); break;
                case 'r': result.Append('\r'); break;
                case 't': result.Append('\t'); break;
                case 'b': result.Append('\b'); break;
                case 'f': result.Append('\f'); break;
                case 'v': result.Append('\v'); break;
                case '0': result.Append('\0'); break;
                case 'u' when i + 4 < body.Length + 0 && int.TryParse(body.Slice(i + 1, 4), System.Globalization.NumberStyles.HexNumber, null, out var code):
                    result.Append((char)code);
                    i += 4;
                    break;
                default: result.Append(c); break;
            }
        }
        return result.ToString();
    }
}
