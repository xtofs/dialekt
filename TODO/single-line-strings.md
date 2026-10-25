# Stop unterminated strings at end of line

`StringLiteral` in `TokenPatterns.cs` allows newlines, as Langium's own `STRING` terminal does. A stray quote therefore pairs with the next quote further down the file and can hide an error until later.

Change the pattern so strings cannot span lines, so the scanner reports "Unterminated string literal" at the right place.

Trade-off: this differs slightly from Langium's own `STRING` terminal.
