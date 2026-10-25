# Add parser tests

Turn the invalid-input cases checked by hand into a small test project, so recovery behaviour doesn't change unnoticed.

Cases to cover (each should report line/column and keep parsing later declarations):

- unclosed `[` in a cross reference
- stray `@` where `;` is expected
- missing operand (`e=;`, `type T = A | ;`, `b: ;` in an interface)
- unterminated regular expression literal
- unterminated comment and unexpected character (`#`)

Also add positive tests that parse `langium-grammar.langium` and `langium-types.langium` with 0 diagnostics.

## Symbol table consistency

`SyntaxFacts.MaxSymbolLength` (a constant) must equal the length of the longest symbol in the `SyntaxFacts.TryGetSymbolKind` switch. The switch cannot be enumerated, so nothing checks this at runtime: a longer symbol added to the switch without updating the constant would silently never be matched by the scanner.

Add a test that holds the list of all symbol texts (the 28 symbols the grammar uses) and asserts:

- every listed symbol is recognized by `TryGetSymbolKind`, and maps to a distinct `SyntaxKind`
- `MaxSymbolLength` equals the longest listed symbol
- no string one character longer than `MaxSymbolLength` built from those symbols is recognized

The test lives in the test project (internal access via `InternalsVisibleTo` for the scanner parts), not in a Debug-only static constructor.
