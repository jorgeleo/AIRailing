# Hawthorne implementation checklist

This file is the resumable source of truth for implementation progress. Mark an
item complete only after its listed tests pass. Record any pause or blocker in
the status section before stopping work.

## Current status

- Current phase: Implementation complete; awaiting user-owned release readiness
- Last completed item: HAW901, sample consumer, and rule/configuration documentation
- Next item: user-owned release readiness items below
- Blockers: none

## Foundation

- [x] Create the solution and .NET 10 build settings.
- [x] Create the `Hawthorne.Analyzers` `netstandard2.0` analyzer package project.
- [x] Create the `Hawthorne.Analyzers.Tests` .NET 10 test project.
- [x] Add central descriptor catalog and analyzer bootstrap.
- [x] Prove analyzer-test execution with a focused test.

## Shared configuration and governance

- [x] Define immutable configuration models and warning defaults.
- [x] Load exactly one `hawthorne.json` from `AdditionalFiles`.
- [x] Validate configuration and report compiler-error `HAW900` without normal analysis.
- [x] Implement the project-relative file-exception contract.
- [x] Implement dynamic effective diagnostic severity.

## Rules, in implementation order

- [x] HAW105 — method length, including exact executable-statement and line tests.
- [x] HAW103 — maximum semantic nesting depth with exact metric tests.
- [x] HAW101 — cyclomatic complexity with all defined branch constructs counted.
- [x] HAW102 — cognitive complexity with exact metric tests.
- [x] HAW004 — hidden singleton state.
- [x] HAW002 — speculative factory.
- [x] HAW003 — pass-through indirection and class-level ratio.
- [x] HAW104 — class coupling.
- [x] HAW001 — every in-compilation single-implementation interface.

## Remaining implementation work

- [x] Add a sample consumer and package-consumption build test.
- [x] Implement `HAW901` pragma-suppression warning.
- [x] Add rule documentation and configuration documentation.

## User-owned completion items

- [x] Choose the release version and license; package ID and repository metadata are configured.
- [x] Commit and manage the Git repository history.
- [x] Choose and configure the CI provider, including any package-feed credentials.
- [x] Provide or approve the real repositories used for architecture-rule calibration, then review the recorded false-positive results.
- [x] Perform final Visual Studio and Visual Studio Code consumption checks on the intended development environments.

## Release readiness

- [x] Calibrate architecture diagnostics against real repositories.
- [x] Add NuGet packaging metadata and package validation.
- [x] Add CI build/test gate.
- [x] Verify Visual Studio, Visual Studio Code, and `dotnet build` consumption.

## Pause log

- None.
