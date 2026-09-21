using System.Collections.Immutable;
using Hawthorne.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class HawthorneAnalyzerBootstrapTests
{
    [Fact]
    public async Task AnalyzeOrdinarySource_ProducesNoDiagnosticsBeforeRulesAreRegistered()
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText("public sealed class Example { }") },
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void SupportedDiagnostics_ExposeTheCompleteVersionOneCatalog()
    {
        var supportedIds = new HawthorneAnalyzerBootstrap()
            .SupportedDiagnostics
            .Select(descriptor => descriptor.Id)
            .OrderBy(id => id)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "HAW001", "HAW002", "HAW003", "HAW004", "HAW101", "HAW102", "HAW103", "HAW104", "HAW105", "HAW900", "HAW901",
            },
            supportedIds);
    }

    [Fact]
    public async Task Analyze_WhenConfigurationIsMalformed_ReportsCompilerErrorHAW900AtTheConfigurationFile()
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText("public sealed class Example { }") },
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var additionalFiles = ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", "{ \"version\": \"one\" }"));
        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions(additionalFiles),
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false);

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap()), options)
            .GetAnalyzerDiagnosticsAsync();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW900", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("/project/hawthorne.json", diagnostic.Location.GetLineSpan().Path);
    }

    [Fact]
    public async Task Analyze_WhenExceptionPathDoesNotMatchSource_ReportsCompilerErrorHAW900()
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText("public sealed class Example { }", path: "/project/Present.cs") },
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var additionalFiles = ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", """
                { "version": 1, "exceptions": [{ "file": "Missing.cs", "rules": ["HAW105"], "reason": "Required." }] }
                """));
        var options = new CompilationWithAnalyzersOptions(new AnalyzerOptions(additionalFiles), null, true, false, false);

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap()), options)
            .GetAnalyzerDiagnosticsAsync();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW900", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("does not match a source file", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenMethodExceedsExecutableStatementLimit_ReportsHAW105()
    {
        var statements = string.Concat(Enumerable.Repeat("int value = 0;", 31));
        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText($"class Example {{ void TooLong() {{ {statements} }} }}") },
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap()))
            .GetAnalyzerDiagnosticsAsync();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW105", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public async Task Analyze_WhenMethodExceedsExecutableStatementLimit_ReportsTheSplitInstructionOnce()
    {
        var statements = string.Concat(Enumerable.Repeat("int value = 0;", 31));
        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText($"class Example {{ void TooLong() {{ {statements} }} }}") },
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap()))
            .GetAnalyzerDiagnosticsAsync();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(
            "Method length exceeds the configured limit: Method 'TooLong' contains 31 executable statements; maximum allowed is 30. Split the method into focused operations.",
            diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenMethodExceedsPhysicalLineLimit_ReportsHAW105()
    {
        var blankLines = string.Concat(Enumerable.Repeat("\n", 50));
        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText($"class Example {{ void TooLong() {{{blankLines}return; }} }}") },
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.Equal("HAW105", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenMethodLengthIsExceptedForItsFile_DoesNotReportHAW105()
    {
        var statements = string.Concat(Enumerable.Repeat("int value = 0;", 31));
        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText($"class Example {{ void TooLong() {{ {statements} }} }}", path: "/project/Legacy.cs") },
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var additionalFiles = ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", """
                { "version": 1, "exceptions": [{ "file": "Legacy.cs", "rules": ["HAW105"], "reason": "Legacy boundary." }] }
                """));
        var options = new CompilationWithAnalyzersOptions(new AnalyzerOptions(additionalFiles), null, true, false, false);

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap()), options)
            .GetAnalyzerDiagnosticsAsync();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenCyclomaticComplexityExceedsDefault_ReportsHAW101()
    {
        var branches = string.Concat(Enumerable.Range(0, 10).Select(index => $"if (value == {index}) {{ }}"));
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText($"class Example {{ void Complex(int value) {{ {branches} }} }}") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();

        Assert.Equal("HAW101", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenCognitiveComplexityExceedsConfiguredMaximum_ReportsHAW102()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("class Example { void Complex() { if (true) { for (;;) { } } } }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var options = new CompilationWithAnalyzersOptions(new AnalyzerOptions(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", "{ \"version\": 1, \"rules\": { \"HAW102\": { \"maximum\": 2 } } }"))), null, true, false, false);

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap()), options).GetAnalyzerDiagnosticsAsync();

        Assert.Equal("HAW102", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenExpressionBodiedMethodExceedsCognitiveMaximum_ReportsHAW102()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("class Example { int Pick(int a, int b) => a > 1 ? (b > 2 ? (a + b > 3 ? 1 : 2) : 3) : 4; }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var options = new CompilationWithAnalyzersOptions(new AnalyzerOptions(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", "{ \"version\": 1, \"rules\": { \"HAW102\": { \"maximum\": 2 } } }"))), null, true, false, false);

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap()), options).GetAnalyzerDiagnosticsAsync();

        Assert.Equal("HAW102", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenExpressionBodiedMethodContainsNestedLambdaControlFlow_ReportsHAW103()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("""
                using System.Collections.Generic;
                class Example { void Run(List<int> values) => values.ForEach(value => { if (value > 0) { if (value > 1) { } } }); }
                """) },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var options = new CompilationWithAnalyzersOptions(new AnalyzerOptions(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", "{ \"version\": 1, \"rules\": { \"HAW103\": { \"maximum\": 1 } } }"))), null, true, false, false);

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap()), options).GetAnalyzerDiagnosticsAsync();

        Assert.Equal("HAW103", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenStaticSelfInstanceHasMutableState_ReportsHAW004()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("class State { public static State Instance { get; } = new State(); public int Value { get; set; } }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();

        Assert.Equal("HAW004", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenStaticSelfInstanceIsImmutable_DoesNotReportHAW004ByDefault()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("class Value { public static Value Instance { get; } = new Value(); }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenStaticSelfFactoryCreatesFreshInitOnlyValues_DoesNotReportHAW004()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("class Result { public bool Success { get; init; } public static Result SuccessResult => new Result { Success = true }; public static Result Create() => new Result { Success = true }; }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenFactoryOnlyConstructsOneType_ReportsHAW002()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("class Item { } class ItemFactory { Item Create() => new Item(); }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();

        Assert.Equal("HAW002", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenFactorySelectsOrInitializes_DoesNotReportHAW002()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("""
                class Item { public int Value { get; set; } } class Other { }
                class ItemFactory { object Create(bool alternate) { if (alternate) return new Other(); return new Item(); } Item CreateConfigured() => new Item { Value = 1 }; }
                """) },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenFactoryUsesTargetTypedNew_ReportsHAW002()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("class Item { } class ItemFactory { Item Create() => new(); }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW002", diagnostic.Id);
    }

    [Fact]
    public async Task Analyze_WhenMethodOnlyForwardsArguments_ReportsHAW003()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("class Service { public int Get(int value) => value; } class Facade { private readonly Service service = new Service(); int Get(int value) => service.Get(value); }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();
        Assert.Equal("HAW003", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenOverrideForwardsToItsDependency_DoesNotReportHAW003()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("abstract class Base { public abstract int Get(int value); } class Service { public int Get(int value) => value; } class Adapter : Base { private readonly Service service = new Service(); public override int Get(int value) => service.Get(value); }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenImplicitInterfaceImplementationForwardsToItsDependency_DoesNotReportHAW003()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("interface IWorker { int Get(int value); } class Service { public int Get(int value) => value; } class Adapter : IWorker { private readonly Service service = new Service(); public int Get(int value) => service.Get(value); } class Alternative : IWorker { public int Get(int value) => value; }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenZeroArgumentMethodDelegatesToPrivateWork_DoesNotReportHAW003()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("class Example { bool IsReady() => CanAccessDatabase(); bool CanAccessDatabase() => true; }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenZeroArgumentMethodPerformsAFluentQuery_DoesNotReportHAW003()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("class Result { } class Query { public Query Filter() => this; public Result Finish() => new Result(); } class Example { private readonly Query query = new Query(); Result Find() => query.Filter().Finish(); }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenMethodTransformsAnArgument_DoesNotReportHAW003()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("class Service { public int Get(int value) => value; } class Facade { private readonly Service service = new Service(); int Get(int value) => service.Get(value + 1); }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTypeMeetsForwardingRatio_ReportsOneTypeLevelHAW003()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("class Service { public int A(int v) => v; public int B(int v) => v; public int C(int v) => v; } class Facade { private readonly Service s = new Service(); int A(int v) => s.A(v); int B(int v) => s.B(v); int C(int v) => s.C(v); }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW003", diagnostic.Id);
        Assert.Equal("Facade", diagnostic.Location.SourceTree!.GetRoot().FindToken(diagnostic.Location.SourceSpan.Start).ValueText);
    }

    [Fact]
    public async Task Analyze_WhenAsyncMethodOnlyAwaitsForwardedInvocation_ReportsHAW003()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("class Service { public System.Threading.Tasks.Task<int> Get(int v) => null; } class Facade { private readonly Service s = new Service(); async System.Threading.Tasks.Task<int> Get(int v) { return await s.Get(v); } }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location), MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location) });
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();
        Assert.Equal("HAW003", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenClassDependsOnMoreThanTwelveConcepts_ReportsHAW104()
    {
        var types = string.Concat(Enumerable.Range(1, 13).Select(index => $"class T{index} {{ }}"));
        var fields = string.Concat(Enumerable.Range(1, 13).Select(index => $"T{index} f{index};"));
        var compilation = CSharpCompilation.Create("TestAssembly", new[] { CSharpSyntaxTree.ParseText(types + "class Host {" + fields + "}") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW104", diagnostic.Id);
        Assert.Contains("Examples:", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenCouplingExceedsConfiguredMaximum_ReportsDependencyCategories()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("""
                class ApiRequest { }
                class ApiResponse { }
                class FieldDependency { }
                class ConstructorDependency { }
                class LocalDependency { }
                class CreatedDependency { }
                class InvokedDependency { public void Run() { } }
                class Host
                {
                    private FieldDependency field;
                    public Host(ConstructorDependency dependency) { }
                    public ApiResponse Handle(ApiRequest request)
                    {
                        var local = new LocalDependency();
                        var created = new CreatedDependency();
                        new InvokedDependency().Run();
                        return default;
                    }
                }
                """) },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var options = new CompilationWithAnalyzersOptions(new AnalyzerOptions(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", "{ \"version\": 1, \"rules\": { \"HAW104\": { \"maximum\": 6 } } }"))), null, true, false, false);

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap()), options).GetAnalyzerDiagnosticsAsync();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW104", diagnostic.Id);
        Assert.Contains("API surface: 2; state/dependency: 2; implementation: 3", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenTypeIsUsedByMultipleSources_CountsItOnceOverall()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("""
                class Shared { }
                class Other { }
                class Host
                {
                    private Shared field;
                    public Host(Shared dependency) { }
                    public Other Get(Shared request)
                    {
                        var local = new Shared();
                        return new Other();
                    }
                }
                """) },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var options = new CompilationWithAnalyzersOptions(new AnalyzerOptions(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", "{ \"version\": 1, \"rules\": { \"HAW104\": { \"maximum\": 1 } } }"))), null, true, false, false);

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap()), options).GetAnalyzerDiagnosticsAsync();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW104", diagnostic.Id);
        Assert.Contains("depends on 2 distinct external types", diagnostic.GetMessage());
        Assert.Contains("API surface: 2; state/dependency: 1; implementation: 2", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenClassMeetsCouplingMaximum_DoesNotReportHAW104()
    {
        var types = string.Concat(Enumerable.Range(1, 12).Select(index => $"class T{index} {{ }}"));
        var fields = string.Concat(Enumerable.Range(1, 12).Select(index => $"T{index} f{index};"));
        var compilation = CSharpCompilation.Create("TestAssembly", new[] { CSharpSyntaxTree.ParseText(types + "class Host {" + fields + "}") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenGenericDependencyExceedsConfiguredMaximum_ReportsTheTypeArgument()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("class Customer { } class Envelope<T> { } class Host { private Envelope<Customer> value; }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var options = new CompilationWithAnalyzersOptions(new AnalyzerOptions(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", "{ \"version\": 1, \"rules\": { \"HAW104\": { \"maximum\": 1 } } }"))), null, true, false, false);

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap()), options).GetAnalyzerDiagnosticsAsync();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW104", diagnostic.Id);
        Assert.Contains("Customer", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenTransparentGenericWrapperIsNormalized_DoesNotRecurse()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("class Customer { } class Host { private System.Collections.Generic.List<Customer> value; }") },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
            });
        var options = new CompilationWithAnalyzersOptions(new AnalyzerOptions(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", "{ \"version\": 1, \"rules\": { \"HAW104\": { \"maximum\": 1 } } }"))), null, true, false, false);

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap()), options)
            .GetAnalyzerDiagnosticsAsync();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenNestedTypeUsesDependency_DoesNotCountItForTheEnclosingType()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("class DirectDependency { } class NestedDependency { } class Outer { private DirectDependency direct; class Nested { void Use() { var value = new NestedDependency(); } } }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var options = new CompilationWithAnalyzersOptions(new AnalyzerOptions(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", "{ \"version\": 1, \"rules\": { \"HAW104\": { \"maximum\": 1 } } }"))), null, true, false, false);

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap()), options).GetAnalyzerDiagnosticsAsync();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenInterfaceHasOneConcreteImplementation_ReportsHAW001()
    {
        var compilation = CSharpCompilation.Create("TestAssembly", new[] { CSharpSyntaxTree.ParseText("interface IWorker { } class Worker : IWorker { }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();
        Assert.Equal("HAW001", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenInterfaceHasMultipleConcreteImplementations_DoesNotReportHAW001()
    {
        var compilation = CSharpCompilation.Create("TestAssembly", new[] { CSharpSyntaxTree.ParseText("interface IWorker { } class FirstWorker : IWorker { } class SecondWorker : IWorker { }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenOnlyAReferencedInterfaceHasOneImplementation_DoesNotReportHAW001()
    {
        var compilation = CSharpCompilation.Create("TestAssembly", new[] { CSharpSyntaxTree.ParseText("class Worker : System.IDisposable { public void Dispose() { } }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenPragmaSuppressesHawthorneRule_ReportsHAW901()
    {
        var compilation = CSharpCompilation.Create("TestAssembly", new[] { CSharpSyntaxTree.ParseText("#pragma warning disable HAW003\nclass Example { }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW901", diagnostic.Id);
        Assert.Contains("HAW003", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenBarePragmaDisablesAllWarnings_ReportsHAW901ForAllDiagnostics()
    {
        var compilation = CSharpCompilation.Create("TestAssembly", new[] { CSharpSyntaxTree.ParseText("#pragma warning disable\nclass Example { }\n#pragma warning restore") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW901", diagnostic.Id);
        Assert.Contains("all diagnostics", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenPragmaDisablesOnlyForeignCodes_DoesNotReportHAW901()
    {
        var compilation = CSharpCompilation.Create("TestAssembly", new[] { CSharpSyntaxTree.ParseText("#pragma warning disable 0219\nclass Example { }\n#pragma warning restore") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();
        Assert.Empty(diagnostics);
    }

    private sealed class TestAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
