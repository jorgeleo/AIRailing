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
    public async Task Analyze_WhenMethodOnlyForwardsArguments_ReportsHAW003()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText("class Service { public int Get(int value) => value; } class Facade { private readonly Service service = new Service(); int Get(int value) => service.Get(value); }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();
        Assert.Equal("HAW003", Assert.Single(diagnostics).Id);
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
        Assert.Equal("HAW104", Assert.Single(diagnostics).Id);
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
    public async Task Analyze_WhenPragmaSuppressesHawthorneRule_ReportsHAW901()
    {
        var compilation = CSharpCompilation.Create("TestAssembly", new[] { CSharpSyntaxTree.ParseText("#pragma warning disable HAW003\nclass Example { }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap())).GetAnalyzerDiagnosticsAsync();
        Assert.Equal("HAW901", Assert.Single(diagnostics).Id);
    }

    private sealed class TestAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
