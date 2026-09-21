# Hawthorne

Hawthorne is a Roslyn analyzer package for C# that detects **over-engineering**:
unnecessary abstraction, pass-through indirection, hidden global state, and
structural complexity. It is aimed at codebases with many small seams — the
kind of layered code that LLMs and busy humans tend to generate when adding an
interface, factory, or wrapper without a real boundary behind it.

Every rule is a **warning by default**, and the package works with zero
configuration. Add a `hawthorne.json` file to a project to tune thresholds,
disable rules, or document file-scoped exceptions.

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

| Rule | What it flags | Default |
| --- | --- | --- |
| HAW001 | Interfaces with exactly one implementation in the current compilation | 1 |
| HAW002 | `Factory` methods that only construct one type directly | — |
| HAW003 | Methods that forward unchanged arguments to a dependency | 3+ methods, or 80%+ of a type |
| HAW004 | Stored static shared instances with mutable state | — |
| HAW101 | Cyclomatic complexity | 10 |
| HAW102 | Cognitive complexity | 15 |
| HAW103 | Control-flow nesting depth | 4 |
| HAW104 | Class coupling (distinct referenced types) | 12 |
| HAW105 | Method length | 30 statements / 50 lines |
| HAW900 | Invalid `hawthorne.json` | compile error |
| HAW901 | `#pragma warning disable` governing Hawthorne rules | — |

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
    "HAW101": { "maximum": 12 },
    "HAW102": { "maximum": 20 },
    "HAW103": { "maximum": 3 },
    "HAW104": { "maximum": 16 },
    "HAW105": { "maximumExecutableStatements": 40, "maximumPhysicalLines": 80 }
  },
  "exceptions": [
    {
      "file": "/src/Legacy/GeneratedController.cs",
      "rules": ["HAW003", "HAW105"],
      "reason": "Generated controller; hand-editing is not supported."
    }
  ]
}
```

- `version` must be `1`.
- Every rule accepts `enabled` (boolean) and `severity`
  (`error`, `warning`, `info`, or `hidden`).
- Metric rules accept their documented `maximum` values.
- Each `exceptions` entry requires an exact project-relative path (wildcards
  are rejected), the rule IDs it covers, and a non-empty `reason`.

An invalid configuration file is reported as **HAW900** (a compiler error) and
normal analysis is skipped for that compilation, so a bad configuration can
never silently change what Hawthorne reports. An exception path that matches
no source file in the compilation is also reported as HAW900.

## Suppression

Prefer file-scoped exceptions in `hawthorne.json` over `#pragma warning
disable`:

- A pragma that names any Hawthorne rule ID is reported as **HAW901**.
- A codeless `#pragma warning disable` suppresses every warning, all of
  Hawthorne's rules included, so it is reported as well.

Keeping suppression decisions in configuration makes them reviewable in one
place instead of scattered through source files.

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
