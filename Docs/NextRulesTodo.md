# Next-rule catalog TODO

This is the resumable implementation checklist for
[NextRulesImplementationPlan.md](NextRulesImplementationPlan.md). It covers
the planned catalog after the completed v1 rules; do not mark an item complete
without the evidence stated in the plan.

## Current status

- Current phase: all next-rule catalog implementations enabled by default;
  regression hardening is complete.
- Last completed item: HAW005, HAW006, HAW007, HAW008, HAW010, HAW011,
  HAW013, HAW014, HAW015, HAW016, HAW017, HAW018, HAW020, HAW021, HAW023,
  HAW024, HAW025, HAW029, HAW030, and HAW100 implementation, focused tests,
  rule documentation, and integration wiring.
- Next action: perform the user-owned repository calibration and IDE consumer
  checks; the automated build/package gates are complete.
- The HAW017 LINQ test setup exposed and fixed an HAW104 error-symbol
  recursion in `CouplingInventory`.
- Blockers: none identified. Default-on policy is implemented; calibration is
  still required to decide whether any thresholds or exclusions need tuning.

## Foundation and regression protection

- [x] Add all twenty descriptors and prove `SupportedDiagnostics` contains the
  complete v1-plus-next catalog.
- [x] Refactor configuration to immutable typed per-rule options without
  changing any existing v1 configuration result or severity.
- [x] Reject unknown non-metadata option properties and invalid integer, ratio,
  decimal, string-list, and optional-threshold values with HAW900.
- [x] Extend diagnostic reporting for compilation-end locations, related
  locations, properties, and rule-provided default severity with configuration
  override.
- [x] Extract `ForwardingMethodClassifier` and prove HAW003's current positive
  and negative behavior is unchanged.
- [x] Add focused semantic classifiers for roles, service dependencies,
  enumerable, async, configuration, and logging symbols.
- [x] Split/add test support so each new rule has a focused test file and the
  existing bootstrap test remains below the maintainability limit.
- [x] Add concurrency/source-only regression tests for every new
  compilation-end index (including the six-index deterministic multi-file
  suite, Wave 3 duplicate/cancellation coverage, and HAW100 source/reference
  coverage).

## Wave 1 — default-on; calibration pending

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

## Wave 2 — default-on; calibration pending

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

## Wave 3 — architectural heuristics and health metric

- [x] HAW020: implement EF Core symbol-based repository pass-through ratio;
  test meaningful ORM behavior and explicit configuration overrides.
- [x] HAW023: implement private dead event/callback/hook evidence; test source
  subscriptions/derivations and externally accessible exclusions.
- [x] HAW025: implement typed configuration-flow chain indexing; test reads,
  transformations, chain-length boundary, and related locations.
- [x] HAW029: implement logger-symbol lifecycle-noise ratio; test error/audit
  exclusions, compile-time templates, and default-on configuration.
- [x] HAW030: implement deterministic operation-shape fingerprint clustering;
  test cluster/similarity/statement boundaries and stable related locations.
- [x] HAW100: implement abstraction/behavior inventory and informational
  diagnostic properties; test zero denominator, configured threshold, and
  culture-invariant density.
- [x] Decide default policy: all rules are enabled by default; repository
  calibration and false-positive review remain user-owned manual work.

## Per-rule completion protocol

- [ ] For every candidate ID in `Docs/next-rules.md`, add positive, negative,
  boundary, configuration, severity, suppression, attribute-target, malformed
  configuration, and generated-code tests.
- [ ] For every compilation-wide rule, add multi-file, source/reference,
  deterministic concurrent-run, cancellation, and no-duplicate tests.
- [x] Add one rule page for each candidate ID in `Docs/next-rules.md`, with the
  documented contract, exclusions, options, alternatives, and suppression.
- [x] Update README, `Docs/configuration.md`, sample `hawthorne.json`, package
  README content, and `AnalyzerReleases.Unshipped.md` with every new rule and
  default status.
- [x] Run and record focused tests per slice, full `dotnet test
  Hawthorne.sln`, `dotnet build Hawthorne.sln`, sample build, package-content
  validation, and `git diff --check` (206 tests passed; build and pack passed
  with zero warnings/errors; package contains analyzer DLL and README).
- [ ] Validate the packed analyzer in `dotnet build`, Visual Studio, and Visual
  Studio Code against a real consumer before release; Visual Studio and Visual
  Studio Code checks are user-owned manual work.
