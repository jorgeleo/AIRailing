# Hawthorne

Hawthorne is a Roslyn analyzer package for C# that detects **over-engineering**:
unnecessary abstraction, pass-through indirection, hidden global state, and
structural complexity. It is aimed at codebases with many small seams — the
kind of layered code that LLMs and busy humans tend to generate when adding an
interface, factory, or wrapper without a real boundary behind it.

Quality rules are warnings by default (HAW100 is informational and HAW900 is a
compiler error). Add a `hawthorne.json` file to a project to tune thresholds,
disable review rules, or tune severity. HAW901 makes an attempted source
suppression of HAW100–HAW106, HAW900, or HAW901 an error.

## Installation

```powershell
dotnet add package Hawthorne.Analyzers
```

The analyzers run in Visual Studio, Visual Studio Code, and `dotnet build`
with no source changes required.

### GitHub Packages feed

Hawthorne is currently published to the author's GitHub Packages feed. Add it
to your `nuget.config`:

```xml
<configuration>
  <packageSources>
    <add key="github" value="https://nuget.pkg.github.com/jorgeleo/index.json" />
  </packageSources>
</configuration>
```

The .NET SDK resolves GitHub Packages with your `GITHUB_TOKEN` automatically.

## What it detects

| Rule   | What it flags                                                         | Default                       |
| ------ | --------------------------------------------------------------------- | ----------------------------- |
| HAW001 | Interfaces with exactly one implementation in the current compilation | 1                             |
| HAW002 | `Factory` methods that only construct one type directly               | —                             |
| HAW003 | Methods that forward unchanged arguments to a dependency              | 3+ methods, or 80%+ of a type |
| HAW004 | Stored static shared instances with mutable state                     | —                             |
| HAW005 | Wrapper-style types that mostly forward to one dependency             | 3 methods / 80%               |
| HAW006 | Abstract or generic abstractions with one demonstrated use            | warning                       |
| HAW007 | Constructors with too many service-like dependencies                  | 7                             |
| HAW008 | Boolean parameters controlling separate branches                      | 2 warning / 3 error           |
| HAW010 | Small one-method service-style classes                                | 25 lines / complexity 2       |
| HAW011 | Types dominated by tiny private methods                               | 8 methods / 60%               |
| HAW013 | Generic exception wrapping without meaningful context                 | warning                       |
| HAW014 | Proven-redundant private non-null guards                              | opt-in evidence               |
| HAW015 | Unread private configuration/options properties                       | warning                       |
| HAW016 | Async APIs that only complete synchronously                           | warning                       |
| HAW017 | Unnecessary immediate LINQ materialization                            | warning                       |
| HAW018 | Repeated enumeration of an enumerable source                          | 2 consumers                   |
| HAW021 | Silent default-return catches and dense try/catch types               | 5 methods / 0.75              |
| HAW024 | Cancellation tokens that are not used or forwarded                    | warning                       |
| HAW020 | EF Core repository methods that mostly mirror ORM operations          | warning                       |
| HAW023 | Private events, callbacks, or hooks with no source consumer           | warning                       |
| HAW025 | Configuration/options forwarded unchanged across source boundaries    | warning                       |
| HAW029 | Lifecycle logging repeated across most methods in a type              | warning                       |
| HAW030 | Clusters of structurally near-duplicate methods                       | warning                       |
| HAW100 | Abstraction-to-behavior health density metric                         | info                          |
| HAW101 | Cyclomatic complexity                                                 | 10                            |
| HAW102 | Cognitive complexity                                                  | 15                            |
| HAW103 | Control-flow nesting depth                                            | 4                             |
| HAW104 | Class coupling (distinct referenced types)                            | 12                            |
| HAW105 | Method length                                                         | 30 statements / 50 lines      |
| HAW106 | Multiple executable statements in a method body on one line            | warning                       |
| HAW900 | Invalid `hawthorne.json`                                              | compile error                 |
| HAW901 | Invalid Hawthorne suppression or `#pragma warning disable`            | warning / error               |

Each rule has a dedicated page in [Docs/rules/](Docs/rules/) with what it
reports, what it deliberately ignores, and its configuration knobs.

## Configuration

Hawthorne reads at most one `hawthorne.json` per compilation from the project's
`AdditionalFiles`. Put the file beside the project file:

```
MyProject/
  MyProject.csproj
  hawthorne.json
```

and reference it:

```xml
<ItemGroup>
  <AdditionalFiles Include="hawthorne.json" />
</ItemGroup>
```

A complete example:

```json
{
  "version": 1,
  "rules": {
    "HAW001": { "enabled": false },
    "HAW004": { "severity": "error", "requireMutableState": true },
    "HAW005": { "minimumForwardingMethods": 3, "minimumForwardingRatio": 0.8 },
    "HAW006": {
      "maximumSharedExecutableStatements": 2,
      "analyzeSingleClosedGenericUse": true
    },
    "HAW007": { "maximumDependencies": 7 },
    "HAW008": { "warningParameterCount": 2, "errorParameterCount": 3 },
    "HAW010": { "maximumPhysicalLines": 25, "maximumCyclomaticComplexity": 2 },
    "HAW011": {
      "maxTinyMethodRatio": 0.6,
      "tinyMethodStatementLimit": 2,
      "minimumMethodCount": 8
    },
    "HAW013": { "reportLogAndRethrow": false },
    "HAW014": { "includeInternalMethods": false },
    "HAW015": { "enabled": true, "includeInternalProperties": false },
    "HAW016": { "reportTrivialTaskRun": false },
    "HAW017": { "analyzeToList": true, "analyzeToArray": true },
    "HAW018": { "minimumEnumerations": 2 },
    "HAW021": { "minimumMethodCount": 5, "maximumTryBlocksPerMethod": 0.75 },
    "HAW024": {
      "reportMissingForwarding": true,
      "treatNoneAsMissingForwarding": true
    },
    "HAW020": {
      "enabled": true,
      "minimumForwardingMethods": 3,
      "minimumForwardingRatio": 0.8
    },
    "HAW023": { "enabled": true, "includePrivateMembers": true },
    "HAW025": { "enabled": true, "minimumForwardingHops": 2 },
    "HAW029": {
      "enabled": true,
      "minimumMethodCount": 5,
      "maximumLifecycleLogRatio": 0.8
    },
    "HAW030": {
      "enabled": true,
      "minimumMethods": 3,
      "minimumStatements": 3,
      "minimumSimilarity": 0.9
    },
    "HAW100": { "enabled": true, "minimumBehavioralTypes": 5 },
    "HAW101": { "maximum": 12 },
    "HAW102": { "maximum": 20 },
    "HAW103": { "maximum": 3 },
    "HAW104": { "maximum": 16 },
    "HAW105": { "maximumExecutableStatements": 40, "maximumPhysicalLines": 80 },
    "HAW106": { "enabled": true }
  }
}
```

- `version` must be `1`.
- `_comment` and `_potentialFix` properties are optional metadata for human and
  AI readers and are ignored by the analyzer. `_potentialFix` values should
  guide code changes; strongly prefer those fixes over suppression.
- Every quality rule accepts `enabled` (boolean) and `severity`
  (`error`, `warning`, `info`, or `hidden`). HAW901's error validation for a
  non-suppressible rule cannot be disabled or downgraded.
- Metric rules accept their documented `maximum` values.
- HAW106 is enabled by default; set `"enabled": false` to opt out. It reports
  block-bodied methods with multiple executable statements on one physical
  line, and only reports diagnostics without editing files.

See [`samples/Consumer/hawthorne.json`](samples/Consumer/hawthorne.json) for a
complete annotated configuration containing every supported diagnostic.

The NuGet package includes that annotated configuration as a content asset. On
the first consumer build, it is copied beside the consuming `.csproj` as
`hawthorne.json` and registered as an `AdditionalFiles` input when no file is
already present. Set `HawthorneAutoCreateConfiguration` to `false` to opt out;
an existing file is never overwritten.

An invalid configuration file is reported as **HAW900** (a compiler error) and
normal analysis is skipped for that compilation, so a bad configuration can
never silently change what Hawthorne reports.

## Prompting.

After the build report the errors and warnings, use this prompt:

```text
There are HAW* warnings, look at hawthorne.json on how to probably resolve them. if you absolutely cannot resolve a warning by code changes, then use the suppress attribute with a justification that needs to explain why you had to suppress that instance and point to verifiable evidence. Do not suppress what can be resolved
```

## Suppression

Use `SuppressMessageAttribute` for an intentional, source-scoped suppression.
The `Justification` must be non-empty:

```csharp
using System.Diagnostics.CodeAnalysis;

[SuppressMessage(
    "Hawthorne.Architecture",
    "HAW001",
    Justification = "The independently published plugin contract currently has one implementation.")]
interface IPluginContract { }
```

- A pragma that names any Hawthorne rule ID is reported as **HAW901**.
- A codeless `#pragma warning disable` suppresses every warning, all of
  Hawthorne's rules included, so it is reported as well.
- A Hawthorne `SuppressMessageAttribute` without a non-blank `Justification`
  is reported as **HAW901**.
- HAW100–HAW106 must be remediated rather than source-suppressed; a
  `SuppressMessageAttribute` or `#pragma` attempt is reported as an
  **error HAW901**. HAW900 (configuration validity) and HAW901 itself have the
  same protection.
- Other rule IDs may use a specific, non-blank justification when a real
  external contract, protocol, provider, reflection boundary, or compatibility
  obligation makes the local code shape intentional.

Keeping the justification next to the suppressed code makes the decision
reviewable at the point where it applies.

## Trying it out

`samples/Consumer` is a minimal console project with the analyzer attached and
a `hawthorne.json` that disables HAW001:

```powershell
dotnet run --project samples/Consumer
```

## Building from source

```powershell
dotnet build Hawthorne.sln
dotnet test Hawthorne.sln
```

The package contains the analyzers under `analyzers/dotnet/cs` plus the README
and license.

## License

MIT — see [LICENSE](LICENSE).
