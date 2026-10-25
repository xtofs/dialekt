# Add parser tests

Turn the invalid-input cases checked by hand into a small test project, so recovery behaviour doesn't change unnoticed.

Cases to cover (each should report line/column and keep parsing later declarations):

- unclosed `[` in a cross reference
- stray `@` where `;` is expected
- missing operand (`e=;`, `type T = A | ;`, `b: ;` in an interface)
- unterminated regular expression literal
- unterminated comment and unexpected character (`#`)

Also add positive tests that parse `langium-grammar.langium` and `langium-types.langium` with 0 diagnostics.
