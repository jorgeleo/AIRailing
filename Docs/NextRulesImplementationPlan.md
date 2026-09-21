# Hawthorne next-rule catalog implementation plan

## Status and scope

Status: implementation complete for the catalog and shared hardening; release
calibration and IDE consumption evidence remain intentionally manual.

This plan implements every candidate in [next-rules.md](next-rules.md):
HAW005–HAW008, HAW010–HAW011, HAW013–HAW018, HAW020–HAW021,
HAW023–HAW025, HAW029–HAW030, and HAW100. It extends the completed v1
catalog without changing its public package identity, `netstandard2.0` target,
or the `hawthorne.json` `AdditionalFiles` contract.

Some candidates specify only a title. The detection contracts below are
deliberately conservative initial definitions for those rules. They must be
made executable as tests before implementation; a broader interpretation is a
new design decision, not an implementation shortcut.

## Non-negotiable compatibility and engineering rules

- Keep one `[DiagnosticAnalyzer(LanguageNames.CSharp)]` bootstrap and register
  all normal rules from its compilation-start action. Configuration errors
  continue to report only compiler-error HAW900 and prevent all normal rule
  registration.
- Keep configuration immutable, loaded once per compilation, and supplied only
  by Roslyn `AdditionalFiles`. Do not add file-system access, environment
  variables, editorconfig settings, or JSON file-level exceptions.
- Continue to ignore generated code, use semantic symbols and `IOperation` for
  behavior decisions, enable concurrent analysis, and report source symbols
  only. Referenced assemblies are evidence only where a framework type must be
  recognized; they are never diagnostic targets.
- Retain the existing local, justified `SuppressMessageAttribute` policy and
  HAW901 enforcement. A new review rule must work with a justified source-level
  suppression unless it is explicitly tagged as non-suppressible; no rule may
  make `#pragma` a supported escape hatch.
- Keep analysis logic outside diagnostic registration classes. Each helper has
  one narrow responsibility; do not create a universal visitor, a global
  mutable rule state object, or a per-syntax-node compilation scan.
- Preserve the v1 configuration version (`1`) and existing defaults. New
  fields are optional. Invalid rule-specific values and unknown non-metadata
  properties must report HAW900 rather than being silently ignored.
- No code fixes are in scope. Diagnostics explain the detected evidence,
  configured threshold if any, and the preferred corrective action.

## Foundation work before the first new rule

### 1. Make the catalog and configuration extensible

1. Add descriptors for all twenty IDs to
   `HawthorneDiagnosticDescriptors`, with Architecture, Reliability, or Health
   categories as appropriate. Add them to `All`, so `SupportedDiagnostics`,
   HAW901's known-ID set, and default configuration stay synchronized.
2. Replace the growing sequence of rule-ID `if` statements in
   `HawthorneConfigurationLoader` with a small typed rule-option registry. A
   registry entry owns the default options, permitted JSON fields, strict
   validation, and the immutable option object for one rule. It does not own
   analyzer registration or detection logic.
3. Change `HawthorneConfiguration` to expose immutable typed options, for
   example `Get<HAW007Options>("HAW007")`, while retaining the common
   `enabled` and `severity` settings. Keep existing v1 options behavior
   unchanged.
4. Extend diagnostic reporting so compilation-end rules can report a stable
   source location, optional related locations, and diagnostic properties.
   Configuration severity remains the final override; a rule can otherwise
   choose a documented default severity for a higher-risk subcase.
5. Add shared, narrowly scoped semantic classifiers:

   - `ForwardingMethodClassifier` for a single dependency invocation with
     identity-preserved arguments and no other behavior.
   - `TypeRoleClassifier` for configured role suffixes and framework symbols.
   - `ServiceDependencyClassifier` for constructor dependencies, simple values,
     option wrappers, and configuration-shaped records.
   - `EnumerableClassifier`, `AsyncClassifier`, `ConfigurationValueClassifier`,
     and `LoggingClassifier`, each based on symbols/operations rather than
     spellings where a framework symbol is available.

6. Split the current all-purpose bootstrap integration tests before adding new
   cases. Create a reusable in-memory compilation/options host and one test
   file per new rule or closely related metric. Keep test files below the
   repository's maintainability limits.

### 2. Compilation-wide indexes

Only rules that need whole-compilation evidence get a compilation-end index.
Each index is created per compilation, receives thread-safe source events, and
is read only at compilation end:

| Index | Consumers | Recorded evidence |
| --- | --- | --- |
| `DerivedTypeIndex` | HAW006 | source abstract type to concrete derived types and observed closed generic constructions |
| `SourceCallIndex` | HAW010, HAW014, HAW024 | source invocations, caller count, argument nullability, and cancellation-token argument position |
| `ExtensionPointUsageIndex` | HAW023 | event subscriptions, delegate-property assignments, and in-compilation derivations |
| `ConfigurationFlowIndex` | HAW015, HAW025 | option/configuration declarations, meaningful reads, and unchanged forwarding edges |
| `DuplicateShapeIndex` | HAW030 | candidate method shape fingerprints and their source locations |
| `AbstractionInventory` | HAW100 | source type roles and behavioral-type evidence |

Indexes must first filter to plausible candidates and store symbols, compact
facts, or fingerprints—not syntax trees or semantic models. They must respect
the analyzer cancellation token and use `SymbolEqualityComparer.Default` for
symbol identity.

### 3. Default and calibration policy

The eight candidates specifically prioritized by `next-rules.md`—HAW005,
HAW006, HAW007, HAW010, HAW013, HAW016, HAW021, and HAW024—ship first as
warning-level review signals once their calibration target is met. HAW008 is
included in that first delivery because its direct-control-flow evidence is
also deterministic.

Rules whose proposed contract is necessarily heuristic (HAW015, HAW020,
HAW023, HAW025, HAW029, and HAW030) are implemented and documented in the
same release train. They are enabled by default by product decision, while
their configuration still supports `enabled: false` and a severity override.
HAW100 is informational by default because it reports a project health
measurement rather than an individual violation. Repository calibration
remains required before release.

This is an explicit extension of the v1 warning-default convention; the
README and sample configuration document the default-on policy and each rule's
disable option. Calibration may still adjust thresholds or exclusions based
on recorded false-positive evidence.

## Rule contracts and implementation details

### Wave 1 — high-confidence architecture and reliability rules

#### HAW005 — Needless Wrapper Explosion

Report a source class only when all of these hold: it has a configured wrapper
role suffix (`Wrapper`, `Adapter`, `Decorator`, `Facade`, or `Client` by
default); it has one dominant field/property/primary-constructor dependency;
at least three eligible public methods forward unchanged arguments to that
same dependency; and the forwarding ratio is at least 80 percent. Reuse the
HAW003 forwarding classifier, including its exclusions for overrides, interface
implementations, transformations, state changes, validation, logging,
selection, caching, and protocol conversion.

Add `minimumForwardingMethods`, `minimumForwardingRatio`,
`requireRoleSuffix`, and `roleSuffixes` options. When HAW005 is enabled and
claims a type, HAW003 suppresses its type-level duplicate; if HAW005 is
disabled, HAW003 retains its current behavior.

#### HAW006 — Premature Generalization

Report a source abstract class with exactly one source concrete derived type
only if its shared implementation is insignificant: no concrete ordinary
method with more than the configured statement limit and no meaningful shared
state/algorithm. For a source generic abstract type, additionally report when
all source-observed closed constructions have one distinct type-argument
combination. A derived implementation, a generic construction, and an
external type never count more than once.

Start with `maximumSharedExecutableStatements: 2`,
`minimumDerivedTypes: 1`, and `analyzeSingleClosedGenericUse: true`. Exclude
framework base types, types with no source location, source generators,
explicit public extension contracts selected by configuration, and bases that
have significant shared behavior. The diagnostic identifies whether the
single-use evidence came from derivation, generic construction, or both.

#### HAW007 — Constructor Dependency Explosion

Analyze each source constructor, including primary constructors. Count
service-like parameters, not all parameters: exclude primitives, enums,
`string`, well-known scalar structs, arrays/collections of values, and
`IOptions<T>`/`IOptionsSnapshot<T>`/`IOptionsMonitor<T>`. Treat an explicitly
configured value-type suffix (initially `Options`, `Settings`, and
`Configuration`) as a configuration value rather than a dependency. Report
the constructor when the count exceeds `maximumDependencies` (default 7), and
include the deterministic list of counted parameter names/types.

Do not infer a service just from a parameter name, flag record/request DTOs, or
combine multiple constructors into one count. Options are
`maximumDependencies`, `configurationTypeSuffixes`, and
`excludeOptionWrappers`.

#### HAW008 — Boolean Parameter Control Flow

Analyze ordinary source methods and count distinct `bool`/`bool?` parameters
that directly decide `if`, loop, conditional-expression, switch-guard, or
short-circuit branch behavior. The default warning threshold is two controlling
parameters and the default error threshold is three. An explicit configured
`severity` overrides the escalation; otherwise reporting uses the documented
threshold severity.

Exclude boolean values used only as data, a single flag, contract-required
overrides/interface implementations, and generated code. Options are
`warningParameterCount`, `errorParameterCount`, and
`requireDirectControlFlowUse`. The diagnostic reports both parameter names and
the number of independently controlled branches.

#### HAW010 — One-Method Service Classes

Report a source concrete class with exactly one eligible public ordinary method
when it has a configured service-style suffix, is below the physical-line and
cyclomatic-complexity limits, has at most one source caller of that method, and
does not implement an interface. Constructors, object members, explicit
framework/interface contracts, abstract types, and types with additional
public behavior are excluded. Caller count is source-only and must be stated
in the documentation; unknown external callers never create a claim of global
unusedness.

Defaults: `serviceSuffixes` of `Service`, `Handler`, `Processor`, and
`Validator`; `maximumPhysicalLines: 25`; `maximumCyclomaticComplexity: 2`; and
`maximumSourceCallers: 1`. The diagnostic recommends co-locating the operation
with its cohesive owner, not merging unrelated responsibilities.

#### HAW013 — Exception Laundering

Report a catch block only when it performs no handling other than throwing a
new generic exception which wraps the caught exception. The initial generic
set is `System.Exception` and `System.ApplicationException`; the block may
contain no mutation, recovery, translation to a specific exception type, or
structured contextual value. A new message literal alone is not meaningful
context. Re-throw (`throw;`), filters, specific domain exceptions, and catches
that recover or add structured data are excluded.

The optional `reportLogAndRethrow` detection is disabled by default because
global logging ownership is application-specific. Its tests and documentation
must keep it distinct from the generic-wrapper detection. Options are
`genericExceptionTypes` and `reportLogAndRethrow`.

#### HAW016 — Fake Async

Report a source `Task`, `Task<T>`, `ValueTask`, or `ValueTask<T>` method when
it has no asynchronous operation and only returns a completed/synchronously
computed value, including `Task.FromResult`, `Task.CompletedTask`, and an
equivalent `ValueTask` construction. Also report `async` methods without an
await after excluding contractual methods. Do not report overrides, interface
implementations, known framework callbacks, or a method that awaits/returns a
genuine asynchronous operation.

The `Task.Run` CPU-work heuristic is implemented behind the default-off
`reportTrivialTaskRun` option: enable it only for calibrated server projects.
The base rule has `ignoreContractMethods: true` and reports the specific fake
async construct found.

#### HAW021 — Excessive Try/Catch

Provide two non-duplicating signals. First, report a catch-all
`catch (Exception)` that returns `null`, `default`, or an equivalent
default-value expression without recovery. Second, report a source type when
its eligible methods meet both a `minimumMethodCount` and a
`maximumTryBlocksPerMethod` density threshold. The class-level diagnostic is
anchored to the type and never repeats HAW013's generic-wrapper message at the
same catch location.

Defaults: `minimumMethodCount: 5`, `maximumTryBlocksPerMethod: 0.75`, and
`reportCatchAllDefaultReturn: true`. Error logging/rethrow patterns remain
outside the default signal and can be configured only after host-level
calibration.

#### HAW024 — Unused CancellationToken Plumbing

Recognize the framework `System.Threading.CancellationToken` symbol. Report a
method parameter when no operation reads, checks, registers, or forwards that
token. Separately, report an invocation only when its caller has an available
token, the selected target method has a compatible token parameter, and the
call omits it or explicitly substitutes `CancellationToken.None`. Deduplicate
the two forms per method/call site and prefer the lost-forwarding location.

Exclude contract-required methods if configured, methods where the token is
used for `ThrowIfCancellationRequested` or registration, and calls to overloads
that do not accept a token. Options are `reportMissingForwarding`,
`treatNoneAsMissingForwarding`, and `ignoreContractMethods`.

### Wave 2 — local behavioral and collection rules

#### HAW011 — Excessive Micro-Methods

At the source type level, calculate:

`tiny private ordinary methods / private ordinary methods`.

A tiny method has at most two executable statements, including one-expression
bodies. Report only when the type has at least eight eligible private methods
and the ratio is greater than 0.60. Exclude property/event accessors,
constructors, overrides, generated members, and partial-type members outside
the source compilation. Options mirror the candidate proposal:
`maxTinyMethodRatio`, `tinyMethodStatementLimit`, and `minimumMethodCount`.

#### HAW014 — Defensive Null Checking Everywhere

The default implementation is deliberately narrower than the candidate:
report a private method's direct null guard for a non-nullable reference
parameter only when every source invocation supplies an expression with
`NullableFlowState.NotNull`. Recognize `x == null`, `x is null`,
`ReferenceEquals(x, null)`, and `ArgumentNullException.ThrowIfNull(x)` before
the parameter is otherwise used. Do not report public, protected, virtual,
interface, nullable, `[AllowNull]`, or unknown/delegate-invoked methods.

`includeInternalMethods` defaults to false because another compilation may call
an internal member. The document and diagnostic must say this is a redundancy
signal, not a claim that public boundary guards are always wrong.

#### HAW015 — Dead Configuration

Because the candidate supplies no detail, initial scope is configuration
properties, not arbitrary JSON keys. Report a source-private or
source-internal configuration/options property only when it is never read in
the compilation other than binding/initialization. A configuration-shaped type
is an `IOptions<T>` payload or a type matched by configured suffixes. Do not
report public configuration contracts, reflection/binder targets, or values
used by a branch, invocation, serialization boundary, or attribute.

Defaults: `includeInternalProperties: false` and `configurationTypeSuffixes`
of `Options`, `Settings`, and `Configuration`. The broader claim that a value
is "never meaningfully variable" is deferred until calibration supplies an
observable, source-only definition.

#### HAW017 — Unnecessary LINQ Materialization

Report an immediate `ToList()` or `ToArray()` whose result is immediately used
as the receiver of a known LINQ operation that accepts `IEnumerable<T>` and
does not require a list/array. Initial supported operations are filtering,
projection, ordering, partitioning, `Any`, `All`, `Count`, `First`, `Single`,
and a second materialization. Do not report a materialization returned/stored
as a collection, a mutation/indexing/List-only operation, a non-LINQ method,
or a semantic conversion that requires the concrete collection.

Use `IInvocationOperation` and recognized `System.Linq.Enumerable` symbols;
never decide from the method text alone. The initial options are
`analyzeToList` and `analyzeToArray`.

#### HAW018 — Repeated Enumeration

Within one source method, report a parameter or local with an enumerable,
non-collection type when it is consumed by two or more definite enumeration
operations without an intervening materialization or reassignment. Initial
consumers are `foreach`, LINQ terminal operations, `ToList`/`ToArray`, and an
explicit enumerator request. Arrays, strings, and types implementing known
collection/read-only collection interfaces are excluded.

Start with same-block, source-order evidence rather than a speculative
whole-method control-flow proof. Add CFG handling only after tests demonstrate
that it improves correct path sensitivity. The option is
`minimumEnumerations` (default 2).

### Wave 3 — default-on architectural heuristics

#### HAW020 — Boilerplate Repository Layer

Report a configured repository-role class only when a single recognized ORM
dependency dominates at least three eligible public methods and at least 80
percent of those methods are direct `DbSet`/`DbContext` pass-throughs such as
`Add`, `Remove`, `Find`, `Where`, or `SaveChanges`. Recognition is by EF Core
symbols, not `DbSet` text. Selection, transaction orchestration, query policy,
mapping, authorization, caching, and domain behavior make a method
non-boilerplate.

The rule is enabled by default because the product policy is all catalog rules
on; projects may disable it when a repository boundary is intentional. Options:
`minimumForwardingMethods`,
`minimumForwardingRatio`, `repositorySuffixes`, and `requireRepositorySuffix`.

#### HAW023 — Dead Extension Points

Report only source-private extension points with complete source evidence:
an event with no subscription, a delegate callback property with no assignment
other than initialization, or a virtual hook in a type with no in-compilation
derivation and no externally visible extension surface. Do not infer that a
public/protected event or virtual member is dead; external consumers cannot be
seen from a Roslyn compilation.

Defaults are `includePrivateMembers: true` and
`includeExternallyAccessibleMembers: false`. Event, callback, and hook
sub-detections have individual options so calibration can disable a weak form.

#### HAW025 — Configuration Pass-Through

Report an unchanged configuration flow only when two or more source types form
a verified chain: a configuration/options value is accepted and stored by one
type, passed unchanged to a dependency, then passed unchanged again without a
property read, validation, selection, or transformation. The compilation-end
flow index records the typed edges and reports the first needless forwarding
boundary with related locations for the whole chain.

Recognize option wrappers and configured type suffixes. Do not report a type
that reads a value, derives a setting, validates options, converts a value, or
crosses a documented external contract. Options are `minimumForwardingHops`
(default 2) and `configurationTypeSuffixes`.

#### HAW029 — Excessive Logging Noise

Report a source type only when at least five eligible methods have a lifecycle
log ratio of at least 80 percent. A lifecycle log is a recognized logger call
with a compile-time template matching configured entry/exit/success terms. Do
not count warning/error/critical logs, logs with exception data, audit/security
events, metrics, or business-state logs. The type-level report lists the ratio
and representative method names.

The rule is enabled by default. Options are `minimumMethodCount`,
`maximumLifecycleLogRatio`, `lifecycleTerms`, and `loggerTypeNames`.

#### HAW030 — Copy-Paste Near Duplication

At compilation end, compare only candidate source methods with at least three
statements. Build a deterministic structural fingerprint from `IOperation`
kinds, control-flow shape, operator kinds, and parameter/local roles while
normalizing identifiers, literals, and type-specific member names. Report only
clusters of three or more methods whose similarity meets the configured 0.90
threshold. One diagnostic represents a cluster and carries the other members
as related locations.

Exclude generated code, overrides/interface implementations, trivial accessors,
and clusters below the statement/size threshold. Mapping layers may be
legitimate; documentation must emphasize reviewing a cluster before
abstracting it. Options are `minimumMethods`,
`minimumStatements`, and `minimumSimilarity`.

#### HAW100 — Abstraction Density

At compilation end, inventory source-defined abstraction-role types (interfaces,
abstract classes, and classes matched by `Factory`, `Adapter`, `Facade`,
`Provider`, `Manager`, `Service`, or configured suffixes) and source-defined
behavioral types (concrete classes/records with at least one meaningful
ordinary method). Count each type once even if it has multiple abstraction
roles. Calculate:

`abstraction types / behavioral types`.

When enabled, emit one informational compilation-end diagnostic with
`abstractionCount`, `behavioralCount`, and a culture-invariant density value as
diagnostic properties. Report a threshold violation only when optional
`maximumDensity` is configured; otherwise it is a measurement. The analyzer
does not invent repository-history trends—CI or a future CLI may persist these
values. Options are `maximumDensity`, `minimumBehavioralTypes`, and
`abstractionRoleSuffixes`.

## TDD, documentation, and calibration protocol

For each rule, write the exact positive, negative, boundary, configuration,
severity, justified-suppression, attribute-target, generated-code, and malformed
configuration tests before the analyzer. Compilation-wide rules additionally
need multi-file/multi-type tests, repeated concurrent runs with identical
diagnostics, and source-versus-referenced-assembly tests. Metric/fingerprint
helpers get exact-value unit tests separate from threshold/reporting tests.

Each rule page at `Docs/rules/HAWxxx.md` must contain its purpose, exact
evidence, non-goals/exclusions, violation and preferred alternative examples,
all option keys/defaults, a justified suppression example, and performance
limits. Update the README catalog, configuration example, sample consumer
configuration, package README, and `AnalyzerReleases.Unshipped.md` together.

Calibrate each rule against at least an ASP.NET Core application, class
library, CLI, layered enterprise application, AI-generated sample, and mature
open-source project. Record rule ID, location, true/false-positive decision,
reason, time/diagnostic count, and resulting heuristic/configuration change.
No heuristic threshold or exclusion is changed without this record.

## Delivery order and gates

1. Complete the foundation refactor and its regression tests without changing
   existing v1 diagnostic behavior.
2. Implement Wave 1 in the order HAW005, HAW007, HAW008, HAW016, HAW024,
   HAW010, HAW013, HAW006, HAW021. Calibrate after each small slice.
3. Implement Wave 2 in the order HAW011, HAW017, HAW018, HAW014, HAW015.
4. Implement Wave 3 in the order HAW020, HAW023, HAW025, HAW029, HAW030,
   HAW100. Keep the documented default-on policy while calibration records any
   threshold or suppression adjustments.
5. For every slice, run its focused tests first, then `dotnet test
   Hawthorne.sln`, `dotnet build Hawthorne.sln`, the sample consumer build,
   `dotnet pack`/package-content validation, and `git diff --check`.
6. Before publishing, run the analyzer in `dotnet build`, Visual Studio, and
   Visual Studio Code against a real consumer and ensure packaged analyzer
   diagnostics/configuration match the project-reference behavior.

## Completion criteria

The implementation is complete when all twenty IDs are registered, have strict
configuration support, detection logic, isolated and integrated TDD coverage,
HAW901-compatible suppression behavior, rule documentation, sample
configuration entries, and release notes. Release readiness additionally
requires recorded calibration and real-consumer IDE evidence. Existing
HAW001–HAW106, HAW900, and HAW901 behavior remains unchanged unless a
separately documented regression test intentionally changes it.
