# Hawthorne configuration

Add `hawthorne.json` beside the consuming project file and include it as an
`AdditionalFiles` item. Rules default to warnings. Each rule accepts `enabled`
and `severity` (`error`, `warning`, `info`, or `hidden`); metric rules also
accept their documented `maximum` values. Intentional suppressions belong on
the affected source symbol with `SuppressMessageAttribute` and a non-blank
`Justification`. Optional `_comment` properties may be added to the root,
`rules` object, and individual rule objects; the analyzer ignores them.
