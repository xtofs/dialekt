# Syntax trees in dialekt

Two layers, plus a schema that describes both.

## Green tree (`src/dialekt/Green/`)
Untyped, immutable storage in a `GreenArena`: a node is a `SyntaxKind`, a width and a fixed list of child handles. It knows
nothing about absolute positions or parents, and equal subtrees are one node (`GreenNodeCache`). Whitespace and comments are
leading trivia children of the following token, so the tree is lossless: the widths of the root's tokens add up to the source.

Every node kind has a **fixed slot layout** (`SyntaxSchema`, a static table in code):

| Slot shape | Child in the slot |
|---|---|
| Required | the part; a `Missing` placeholder if the parser did not find it (flag `IsMissing`, zero width) |
| Optional | the part, or the shared zero-width `Absent` node |
| List / SeparatedList | a `List` / `SeparatedList` node holding the items (and separator tokens), possibly empty |

`Parser.Close` fits the children it collected to the slots of the kind, so `ParserRule` slot 1 is always its name,
whether or not it has a modifier. `Error` nodes (a declaration that could not be parsed) are free-form.

## Red layer: struct views (`src/dialekt/Syntax/`)
`SyntaxNode` is a 16-byte `readonly struct` (tree, green handle, absolute position): no allocation, nothing cached, no
parent pointer. Navigating just creates new views. `SyntaxTree.GetParent`/`Ancestors` re-descend from the root when a
parent is really needed. A zero-width node (absent part, empty list) cannot be told from an equal sibling at the same
position; for those the outermost match is returned.

Slots are read by name, checked at run time against the schema:

```csharp
var rule = member;                                  // a ParserRule node
var name = rule.GetSlot("Name").Text;
if (!rule.GetSlot("Modifier").IsAbsent) { /* entry or fragment */ }
foreach (var element in rule.GetSlot("Definition").GetSlot("Elements").Children) { ... }
```

`GetSlot` throws with the valid slot names when the kind has no such slot; `TryGetSlot` probes. An optional part that is not
there is an `Absent` node (`IsAbsent`), a required part the parser did not find is `IsMissing`, and a list slot is a list
node whose `Children` are the items (separated lists alternate items and separator tokens).

## Which layer does a traversal use?
- **Green** (handles and `GreenNodeReader`, no positions): anything that is a pure function of structure: counting,
  compaction, structural comparison, the incremental reuse analysis, folds memoized per handle. `GreenNodeReader` is a
  `ref struct`, so green recursion is plain methods, not lambdas or iterators.
- **Red views**: anything that needs a position, text, line/column, diagnostics, or reads the tree by slot name. A result
  that must survive an edit is keyed by green handle or by name, never kept as a view.

## Adding or changing a node kind
1. Add the kind to `SyntaxKind`.
2. Describe its slots in `SyntaxSchema.Build` (and `IsInCategory` if it joins a category).
3. Make the parser call `Close(mark, kind)` with the children in slot order.

The schema conformance tests tell you if the parser and the schema disagree.

## Incremental parsing
`Parser.ReParse(newText, previousTree, textChange)` rescans the text but takes over the top-level declarations of the
previous tree that the edit did not touch (see `Parser.TryReuse` for the conditions). `ParseResult.ReusedDeclarations` and
`ReparsedDeclarations` report what happened. Each re-parse forks the arena; `SyntaxTree.Compact()` drops old versions.
