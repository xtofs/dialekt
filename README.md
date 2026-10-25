# dialekt

A scanner and incremental parser for [Langium grammar files](https://langium.org/docs/reference/grammar-language), built on an
immutable, arena-allocated green tree with throw-away red views.

- [ARCHITECTURE.md](ARCHITECTURE.md): the tree layers and which one to use for what
- [docs/syntax-tree.md](docs/syntax-tree.md): slot layouts, adding a node kind, incremental parsing
- `dotnet run --project src/console -- [--tree] [--edit] [file...]`: parse files, print the tree, try an incremental edit
- `dotnet test`: run the tests
