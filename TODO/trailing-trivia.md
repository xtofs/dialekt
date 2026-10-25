# Trailing trivia (comment ownership)

> **Status: feature for a requirement that does not exist yet.** Nothing in the project needs this today. Do not implement it until a feature such as formatting, refactoring/"extract node" tooling, or "this comment belongs to this rule" is actually required.

Currently all trivia (whitespace and comments) is **leading** trivia of the following token; the end-of-file token carries the final trivia. This is lossless and sufficient for parsing.

## What it would add

Roslyn-style ownership: a token gets as **trailing** trivia the rest of its line, and the next token gets everything after that as leading trivia. A comment on the same line as a token then stays with it:

```
x = 1;   // about x      <- trailing trivia of `;`
/** about y */           <- leading trivia of `y`
y = 2;
```

With leading-only, both comments belong to `y`, so moving, copying or deleting the `x` statement leaves its comment behind.

## Sketch

Do it in the token-building loop in the `Parser` constructor, where trivia is attached to tokens. The header, arena and cache need no change: trailing trivia would be extra children after the token text, and `GreenNodeReader.TextWidth` would have to subtract both leading and trailing trivia. `TreeFormatter` would also need to know which trivia children are leading and which are trailing.

Costs: attaching trailing trivia means scanning to the end of the line, and a comment between two tokens on the same line goes with the left one.
