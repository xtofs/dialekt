# Architecture

How dialekt turns source text into trees, and which layer to use for what. The mechanics of the tree layers (slot layouts,
placeholders, adding a node kind) are in [docs/syntax-tree.md](docs/syntax-tree.md).

```
source text -> Scanner -> tokens -> Parser -> green tree (arena) -> red views -> semantic model (planned)
```

## Projects
- **`dialekt.runtime`**: everything that does not depend on a particular language: the green tree and arena, the red views,
  `Language` (kind names, slot layouts), `ParserBase` (tokens, nodes, slots, error handling, incremental reuse), `ITokenizer`,
  diagnostics. Languages, handwritten or generated, build on it.
- **`dialekt`**: the Langium language: `LangiumKinds`, `LangiumScanner`, `LangiumParser`, `LangiumLanguage`/`LangiumSchema`.
- **`console`**: a command line tool that parses files and prints trees.
- **`dialekt.tests`**: tests for all of the above.

A language defines its kinds as constants of the runtime's `SyntaxKind` enum (numbered from `FirstLanguageKind`), a
`Language` object, a tokenizer producing those kinds, and a parser that derives from `ParserBase` with one method per rule.

## Green: structure without place

- **What it is:** immutable storage in a `GreenArena`, hash-consed through `GreenNodeCache`. Nodes have no positions and no
  parents; equal subtrees are the same node.
- **Use it for** questions answered from structure alone:
  - "Did this declaration change?" (handle equality, which the incremental parser relies on)
  - counting, compaction, structural diff
  - memoized pure functions, such as "which rule names does this declaration mention?". The result is cached per handle and
    computed once per unique subtree.
- **Limit:** it cannot say where anything is, what encloses it, or what a slot is called. Slots are only indices.

## Red: the same tree at a position, with names

- **What it is:** a throw-away struct view (`SyntaxNode`) with position, text, line/column and slot access by name
  (`rule.GetSlot("Name")`, checked at run time against the schema). The immediate parent is stored; further ancestors come from one descent from the root.
- **Use it when the answer depends on where you are, or needs the source text:**
  - diagnostics: a range to underline
  - cursor features: `FindToken(offset)` for hover, go-to-definition, completion context
  - editor structure: outline, folding ranges, rename edits (the ranges to replace)
  - formatting and refactoring: positions and trivia
  - reading the tree by name: `rule.GetSlot("Name").Text`, because green has no slot names
  - context: "which rule encloses this node?"
- **Limit:** do not hold or cache views across edits. A view belongs to one tree version.
- Red nodes are a view of the _concrete_ syntax tree (every token and all trivia are there so the tree reproduces the
  source). They are not an abstract syntax tree; meaning belongs to the semantic model.

## Semantic model: meaning (planned)

- **What it is:** derived data, such as which rule a `RuleCall` refers to, duplicate or unused rules, resolved types, and which
  type an `Assignment` feature belongs to.
- **How it is built:** it reads the tree through red views, because computing it needs names and sometimes positions, and it
  is computed per tree version.
- **Where it is stored:** not keyed by green handle alone. Two textually identical rules at different places are the same green
  node, but they can resolve differently (one is a duplicate, the other is not), and resolution depends on the rest of the
  grammar. Results are keyed by something location-aware (position or declaration name) and tied to one tree version.
- **What can be reused across edits:** the pure part, such as a rule's own name or its referenced names. That part can be
  cached by green handle, and a reused declaration handle tells you it did not change.

## Choosing a layer

| Question                                                         | Layer                            |
| ---------------------------------------------------------------- | -------------------------------- |
| Is this subtree identical to before? Count, diff, hash?          | Green                            |
| What is this rule's name? What does the rule call?               | Red (read by slot name)          |
| Where is it? What encloses it? Which token is at the cursor?     | Red                              |
| What range do I report an error on, or edit?                     | Red                              |
| Which rule does this call resolve to? Is it unused or duplicate? | Semantic model (reading via red) |

Red nodes do not carry meaning or storage. Analysis that never has to point at source could mostly avoid them. Nearly every
real consumer eventually has to say "here", and that is the point where red comes in.
