# Next-rule catalog TODO

This is the resumable implementation checklist for
[NextRulesImplementationPlan.md](NextRulesImplementationPlan.md). It covers
the planned catalog after the completed v1 rules; do not mark an item complete
without the evidence stated in the plan.

## Current status

- Current phase: Wave 1 implementation in progress.
- Last completed item: HAW005, HAW006, HAW007, HAW008, HAW010, HAW011,
  HAW013, HAW014, HAW015, HAW016, HAW017, HAW018, HAW021, and HAW024
  implementation, focused tests, rule documentation, full test suite, and
  solution build.
- Next action: calibrate the completed Wave 2 rules against the repository set,
  then begin Wave 3 HAW020.
- The HAW017 LINQ test setup exposed and fixed an HAW104 error-symbol
  recursion in `CouplingInventory`.
- Blockers: none identified. The five title-only candidates have conservative
  contracts in the plan and require calibration before default enablement.

## Foundation and regression protection

- [x] Add all twenty descriptors and prove `SupportedDiagnostics` contains the
  complete v1-plus-next catalog.
- [ ] Refactor configuration to immutable typed per-rule options without
  changing any existing v1 configuration result or severity.
- [ ] Reject unknown non-metadata option properties and invalid integer, ratio,
  decimal, string-list, and optional-threshold values with HAW900.
- [ ] Extend diagnostic reporting for compilation-end locations, related
  locations, properties, and rule-provided default severity with configuration
  override.
- [ ] Extract `ForwardingMethodClassifier` and prove HAW003's current positive
  and negative behavior is unchanged.
- [ ] Add focused semantic classifiers for roles, service dependencies,
  enumerable, async, configuration, and logging symbols.
- [ ] Split/add test support so each new rule has a focused test file and the
  existing bootstrap test remains below the maintainability limit.
- [ ] Add concurrency/source-only regression tests for every new
  compilation-end index.

## Wave 1 — default-on after calibration

- [x] HAW005: implement wrapper type classification and shared forwarding
  ratio; test duplicate prevention with HAW003, contracts, transformations,
  and all thresholds.
- [x] HAW007: implement service-like constructor dependency counting; test
  scalar/options/config exclusions and the 7/8 boundary.
- [x] HAW008: implement direct boolean-control-flow counting and warning/error
  escalation; test data-only flags, contracts, and both boundaries.
- [x] HAW016: implement sync-completed task and async-without-await detection;
  test real async, contracts, and default-off `Task.Run` heuristic.
- [x] HAW024: implement unused-token and lost-forwarding detection; test token
  reads, token-aware overloads, `CancellationToken.None`, and deduplication.
- [x] HAW010: implement one-method service evidence plus source caller index;
  test line/complexity/caller boundaries and interface exclusions.
- [x] HAW013: implement generic exception wrapping detection; test meaningful
  context, specific translation, rethrow, and default-off log/rethrow behavior.
- [x] HAW006: implement derived-type and closed-generic-use indexing; test
  shared behavior, generic-use, configuration, and suppression boundaries.
- [x] HAW021: implement catch-all default-return and try-density signals; test
  separation from HAW013 and all density boundaries.
- [ ] Calibrate HAW005, HAW006, HAW007, HAW008, HAW010, HAW013, HAW016,
  HAW021, and HAW024; record decisions before enabling their warning defaults.

## Wave 2 — local behavioral and collection rules

- [x] HAW011: implement tiny-private-method metric and exact ratio/boundary
  tests.
- [x] HAW014: implement private non-nullable redundant-guard analysis with
  source call nullability evidence; test nullable/public/unknown-call paths.
- [x] HAW015: implement the conservative unused private configuration-property
  contract; test binding, meaningful reads, and external-contract exclusions.
- [x] HAW017: implement immediate `ToList`/`ToArray` chain detection; test
  every supported LINQ receiver plus mutations and returned collections.
- [x] HAW018: implement same-block repeated enumeration evidence; test known
  collection exclusions, materialization/reassignment, and boundary count.
- [ ] Calibrate Wave 2 against the required repository set and adjust only the
  documented contracts/options.

## Wave 3 — opt-in architectural heuristics and health metric

- [ ] HAW020: implement EF Core symbol-based repository pass-through ratio;
  test meaningful ORM behavior and intentional opt-in configuration.
- [ ] HAW023: implement private dead event/callback/hook evidence; test source
  subscriptions/derivations and externally accessible exclusions.
- [ ] HAW025: implement typed configuration-flow chain indexing; test reads,
  transformations, chain-length boundary, and related locations.
- [ ] HAW029: implement logger-symbol lifecycle-noise ratio; test error/audit
  exclusions, compile-time templates, and default-disabled configuration.
- [ ] HAW030: implement deterministic operation-shape fingerprint clustering;
  test cluster/similarity/statement boundaries and stable related locations.
- [ ] HAW100: implement abstraction/behavior inventory and informational
  diagnostic properties; test zero denominator, configured threshold, and
  culture-invariant density.
- [ ] Calibrate each opt-in rule and decide, with recorded evidence, whether it
  remains opt-in or can become default-on.

## Per-rule completion protocol

- [ ] For every candidate ID in `Docs/next-rules.md`, add positive, negative,
  boundary, configuration, severity, suppression, attribute-target, malformed
  configuration, and generated-code tests.
- [ ] For every compilation-wide rule, add multi-file, source/reference,
  deterministic concurrent-run, cancellation, and no-duplicate tests.
- [ ] Add one rule page for each candidate ID in `Docs/next-rules.md`, with the
  documented contract, exclusions, options, alternatives, and suppression.
- [ ] Update README, `Docs/configuration.md`, sample `hawthorne.json`, package
  README content, and `AnalyzerReleases.Unshipped.md` with every new rule and
  opt-in/default status.
- [ ] Run and record focused tests per slice, full `dotnet test
  Hawthorne.sln`, `dotnet build Hawthorne.sln`, sample build, package-content
  validation, and `git diff --check`.
- [ ] Validate the packed analyzer in `dotnet build`, Visual Studio, and Visual
  Studio Code against a real consumer before release.
