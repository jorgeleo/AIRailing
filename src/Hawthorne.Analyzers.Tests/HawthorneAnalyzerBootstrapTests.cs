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

    private sealed class TestAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
