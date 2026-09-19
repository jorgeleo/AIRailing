# Hawthorne Analyzer — Detailed Implementation Plan

## 1. Product Definition

### 1.1 Purpose

Hawthorne Analyzer is a cross-project Roslyn analyzer package designed to detect unnecessary abstraction, excessive indirection, hidden global state, and code complexity patterns that are especially common in AI-generated C# code.

Hawthorne is not tied to the Fluid product family. It is intended to be reusable across independent C#/.NET projects as a general code-quality enforcement layer.

The analyzer is intentionally opinionated. Its primary goal is not formatting or conventional style enforcement; its goal is to detect architecture and implementation choices that add complexity without proportional value.

The analyzer must be suitable for use in:

- Human-written C# projects.
- AI-assisted development.
- Autonomous or semi-autonomous coding harnesses.
- CI/CD quality gates.
- Local builds and IDE feedback.

The analyzer should report configured violations as Roslyn diagnostics and allow projects to promote those diagnostics to build errors.

---

## 2. Core Design Principles

### 2.1 Cross-project branding

All analyzer rule IDs, namespaces, package names, diagnostic categories, configuration names, and documentation should use the `Hawthorne` brand.

Recommended names:

```text
Hawthorne.Analyzers
Hawthorne.Analyzers.Tests
Hawthorne.Analysis
Hawthorne.Configuration
```

Rule IDs should use the `HAW` prefix.

Example:

```text
HAW001
HAW002
HAW003
```

### 2.2 Avoid simplistic pattern matching

Rules should minimize false positives by using the semantic model, symbols, `IOperation`, and compilation-wide analysis where appropriate.

The analyzer should not flag a construct merely because it superficially resembles an anti-pattern. It should look for evidence that the abstraction or construct is not adding meaningful behavior.

### 2.3 Configuration lives in JSON

Hawthorne must use a dedicated JSON configuration file rather than `.editorconfig` for rule configuration.

Recommended default file name:

```text
hawthorne.json
```

The analyzer should search for the configuration through Roslyn `AdditionalFiles` / `AdditionalTexts` supplied by the consuming project.

The consuming project should include the configuration file as an `AdditionalFiles` item.

Example project configuration:

```xml
<ItemGroup>
  <AdditionalFiles Include="hawthorne.json" />
</ItemGroup>
```

### 2.4 Suppression is controlled centrally

Source-level suppression using pragmas must not be considered a valid Hawthorne exception mechanism.

Examples such as:

```csharp
#pragma warning disable HAW003
```

must not be part of the supported Hawthorne workflow.

Hawthorne exceptions must instead be explicitly documented in `hawthorne.json`.

### 2.5 Exceptions are file-scoped

Every exception must identify a specific source file.

There must be no configuration mechanism that disables a rule globally through the exceptions system.

Valid exception concept:

```json
{
  "file": "Infrastructure/LegacyBridge.cs",
  "rules": ["HAW003"],
  "reason": "Compatibility adapter required by legacy integration."
}
```

Invalid exception concepts:

```json
{
  "rules": ["HAW003"]
}
```

```json
{
  "file": "*",
  "rules": ["HAW003"]
}
```

Exceptions should always contain a justification.

---

# 3. Diagnostic Rules

## 3.1 Rule catalog

| Rule | Name | Default Threshold / Condition |
|---|---|---|
| HAW001 | Redundant Single-Implementation Abstraction | Semantic heuristic |
| HAW002 | Speculative Factory | Factory constructs one fixed type without meaningful behavior |
| HAW003 | Pass-Through Indirection | 80% forwarding ratio |
| HAW004 | Hidden Singleton State | Public/static self instance with mutable state |
| HAW101 | Cyclomatic Complexity | 10 |
| HAW102 | Cognitive Complexity | 15 |
| HAW103 | Maximum Nesting Depth | 4 |
| HAW104 | Class Coupling | 12 |
| HAW105 | Method Length | 30 executable statements / optional physical LOC limit |

Rules `HAW001` through `HAW004` target architectural overengineering.

Rules `HAW101` through `HAW105` target structural complexity.

---

# 4. Configuration Design

## 4.1 Proposed `hawthorne.json`

```json
{
  "version": 1,
  "rules": {
    "HAW001": {
      "enabled": true,
      "severity": "error",
      "minimumConfidence": 7
    },
    "HAW002": {
      "enabled": true,
      "severity": "error"
    },
    "HAW003": {
      "enabled": true,
      "severity": "error",
      "forwardingRatio": 0.80,
      "minimumForwardingMethods": 3
    },
    "HAW004": {
      "enabled": true,
      "severity": "error",
      "requireMutableState": true
    },
    "HAW101": {
      "enabled": true,
      "severity": "error",
      "maximum": 10
    },
    "HAW102": {
      "enabled": true,
      "severity": "error",
      "maximum": 15
    },
    "HAW103": {
      "enabled": true,
      "severity": "error",
      "maximum": 4
    },
    "HAW104": {
      "enabled": true,
      "severity": "error",
      "maximum": 12
    },
    "HAW105": {
      "enabled": true,
      "severity": "error",
      "maximumExecutableStatements": 30,
      "maximumPhysicalLines": 50
    }
  },
  "exceptions": [
    {
      "file": "Infrastructure/LegacyBridge.cs",
      "rules": ["HAW003", "HAW104"],
      "reason": "Legacy compatibility boundary required by external system."
    },
    {
      "file": "Generated/ProtocolAdapter.cs",
      "rules": ["HAW001"],
      "reason": "Interface is required by protocol code generation contract."
    }
  ]
}
```

---

## 4.2 Configuration schema

Create strongly typed configuration classes:

```text
HawthorneConfiguration
    Version
    Rules
    Exceptions

HawthorneRuleConfiguration
    Enabled
    Severity
    Rule-specific properties

HawthorneException
    File
    Rules
    Reason
```

Prefer dedicated strongly typed classes for rule-specific settings over an unstructured dictionary where practical.

Example:

```text
Hawthorne001Configuration
Hawthorne002Configuration
Hawthorne003Configuration
...
```

Alternatively use a shared base configuration plus typed extension properties.

---

## 4.3 Configuration parser

Create:

```text
Configuration/HawthorneConfigurationLoader.cs
```

Responsibilities:

1. Locate `hawthorne.json` among Roslyn `AdditionalFiles`.
2. Read file contents.
3. Deserialize JSON.
4. Validate schema and configuration values.
5. Normalize exception paths.
6. Return immutable configuration objects.
7. Fall back to built-in defaults when configuration is absent.
8. Produce a configuration diagnostic when the file is malformed.

Recommended diagnostic:

```text
HAW900 — Invalid Hawthorne configuration
```

Example:

```text
HAW900: hawthorne.json contains an invalid value for
HAW103.maximum. Expected an integer greater than zero.
```

Do not silently ignore malformed configuration.

---

## 4.4 File path normalization

Exception handling must work reliably across Windows, macOS, and Linux.

Normalize paths by:

1. Converting `\\` to `/`.
2. Removing leading `./`.
3. Resolving paths relative to the project root when possible.
4. Comparing with the appropriate platform-independent normalization strategy.
5. Avoiding dependence on absolute machine-specific paths.

Preferred configuration format:

```text
relative/path/from/project/root.cs
```

Example:

```text
Services/Legacy/UserServiceAdapter.cs
```

Do not encourage absolute paths in configuration.

---

# 5. Exception System

## 5.1 Requirements

Exceptions must:

- Apply to exactly one source file path entry.
- Identify one or more Hawthorne rule IDs.
- Require a non-empty reason.
- Be loaded only from `hawthorne.json`.
- Be checked before reporting diagnostics.
- Be visible and reviewable in source control.

Exceptions must not:

- Disable all rules globally.
- Use wildcard-only file matches.
- Depend on `#pragma warning disable`.
- Depend on `[SuppressMessage]`.
- Hide diagnostics without a recorded reason.

---

## 5.2 Exception evaluator

Create:

```text
Configuration/HawthorneExceptionEvaluator.cs
```

Primary API:

```csharp
bool IsExcepted(
    string diagnosticId,
    SyntaxTree syntaxTree);
```

or:

```csharp
bool IsExcepted(
    string diagnosticId,
    Location location);
```

The analyzer should perform this check immediately before reporting the diagnostic.

Common helper:

```csharp
context.ReportHawthorneDiagnostic(
    descriptor,
    location,
    configuration,
    arguments);
```

This helper should:

1. Check whether the rule is enabled.
2. Check whether the target file is excepted.
3. Resolve configured severity if supported internally.
4. Report the diagnostic only when applicable.

---

# 6. Solution Structure

Recommended repository structure:

```text
Hawthorne/
│
├── src/
│   ├── Hawthorne.Analyzers/
│   │   ├── Diagnostics/
│   │   │   ├── HawthorneDiagnosticDescriptors.cs
│   │   │   ├── HAW001SingleImplementationAnalyzer.cs
│   │   │   ├── HAW002TrivialFactoryAnalyzer.cs
│   │   │   ├── HAW003PassThroughAnalyzer.cs
│   │   │   ├── HAW004SingletonAnalyzer.cs
│   │   │   ├── HAW101CyclomaticComplexityAnalyzer.cs
│   │   │   ├── HAW102CognitiveComplexityAnalyzer.cs
│   │   │   ├── HAW103NestingAnalyzer.cs
│   │   │   ├── HAW104CouplingAnalyzer.cs
│   │   │   └── HAW105MethodLengthAnalyzer.cs
│   │   │
│   │   ├── Analysis/
│   │   │   ├── Complexity/
│   │   │   │   ├── CyclomaticComplexityCalculator.cs
│   │   │   │   ├── CognitiveComplexityCalculator.cs
│   │   │   │   └── NestingDepthCalculator.cs
│   │   │   ├── Architecture/
│   │   │   │   ├── ForwardingMethodDetector.cs
│   │   │   │   ├── FactoryBehaviorAnalyzer.cs
│   │   │   │   ├── InterfaceImplementationIndex.cs
│   │   │   │   ├── SingletonPatternDetector.cs
│   │   │   │   └── TypeDependencyCollector.cs
│   │   │   └── Metrics/
│   │   │       ├── MethodMetrics.cs
│   │   │       └── TypeMetrics.cs
│   │   │
│   │   ├── Configuration/
│   │   │   ├── HawthorneConfiguration.cs
│   │   │   ├── HawthorneConfigurationDefaults.cs
│   │   │   ├── HawthorneConfigurationLoader.cs
│   │   │   ├── HawthorneConfigurationValidator.cs
│   │   │   └── HawthorneExceptionEvaluator.cs
│   │   │
│   │   ├── Infrastructure/
│   │   │   ├── DiagnosticReportingExtensions.cs
│   │   │   ├── OperationExtensions.cs
│   │   │   ├── SymbolExtensions.cs
│   │   │   ├── SyntaxExtensions.cs
│   │   │   ├── PathNormalizer.cs
│   │   │   └── GeneratedCodeDetector.cs
│   │   │
│   │   └── HawthorneAnalyzerBootstrap.cs
│   │
│   └── Hawthorne.Analyzers.Tests/
│       ├── Configuration/
│       ├── Diagnostics/
│       ├── Analysis/
│       └── TestInfrastructure/
│
├── samples/
│   ├── GoodExamples/
│   └── ViolationExamples/
│
├── docs/
│   ├── rules/
│   └── configuration.md
│
├── hawthorne.example.json
└── README.md
```

---

# 7. Shared Analyzer Bootstrap

Create a common initialization layer.

The analyzer package must enable:

```csharp
context.EnableConcurrentExecution();
```

Generated code should normally be excluded using:

```csharp
context.ConfigureGeneratedCodeAnalysis(
    GeneratedCodeAnalysisFlags.None);
```

The configuration should be loaded once during compilation start and shared with analyzers.

Use:

```csharp
RegisterCompilationStartAction
```

where compilation-wide indexes or configuration state are needed.

Avoid repeatedly parsing `hawthorne.json` for every method or syntax node.

---

# 8. HAW001 — Redundant Single-Implementation Abstraction

## 8.1 Intent

Detect interfaces that introduce abstraction without demonstrated polymorphism or architectural value.

Bad example:

```csharp
public interface IUserService
{
    User Get(int id);
}

public sealed class UserService : IUserService
{
    public User Get(int id) => ...;
}
```

when `UserService` is the only implementation and the interface exists solely to wrap it.

---

## 8.2 Analysis strategy

Use compilation-wide symbol analysis.

Build an index:

```text
Interface -> Implementing Types
```

For every concrete class, inspect `AllInterfaces` and record implementation relationships.

At compilation completion, evaluate interfaces with exactly one concrete implementation.

---

## 8.3 Confidence model

Do not flag every one-implementation interface.

Assign confidence points.

Suggested model:

```text
+2 exactly one implementation in compilation
+2 interface and implementation in same assembly
+1 interface and implementation in same namespace
+1 names match IThing / Thing
+1 implementation is sealed
+1 interface has no default implementation behavior
+1 all known construction sites instantiate the same implementation
+1 only one DI registration binds the interface
```

Potential suppression signals:

```text
- external/public plugin boundary
- COM/export contract
- interface referenced by generated code contract
- assembly explicitly designed as abstraction package
- multiple implementations appear in test compilation
- marker interface
- default interface implementation contains meaningful behavior
```

Report only when:

```text
confidence >= configured minimumConfidence
```

Default:

```text
7
```

---

## 8.4 Diagnostic

```text
HAW001
Redundant abstraction
```

Suggested message:

```text
Interface '{0}' has only one detected implementation, '{1}', and no
polymorphic use was found. Remove the interface unless runtime variation
or an architectural boundary is required.
```

Diagnostic location should be the interface declaration.

---

# 9. HAW002 — Speculative Factory

## 9.1 Intent

Detect factories that simply wrap `new` without performing selection, configuration, lifecycle management, caching, pooling, or other meaningful behavior.

Example violation:

```csharp
public sealed class UserServiceFactory
{
    public IUserService Create()
    {
        return new UserService();
    }
}
```

---

## 9.2 Candidate identification

Default candidate type suffix:

```text
Factory
```

Potential future configurable suffixes:

```text
Provider
Builder
Creator
```

Only `Factory` should be enabled by default.

---

## 9.3 Operation analysis

Use `IOperation`.

For candidate creation methods determine:

- Number of constructed concrete types.
- Presence of `if` / `switch` / conditional expression.
- Configuration operations after construction.
- Caching.
- Pool lookup.
- Dependency lookup.
- Resource ownership behavior.
- Lifecycle operations.

Violation condition:

```text
exactly one concrete returned type
AND
no meaningful selection logic
AND
no meaningful configuration
AND
no meaningful lifecycle/resource behavior
```

Simple constructor argument forwarding alone should not count as meaningful behavior.

---

## 9.4 Diagnostic

```text
Factory '{0}' always constructs '{1}' and performs no meaningful selection,
configuration, or lifecycle behavior. Instantiate '{1}' directly.
```

---

# 10. HAW003 — Pass-Through Indirection

## 10.1 Intent

Detect classes and methods that add layers without adding behavior.

Example:

```csharp
public Task<User> GetUser(int id)
    => _service.GetUser(id);
```

---

## 10.2 Forwarding method definition

A method is considered forwarding when it primarily:

1. Accepts parameters.
2. Calls one underlying dependency.
3. Passes the same values through unchanged.
4. Returns the dependency result unchanged, or directly propagates `void`.
5. Does not materially mutate object or application state.
6. Does not transform arguments or results.
7. Does not add validation, orchestration, authorization, logging policy, retry behavior, transactions, caching, or other meaningful behavior.

---

## 10.3 Detection

Implement:

```text
ForwardingMethodDetector
```

Use `IOperation` to inspect:

```text
IReturnOperation
IInvocationOperation
IArgumentOperation
IConversionOperation
IAwaitOperation
IExpressionStatementOperation
```

Treat trivial `await` forwarding as forwarding:

```csharp
public async Task<User> GetUser(int id)
{
    return await _service.GetUser(id);
}
```

Treat argument reordering or transformation as non-trivial unless proved semantically equivalent.

---

## 10.4 Class-level forwarding ratio

For each class calculate:

```text
ForwardingRatio = ForwardingMethods / EligibleMethods
```

Default threshold:

```text
0.80
```

Default minimum forwarding methods:

```text
3
```

This avoids flagging tiny classes based on one method.

---

## 10.5 Diagnostics

Method-level diagnostic:

```text
Method '{0}' only forwards its arguments to '{1}' without adding behavior.
Remove the unnecessary indirection or add the responsibility that justifies it.
```

Class-level diagnostic:

```text
Type '{0}' forwards {1:P0} of its eligible methods to another dependency.
Consider removing or collapsing this layer.
```

Prefer one class-level diagnostic when the class clearly violates the forwarding ratio to avoid diagnostic spam.

---

# 11. HAW004 — Hidden Singleton State

## 11.1 Intent

Detect application-level singleton patterns that expose hidden shared mutable state through static access.

Candidate examples:

```csharp
public static Foo Instance { get; }
```

```csharp
public static Foo Current => _instance;
```

```csharp
public static Foo GetInstance() => _instance;
```

---

## 11.2 Semantic conditions

Detect when:

```text
static member returns containing type
OR
static method returns containing type
```

Then determine whether the containing type has mutable instance state.

Mutable state indicators:

- Non-readonly instance field.
- Settable instance property.
- Collection field that can mutate.
- Instance methods that mutate fields/properties.

If configuration has:

```json
"requireMutableState": true
```

then immutable stateless singleton-like values should not be reported.

---

## 11.3 Diagnostic

```text
Type '{0}' exposes a static shared instance and contains mutable state.
Prefer dependency injection with an explicit service lifetime.
```

---

# 12. HAW101 — Cyclomatic Complexity

## 12.1 Intent

Measure independent execution paths in a method.

Start each method at:

```text
1
```

Increment for configured branch constructs.

Initial set:

- `if`
- `else if`
- `for`
- `foreach`
- `while`
- `do`
- `case` with executable path
- `catch`
- conditional operator
- optionally logical short-circuit branches

---

## 12.2 Implementation

Prefer `IOperation` and/or `ControlFlowGraph` over token counting.

Create:

```text
CyclomaticComplexityCalculator
```

API:

```csharp
int Calculate(IOperation operation);
```

---

## 12.3 Diagnostic

```text
Method '{0}' has cyclomatic complexity {1}; maximum allowed is {2}.
```

---

# 13. HAW102 — Cognitive Complexity

## 13.1 Intent

Measure how difficult the method is to reason about, particularly by penalizing nesting.

---

## 13.2 Scoring model

Suggested baseline:

```text
if        +1 + nesting level
for       +1 + nesting level
foreach   +1 + nesting level
while     +1 + nesting level
do        +1 + nesting level
catch     +1 + nesting level
switch    +1
conditional expression +1 + nesting level
```

Additional penalties may later be added for:

- `goto`
- complex boolean chains
- deeply nested lambdas/local functions

The first version should remain deterministic and easy to explain.

---

## 13.3 Diagnostic

```text
Method '{0}' has cognitive complexity {1}; maximum allowed is {2}.
```

---

# 14. HAW103 — Maximum Nesting Depth

## 14.1 Intent

Prevent deeply nested control flow.

Count semantic nesting, not arbitrary braces.

Count:

- `if`
- `switch`
- `for`
- `foreach`
- `while`
- `do`
- `try`
- `catch`
- `lock`
- block-form `using`

Do not increment simply because a `BlockSyntax` exists.

---

## 14.2 Implementation

Create recursive visitor:

```text
NestingDepthCalculator
```

Maintain:

```text
currentDepth
maximumDepth
```

Default maximum:

```text
4
```

---

## 14.3 Diagnostic

```text
Method '{0}' reaches nesting depth {1}; maximum allowed is {2}.
```

---

# 15. HAW104 — Class Coupling

## 15.1 Intent

Measure how many distinct external concepts/types a class depends on.

---

## 15.2 Dependency sources

Collect types from:

- Constructor parameters.
- Fields.
- Properties.
- Base class.
- Implemented interfaces.
- Method parameter types.
- Return types.
- Object creation.
- Invoked owning types.
- Generic type arguments.
- Local variables where relevant.

Use a:

```csharp
HashSet<INamedTypeSymbol>
```

with symbol equality.

---

## 15.3 Framework filtering

Exclude common infrastructure wrappers that do not represent meaningful conceptual coupling.

Initial exclusions:

```text
System.String
System.Boolean
System.Byte
System.Int16
System.Int32
System.Int64
System.Decimal
System.Double
System.Single
System.DateTime
System.Guid
System.Threading.Tasks.Task
System.Threading.Tasks.ValueTask
System.Collections.Generic.List<T>
System.Collections.Generic.IEnumerable<T>
System.Collections.Generic.ICollection<T>
System.Collections.Generic.Dictionary<K,V>
System.Nullable<T>
```

For wrappers such as:

```text
Task<List<Customer>>
```

count `Customer`, not `Task` and `List`.

Do not hard-code an enormous list initially. Build a clear helper that can evolve.

---

## 15.4 Diagnostic

```text
Type '{0}' depends on {1} distinct external types; maximum allowed is {2}.
```

---

# 16. HAW105 — Method Length

## 16.1 Intent

Prevent methods from accumulating excessive responsibility.

Use two measurements:

```text
Executable statements
Physical source lines
```

Executable statement count is primary.

---

## 16.2 Executable statement count

Count executable statements while excluding:

- Empty statements.
- Comments.
- Documentation.
- Blank lines.
- Pure declaration syntax that generates no meaningful action where appropriate.

Default:

```text
30 executable statements
```

---

## 16.3 Physical lines

Use syntax location line spans.

Default:

```text
50 lines
```

Physical line checking may be independently configurable.

---

## 16.4 Diagnostic

Prefer the metric actually violated:

```text
Method '{0}' contains {1} executable statements; maximum allowed is {2}.
```

or:

```text
Method '{0}' spans {1} physical lines; maximum allowed is {2}.
```

---

# 17. Diagnostics Infrastructure

Create a central descriptor catalog:

```text
HawthorneDiagnosticDescriptors
```

Each descriptor must contain:

```text
ID
Title
Message format
Category
Default severity
Description
Help link
```

Suggested categories:

```text
Hawthorne.Architecture
Hawthorne.Complexity
Hawthorne.Configuration
```

---

# 18. Diagnostic Severity

Configuration accepts:

```text
error
warning
info
hidden
```

However, Roslyn diagnostic descriptors have static default severity semantics.

Implementation must determine the best Roslyn-compatible strategy for dynamic configured severity.

Preferred design options, in priority order:

1. Separate descriptor instances per supported severity.
2. Apply project-level Roslyn severity configuration generated or mapped from Hawthorne configuration if required.
3. Keep descriptor default at warning and document build-error promotion mechanism.

The implementation should avoid unsupported assumptions about mutating severity dynamically at reporting time.

Because the product goal is build gating, ensure the final integration allows Hawthorne violations configured as errors to fail the build.

---

# 19. Pragmas and Standard Roslyn Suppression

Hawthorne should not advertise or depend on pragma-based exceptions.

The intended governance model is:

```text
Violation
   ↓
Fix code
OR
Add documented file exception to hawthorne.json
```

The analyzer cannot necessarily prevent the compiler infrastructure from honoring every built-in Roslyn suppression mechanism in all hosting environments. Therefore the implementation requirement is:

- Hawthorne itself must never generate or recommend pragma suppression.
- Hawthorne's own exception logic must ignore pragma state.
- All officially supported exemptions must live in `hawthorne.json`.
- CI guidance should treat source-level suppression of `HAW*` diagnostics as prohibited repository policy if the host compiler permits it.

Optionally create a future companion rule that scans source text for:

```text
#pragma warning disable HAW
```

and reports it as a separate violation.

Potential diagnostic:

```text
HAW901 — Hawthorne pragma suppression is not permitted
```

This is recommended for the first release if technically reliable.

---

# 20. Testing Strategy

## 20.1 Test project

Use the standard Microsoft Roslyn analyzer testing packages.

Each rule requires:

```text
Positive tests
Negative tests
Boundary tests
Configuration tests
Exception tests
Cross-platform path tests
Malformed configuration tests
```

---

## 20.2 HAW001 tests

Test:

- One interface / one implementation / no polymorphism -> violation.
- One interface / two implementations -> no violation.
- One interface / one implementation but configured file exception -> no violation.
- Interface with external-boundary evidence -> no violation where heuristic supports it.
- Matching and non-matching interface/class naming.
- Generic interfaces.
- Nested classes.

---

## 20.3 HAW002 tests

Test:

- Factory returns one `new` -> violation.
- Factory with `if` selecting different implementations -> no violation.
- Factory with switch selecting types -> no violation.
- Factory with meaningful initialization -> no violation.
- Factory with trivial constructor forwarding -> violation.
- Factory exception by file -> no violation.

---

## 20.4 HAW003 tests

Test:

- Expression-bodied forwarding method.
- Normal block forwarding method.
- Async forwarding method.
- Forwarding with argument transformation -> no violation.
- Forwarding plus state mutation -> no violation.
- Forwarding plus validation -> no violation.
- Class below ratio -> no class violation.
- Class exactly at ratio -> verify configured boundary behavior.
- Minimum forwarding method threshold.

---

## 20.5 HAW004 tests

Test:

- Public static `Instance` with mutable state -> violation.
- Static `Current` with mutable state -> violation.
- Static `GetInstance()` -> violation.
- Immutable singleton-style value -> no violation when mutable state required.
- DI singleton registration without static instance -> no violation.

---

## 20.6 Complexity rule tests

For HAW101-105 create small deterministic examples where expected scores are explicitly documented.

Do not only test pass/fail.

Test calculators separately:

```text
input source -> exact expected metric
```

Then test analyzer threshold behavior separately.

This prevents diagnostic tests from hiding metric calculation bugs.

---

# 21. Performance Requirements

Roslyn analyzers execute during builds and IDE editing, so performance is important.

Requirements:

- Parse configuration once per compilation.
- Avoid repeated `SemanticModel` queries where symbols or operations are already available.
- Use immutable collections for shared compilation state after construction.
- Enable concurrent analysis where state is thread-safe.
- Avoid filesystem access outside Roslyn-provided `AdditionalFiles`.
- Avoid whole-compilation scans per syntax node.
- Cache interface implementation relationships.
- Cache normalized exception mappings.
- Avoid constructing CFGs unless the rule actually needs them.

Target principle:

```text
Cheap rules stay cheap.
Expensive semantic analysis occurs only for plausible candidates.
```

Example:

Do not perform full factory operation analysis on every class. First filter by class name ending in `Factory`.

---

# 22. Generated Code

Default behavior:

```text
Ignore generated code.
```

Recognize normal generated-code mechanisms through Roslyn configuration and common generated file conventions.

Potential future JSON option:

```json
{
  "analyzeGeneratedCode": false
}
```

Keep it false by default.

---

# 23. Documentation

Each diagnostic should have a documentation page:

```text
docs/rules/HAW001.md
docs/rules/HAW002.md
...
```

Each page should contain:

1. Rule purpose.
2. Why it exists.
3. Violation example.
4. Preferred alternative.
5. Legitimate exceptions.
6. Configuration keys.
7. File-scoped exception example.

Documentation should be written for both humans and coding agents: direct, deterministic, and example-heavy.

---

# 24. Implementation Phases

## Phase 1 — Repository and analyzer foundation

Deliverables:

- Solution and projects.
- NuGet analyzer project structure.
- Analyzer test infrastructure.
- Diagnostic descriptor catalog.
- Shared utility classes.
- Basic CI build.

Acceptance criteria:

- Empty analyzer package builds.
- Test project can execute analyzer tests.
- Sample project can consume analyzer package.

---

## Phase 2 — JSON configuration

Deliverables:

- `HawthorneConfiguration` model.
- JSON loader.
- Defaults.
- Validation.
- `HAW900` malformed configuration diagnostic.
- `AdditionalFiles` integration.

Acceptance criteria:

- Analyzer works without configuration using defaults.
- Valid JSON overrides defaults.
- Invalid JSON generates an actionable diagnostic.
- Configuration is loaded once per compilation.

---

## Phase 3 — File-scoped exception engine

Deliverables:

- Path normalization.
- Exception lookup table.
- Required `reason` validation.
- Shared diagnostic reporting helper.

Acceptance criteria:

- Exception suppresses only specified rules in specified file.
- Same diagnostic in another file is still reported.
- Empty reason is rejected.
- Wildcard global exception is rejected.
- Windows and Unix path separators produce equivalent matching.

---

## Phase 4 — HAW105 Method Length

Implement first because it is simple and validates the full reporting/configuration/exception pipeline.

Acceptance criteria:

- Executable statement threshold works.
- Physical line threshold works.
- File exception works.
- Rule enable/disable works.

---

## Phase 5 — HAW103 Maximum Nesting

Deliverables:

- `NestingDepthCalculator`.
- Analyzer integration.

Acceptance criteria:

- Semantic control nesting counted correctly.
- Arbitrary braces do not increase depth.
- Threshold configuration works.

---

## Phase 6 — HAW101 Cyclomatic Complexity

Deliverables:

- `CyclomaticComplexityCalculator`.
- Exact metric tests.
- Analyzer.

Acceptance criteria:

- Branch types are consistently counted.
- Metric output is deterministic.
- Threshold is configurable.

---

## Phase 7 — HAW102 Cognitive Complexity

Deliverables:

- Recursive complexity visitor.
- Nesting-weighted scoring.
- Exact score tests.

Acceptance criteria:

- Nested structures score higher than flat equivalent branching.
- Rule remains deterministic and documented.

---

## Phase 8 — HAW004 Hidden Singleton State

Deliverables:

- Self-type static-member detector.
- Mutable-state detector.
- Static factory method detection.

Acceptance criteria:

- Mutable global singleton patterns are detected.
- Normal DI singleton services are not detected.
- Immutable singleton-style constants can be excluded.

---

## Phase 9 — HAW002 Speculative Factory

Deliverables:

- Candidate factory filter.
- `FactoryBehaviorAnalyzer`.
- Object construction analysis.
- Conditional selection detection.
- Meaningful configuration detection.

Acceptance criteria:

- Trivial factory is reported.
- Multi-implementation factory is not.
- Factory with legitimate lifecycle/configuration work is not.

---

## Phase 10 — HAW003 Pass-Through Indirection

Deliverables:

- `ForwardingMethodDetector`.
- Parameter equivalence analysis.
- Result passthrough detection.
- Class forwarding ratio analysis.

Acceptance criteria:

- Direct forwarding is detected.
- Async direct forwarding is detected.
- Transformations prevent false positives.
- Class-level ratio works.
- Diagnostic spam is controlled.

---

## Phase 11 — HAW104 Class Coupling

Deliverables:

- `TypeDependencyCollector`.
- Wrapper/framework filtering.
- Unique semantic type counting.

Acceptance criteria:

- External conceptual dependencies are counted.
- Framework containers are filtered.
- Generic payload types are still counted.

---

## Phase 12 — HAW001 Redundant Abstraction

Implement last because it requires the most compilation-wide context and calibration.

Deliverables:

- `InterfaceImplementationIndex`.
- Confidence calculation.
- False-positive suppression heuristics.
- Compilation-end diagnostic reporting.

Acceptance criteria:

- Obvious `IFoo/Foo` single-implementation wrappers are detected.
- Multiple implementations suppress rule.
- Configured confidence threshold works.
- Known architectural boundary patterns avoid obvious false positives.

---

## Phase 13 — Anti-pragma enforcement

Recommended for first production release.

Implement:

```text
HAW901
```

Detect source directives attempting to suppress Hawthorne diagnostics, such as:

```csharp
#pragma warning disable HAW003
```

Diagnostic:

```text
Hawthorne diagnostics may not be suppressed with pragmas.
Add a documented file-scoped exception to hawthorne.json instead.
```

Acceptance criteria:

- Hawthorne pragma suppression is detected.
- Pragmas for unrelated compiler/analyzer diagnostics are ignored.

---

# 25. Calibration Phase

Before declaring HAW001-HAW004 production-ready as errors, run Hawthorne against multiple real repositories.

Suggested categories:

- ASP.NET Core application.
- Class library.
- CLI utility.
- Layered enterprise application.
- AI-generated sample project.
- Existing mature open-source project.

Record:

```text
Rule
Diagnostic location
True positive
False positive
Reason
Required heuristic adjustment
```

The goal is not zero diagnostics. The goal is low false-positive rates on architecture rules.

Complexity rules are deterministic and can be activated as errors earlier.

---

# 26. Coding-Agent Feedback Design

Diagnostic messages should tell an AI agent what action is expected.

Bad:

```text
HAW003: Excessive indirection.
```

Good:

```text
HAW003: Method 'GetUser' only forwards its arguments to
'IUserService.GetUser' without transformation or state changes.
Remove this method/layer or add the behavior that justifies it.
```

Diagnostics should preferably include:

1. What was measured.
2. Why the rule triggered.
3. Actual value where applicable.
4. Allowed threshold where applicable.
5. Preferred corrective action.

This makes Hawthorne useful as machine-readable feedback in an autonomous coding loop.

---

# 27. Optional Future Code Fixes

Do not make code fixes a prerequisite for the first analyzer release.

Potential later fixes:

```text
HAW001 -> Inline implementation / remove redundant interface
HAW002 -> Replace factory call with constructor
HAW003 -> Inline forwarding member
HAW004 -> Convert static singleton access to constructor dependency
HAW101-105 -> No automatic fix initially
```

Architecture-changing code fixes should be conservative and require explicit invocation.

---

# 28. Public API / Reusability

Keep metric calculators independent of Roslyn diagnostic reporting where possible.

Example:

```csharp
MethodMetrics metrics = MethodMetricsCalculator.Calculate(operation);
```

This allows future reuse by:

- Hawthorne CLI.
- Coding harnesses.
- Pull-request quality reports.
- IDE dashboards.
- Pre-commit checks.
- Repository health analysis.

Do not bury all analysis logic directly inside `DiagnosticAnalyzer` classes.

---

# 29. Potential Hawthorne CLI

Not required for first release, but preserve architectural compatibility with a future command such as:

```bash
hawthorne analyze MySolution.sln
```

Possible output:

```text
HAW003 Services/UserFacade.cs:42
Pass-through indirection detected.

HAW101 Services/InvoiceProcessor.cs:83
Cyclomatic complexity 17 exceeds configured maximum 10.
```

The same JSON configuration should eventually be reusable by both Roslyn and CLI execution.

---

# 30. Definition of Done — Version 1

Hawthorne v1 is complete when:

- Analyzer ships as a consumable NuGet analyzer package.
- All nine primary rules are implemented.
- `hawthorne.json` controls thresholds and rule enablement.
- Configuration parsing is validated and tested.
- File-specific exceptions work.
- Every exception requires a reason.
- No supported global exception mechanism exists.
- Hawthorne pragma suppression is either actively detected by HAW901 or explicitly prohibited/documented for CI enforcement.
- Generated code is ignored by default.
- Rules operate correctly in Visual Studio / Rider-compatible Roslyn environments and `dotnet build`.
- Every rule has positive and negative tests.
- Metric calculators have exact-value unit tests.
- Cross-platform exception paths are tested.
- Analyzer execution is concurrency-safe.
- Architecture rules have been calibrated against real repositories.
- Documentation exists for every rule.
- Diagnostic messages are useful to both humans and coding agents.

---

# 31. Recommended Initial Development Order

Execute implementation in this exact order:

```text
1. Solution/project skeleton
2. Test harness
3. Diagnostic descriptor infrastructure
4. hawthorne.json parser
5. Configuration validation / HAW900
6. File exception subsystem
7. Shared diagnostic-reporting infrastructure
8. HAW105 method length
9. HAW103 nesting
10. HAW101 cyclomatic complexity
11. HAW102 cognitive complexity
12. HAW004 singleton detection
13. HAW002 trivial factory detection
14. HAW003 pass-through analysis
15. HAW104 coupling analysis
16. HAW001 redundant abstraction analysis
17. HAW901 anti-pragma rule
18. Real-repository calibration
19. Documentation
20. NuGet packaging and CI integration
```

This sequence deliberately postpones the most heuristic rules until the underlying semantic-analysis infrastructure has already been proven by simpler diagnostics.

---

# 32. Architectural Outcome

Hawthorne should ultimately operate as a negative-feedback layer around code generation:

```text
Coding Agent
     │
     ▼
Generates / modifies C#
     │
     ▼
Compiler + Hawthorne
     │
     ├── clean ───────────────► accept change
     │
     └── HAW violation
              │
              ▼
      diagnostic feedback
              │
              ▼
        coding agent fixes
              │
              └───────────────► compile again
```

The important distinction from conventional linting is that Hawthorne evaluates not only whether code is valid, but whether architectural machinery appears to justify its own complexity.

The intended behavioral pressure is:

```text
Prefer direct code.
Introduce abstractions when they solve a demonstrated problem.
Keep methods understandable.
Keep dependency surfaces small.
Avoid layers that only relay calls.
Make exceptions explicit, local, and documented.
```

That philosophy should guide future Hawthorne rules as the analyzer expands.
