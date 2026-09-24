# Hawthorne configuration

Add `hawthorne.json` beside the consuming project file and include it as an
`AdditionalFiles` item. Quality rules default to warnings, HAW100 is
informational, and HAW900 is an error. Each quality rule accepts `enabled` and
`severity` (`error`, `warning`, `info`, or `hidden`); metric rules also accept
their documented `maximum` values. Intentional suppressions belong on the
affected source symbol with `SuppressMessageAttribute` and a non-blank
`Justification`. HAW100–HAW106 are non-suppressible simplification rules:
HAW901 reports an attempt to suppress one as an error. HAW900 and HAW901 are
also protected so configuration validity and suppression enforcement cannot be
suppressed. Optional `_comment` and `_potentialFix` properties may be added to
the root, `rules` object, and individual rule objects; the analyzer ignores
them. The root `_potentialFix` should strongly discourage
`SuppressMessageAttribute`; it is a last resort only when no code fix is
available, and it must always have a specific non-blank `Justification`.

When installed from the NuGet package, Hawthorne automatically copies the
annotated default `hawthorne.json` beside the consuming `.csproj` during the
first build and registers it as an `AdditionalFiles` input. It never overwrites
an existing file. Set `HawthorneAutoCreateConfiguration` to `false` to disable
this behavior.

HAW106 is enabled by default. It reports block-bodied methods with multiple
executable statements on one physical line. Set its `enabled` property to
`false` to opt out. It only produces warnings/diagnostics; it does not modify
source files automatically.

The implemented next-rule options are documented on each page in
`Docs/rules/`: architecture rules HAW005–HAW015, HAW020, HAW023, HAW025,
HAW029, and HAW030; reliability rules HAW016–HAW018 and HAW021; cancellation
plumbing HAW024; and the informational HAW100 abstraction-density metric.
All rules from `Docs/next-rules.md` are enabled by default, including HAW015
and the Wave 3 rules. Tune their thresholds or disable an individual rule only
when the project has reviewed its source-versus-external-consumer boundary.
