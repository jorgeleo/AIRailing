using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class CompilationWideDeterminismTests
{
    [Fact]
    public async Task PrematureGeneralization_IsStableAcrossSourceFilesAndConcurrentRuns()
    {
        var sources = new[] { "abstract class Base { }", "sealed class Child : Base { }" };
        var runs = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ =>
            AnalyzerTestHost.AnalyzeSourcesAsync(sources, """{ "version": 1, "rules": { "HAW006": { "enabled": true } } }""")));

        AssertStableSingleDiagnostic(runs, "HAW006");
    }

    [Fact]
    public async Task OneMethodService_IndexesCallersAcrossSourceFilesDeterministically()
    {
        var sources = new[]
        {
            "class DataService { public void Run() { } }",
            "class Caller { void Use(DataService service) { service.Run(); } }",
        };
        var runs = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ =>
            AnalyzerTestHost.AnalyzeSourcesAsync(sources, """{ "version": 1, "rules": { "HAW010": { "enabled": true } } }""")));

        AssertStableSingleDiagnostic(runs, "HAW010");
    }

    [Fact]
    public async Task DefensiveNullChecking_IndexesNonNullCallersAcrossPartialSourceFiles()
    {
        var sources = new[]
        {
            "#nullable enable\npartial class Validator { private void Validate(string value) { if (value is null) throw new System.ArgumentNullException(nameof(value)); } }",
            "#nullable enable\npartial class Validator { void Use() { Validate(\"ok\"); } }",
        };
        var runs = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ =>
            AnalyzerTestHost.AnalyzeSourcesAsync(sources, """{ "version": 1, "rules": { "HAW014": { "enabled": true } } }""")));

        AssertStableSingleDiagnostic(runs, "HAW014");
    }

    [Fact]
    public async Task DeadConfiguration_IndexesPropertyReadsAcrossPartialSourceFiles()
    {
        var sources = new[]
        {
            "partial class ServiceOptions { private string Endpoint { get; } }",
            "partial class ServiceOptions { void Configure() { } }",
        };
        var runs = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ =>
            AnalyzerTestHost.AnalyzeSourcesAsync(sources, """{ "version": 1, "rules": { "HAW015": { "enabled": true } } }""")));

        AssertStableSingleDiagnostic(runs, "HAW015");
    }

    [Fact]
    public async Task DeadExtensionPoint_IndexesSourceUsageAcrossPartialSourceFiles()
    {
        var sources = new[]
        {
            "partial class Host { private event System.EventHandler Changed; }",
            "partial class Host { void Run() { } }",
        };
        var runs = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ =>
            AnalyzerTestHost.AnalyzeSourcesAsync(sources, """{ "version": 1, "rules": { "HAW023": { "enabled": true } } }""")));

        AssertStableSingleDiagnostic(runs, "HAW023");
    }

    [Fact]
    public async Task ConfigurationFlow_IndexesForwardingEdgesAcrossSourceFiles()
    {
        var sources = new[]
        {
            "class Options { public int Value; } partial class Pipeline { void Entry(Options options) => Middle(options); }",
            "partial class Pipeline { void Middle(Options options) => Leaf(options); void Leaf(Options options) { } }",
        };
        var runs = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ =>
            AnalyzerTestHost.AnalyzeSourcesAsync(sources, """{ "version": 1, "rules": { "HAW025": { "enabled": true } } }""")));

        AssertStableSingleDiagnostic(runs, "HAW025");
    }

    private static void AssertStableSingleDiagnostic(
        IEnumerable<ImmutableArray<Diagnostic>> runs,
        string diagnosticId)
    {
        var signatures = runs
            .Select(run => run.Where(diagnostic => diagnostic.Id == diagnosticId)
                .Select(diagnostic => $"{diagnostic.Id}:{diagnostic.Location.SourceSpan.Start}:{diagnostic.GetMessage()}")
                .ToArray())
            .ToArray();

        Assert.All(signatures, signature => Assert.Single(signature));
        Assert.All(signatures, signature => Assert.Equal(signatures[0], signature));
    }
}
