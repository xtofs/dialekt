# dialekt

A scanner and incremental parser for [Langium grammar files](https://langium.org/docs/reference/grammar-language), built on an
immutable, arena-allocated green tree with throw-away red views.

- [ARCHITECTURE.md](ARCHITECTURE.md): the tree layers and which one to use for what
- [docs/syntax-tree.md](docs/syntax-tree.md): slot layouts, adding a node kind, incremental parsing
- Projects: `src/dialekt.runtime` (language-independent trees and parser machinery), `src/dialekt` (the Langium language), `src/dialekt.grammar` (the semantic model of a grammar), `src/console`, `tests/dialekt.tests`
- `samples/`: example grammars (Hello-World, arithmetics, domain model, state machine, and Langium's own grammar)
- `dotnet run --project src/console -- [--tree] [--model] [--edit] [file...]`: parse files, print the tree, print the grammar model, try an incremental edit
- `dotnet test`: run the tests
