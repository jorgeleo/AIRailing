# Hawthorne configuration

Add `hawthorne.json` beside the consuming project file and include it as an
`AdditionalFiles` item. Rules default to warnings. Each rule accepts `enabled`
and `severity` (`error`, `warning`, `info`, or `hidden`); metric rules also
accept their documented `maximum` values. Exceptions require an exact
project-relative `/` path, rule IDs, and a non-empty reason.
