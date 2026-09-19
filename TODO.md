# Hawthorne implementation checklist

This file is the resumable source of truth for implementation progress. Mark an
item complete only after its listed tests pass. Record any pause or blocker in
the status section before stopping work.

## Current status

- Current phase: Phase 2 — JSON configuration
- Last completed item: warning-default configuration model and loader
- Next item: connect malformed-configuration reporting to HAW900
- Blockers: none

## Foundation

- [x] Create the solution and .NET 10 build settings.
- [x] Create the `Hawthorne.Analyzers` `netstandard2.0` analyzer package project.
- [x] Create the `Hawthorne.Analyzers.Tests` .NET 10 test project.
- [x] Add central descriptor catalog and analyzer bootstrap.
- [x] Prove analyzer-test execution with a focused test.
- [ ] Add a sample consumer and package-consumption build test.

## Shared configuration and governance

- [x] Define immutable configuration models and warning defaults.
- [x] Load exactly one `hawthorne.json` from `AdditionalFiles`.
- [ ] Validate configuration and report compiler-error `HAW900` without normal analysis.
- [ ] Implement the project-relative file-exception contract.
- [ ] Implement dynamic effective diagnostic severity.
- [ ] Implement `HAW901` pragma-suppression warning.

## Rules, in implementation order

- [ ] HAW105 — method length, including exact executable-statement and line tests.
- [ ] HAW103 — maximum semantic nesting depth with exact metric tests.
- [ ] HAW101 — cyclomatic complexity with all defined branch constructs counted.
- [ ] HAW102 — cognitive complexity with exact metric tests.
- [ ] HAW004 — hidden singleton state.
- [ ] HAW002 — speculative factory.
- [ ] HAW003 — pass-through indirection and class-level ratio.
- [ ] HAW104 — class coupling.
- [ ] HAW001 — every in-compilation single-implementation interface.

## Release readiness

- [ ] Add rule documentation and configuration documentation.
- [ ] Calibrate architecture diagnostics against real repositories.
- [ ] Add NuGet packaging metadata and package validation.
- [ ] Add CI build/test gate.
- [ ] Verify Visual Studio, Visual Studio Code, and `dotnet build` consumption.

## Pause log

- None.
