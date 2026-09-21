What this repo is

Hawthorne is a Roslyn analyzer NuGet package (Hawthorne.Analyzers, v0.1.3, netstandard2.0) that detects  
 over-engineering in C# — its stated purpose is catching the structural footprint LLMs leave: single-implementation  
 interfaces, trivial factories, pass-through indirection, hidden singleton state, plus classic complexity metrics.

- 11 rules: HAW001–004 (architecture), HAW101–105 (complexity), HAW900 (invalid config → compile error), HAW901  
  (pragma suppression → warning)
- Governance model: one hawthorne.json per project (versioned, strict validation), per-rule  
  enable/severity/thresholds, file-scoped exceptions requiring exact paths + reasons; pragmas are the forbidden  
  suppression path
- Layout: src/Hawthorne.Analyzers (13 files, ~700 lines), src/Hawthorne.Analyzers.Tests (45 tests), samples/Consumer,  
  Docs/ (1,851-line implementation plan, per-rule docs), GitHub Packages publish workflow
- State: builds clean, all tests pass; working tree has an uncommitted 0.1.2→0.1.3 bump  


What's genuinely good (don't touch): the config loader is tight (wildcard rejection, one-file rule, exception-path  
 verification, compile-error on bad config); HAW003 is well-designed (excludes overrides/interface impls,  
 argument-identity by name, await-aware, type-level ratio); I verified symbol-action rules don't pollute  
 referenced-assembly builds; docs match behavior. This is a coherent, appropriately-scoped package — ironically not  
 over-engineered at all.

So: improvements are needed, but they're small and surgical. In priority order:

A. Real behavior bugs (all verified empirically)

1.  HAW105 prints its instruction twice. HawthorneDiagnosticDescriptors.cs:55 ends the messageFormat with "Split the  
    method into focused operations." and HAW105MethodLengthAnalyzer.cs:25,33 passes an arg that also ends with that  
    sentence. Observed in a real build: ...maximum allowed is 30. Split the method into focused operations.. Split the  
    method into focused operations. (double period, doubled). Fix: make the format "...limit: {0}." or pass structured  
    values like HAW101–104 do.  

2.  HAW901 has a governance hole: bare #pragma warning disable. HAW901PragmaSuppressionAnalyzer.cs:17-18 returns when  
    the directive has no codes — but a codeless disable suppresses all warnings, including HAW\*. I wrapped a singleton in  
    a bare pragma: HAW004 vanished with no HAW901. That defeats the package's core contract (Docs/rules/HAW901.md,  
    ImplementationPlan §2.4: "suppression must flow through hawthorne.json"). Fix: report when a disable directive has  
    zero codes (it suppresses the whole HAW catalog). Side note: the StartsWith("HAW") match could also catch a foreign  
    analyzer's HAW… IDs — matching against your own catalog is cleaner.  

3.  HAW102/HAW103 score expression-bodied methods as 0. CognitiveComplexityCalculator.cs:12 and  
    NestingDepthCalculator.cs:12 call visitor.Visit(method.Body) — which is null for => methods, so the walk is a no-op.  
    HAW101 does it right (CyclomaticComplexityCalculator.cs:12 visits method.Body ?? ExpressionBody), so the three rules  
    are internally inconsistent. Verified: int M(int a, int b) => a > 1 ? (b > 2 ? (a + b > 3 ? 1 : 2) : 3) : 4; with  
    HAW102 maximum: 2 → no diagnostic (it would score 6). Expression-bodied methods are the modern style; this rule  
    currently ignores them entirely. Fix: same ?? ExpressionBody pattern + a documented decision on lambdas.  

4.  HAW002 misses target-typed new. HAW002TrivialFactoryAnalyzer.cs:28-30 only matches ObjectCreationExpressionSyntax.  
    Side-by-side probe in a WidgetFactory: MakeExplicit() => new Widget(); → fires; Make() => new(); → silent.  
    Target-typed new is the default idiom for exactly the factories this rule targets — high-value one-line-ish fix.  


B. Release blockers

5.  No license exists. No LICENSE file, no PackageLicenseExpression in the csproj (only  
    PackageRequireLicenseAcceptance=false at :14), no <license> in the packed nuspec. NuGet.org rejects new packages  
    without license metadata. TODO.md has "Choose the release version and license" checked — it wasn't actually done.  

6.  No CI gate on PRs. The only workflow (.github/workflows/publish-nuget.yml) triggers on tag/workflow_dispatch — PRs  
    merge untested. (TODO.md claims "CI build/test gate" is done.) A pull_request job running dotnet test costs you ~1  
    second.  

7.  xunit v2/v3 skew in Hawthorne.Analyzers.Tests.csproj:10-11 — xunit 2.9.3 with xunit.runner.visualstudio 3.1.4. It  
    happens to work (v3 runner hosts v2 tests; 45/45 pass) but it's a maintenance trap. Pick one major.  


C. Polish

8.  README is 7 lines and doubles as the NuGet package page. Add install snippet (feed URL + dotnet add package), the  
    11-rule table, a minimal hawthorne.json example, links into Docs/rules/.
9.  Docs/general idea.md is a raw LLM dump — duplicated sections, placeholder citations ([1] https://medium.com with no
    URL), trailing chat residue ("If you want, I can share:"). Archive or clean it; it currently misrepresents the  
    project.
10. TODO.md contradicts itself: status says "awaiting user-owned release readiness" while every user-owned box  
    (including the license claim in #5) is checked.
11. HawthorneConfigurationLoader.cs:96,102,108,120 — error.Replace("HAW105", "HAW103") string-rewrite hack to build  
    error messages; thread the rule ID into GetPositiveInteger.
12. CouplingInventory.cs:54-55 — IsTransparentWrapper matches bare type names, so a user domain type named Task or  
    List in its own namespace is silently excluded from HAW104 coupling. Match fully-qualified names.
13. Hygiene: untracked splash.sh at repo root (a one-line local tooling command); artifacts/packages/ holds 0.1.1 +  
    0.1.3 nupkgs of mixed provenance.  


Checked and fine (no action needed)

- Reference-assembly pollution: I built a consumer against a library containing a mutable singleton and a 13-coupling  
  type — zero diagnostics from referenced types (Roslyn doesn't run symbol actions on metadata symbols here). One  
  optional hardening: add an explicit symbol.Locations.IsInSource guard + a metadata-reference test so the source-only
  scope survives Roslyn behavior changes. Related nuance for HAW001: implementation counts are source-only, so a  
  source interface with 1 source + 1 library implementation still fires — that's documented in Docs/rules/HAW001.md,  
  just know it can surprise.
- Config contract, per-rule docs, build hygiene (TreatWarningsAsErrors, EnforceExtendedAnalyzerRules), and the 45  
  tests are all solid.  


Bottom line: a genuinely good foundation — no rewrite needed. The four items in section A are small, user-visible  
 correctness fixes; #5 and #6 block any public feed. Want me to implement any of these?
