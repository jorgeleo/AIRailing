using System.Collections.Immutable;
using Hawthorne.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
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
}
