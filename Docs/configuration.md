# Hawthorne configuration

Add `hawthorne.json` beside the consuming project file and include it as an
`AdditionalFiles` item. Rules default to warnings. Each rule accepts `enabled`
and `severity` (`error`, `warning`, `info`, or `hidden`); metric rules also
accept their documented `maximum` values. Intentional suppressions belong on
the affected source symbol with `SuppressMessageAttribute` and a non-blank
`Justification`. Optional `_comment` and `_potentialFix` properties may be
added to the root, `rules` object, and individual rule objects; the analyzer
ignores them. The root `_potentialFix` should strongly discourage
`SuppressMessageAttribute`; it is a last resort only when no code fix is
available, and it must always have a specific non-blank `Justification`.

HAW106 is opt-in. Set its `enabled` property to `true` to report missing CRLF
after opening braces and semicolons. It only produces warnings/diagnostics; it
does not modify source files automatically.
