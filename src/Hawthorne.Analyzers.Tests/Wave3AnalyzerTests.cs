using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class Wave3AnalyzerTests
{
    [Fact]
    public async Task RepositoryLayer_RecognizesEfSymbolsAndIsEnabledByDefault()
    {
        const string source = """
            namespace Microsoft.EntityFrameworkCore { public class DbSet<T> { public T Find(int id) => default; public void Add(T value) { } public void Remove(T value) { } } }
            class Customer { }
            class CustomerRepository
            {
                private Microsoft.EntityFrameworkCore.DbSet<Customer> set;
                public Customer Get(int id) => set.Find(id);
                public void Add(Customer value) => set.Add(value);
                public void Remove(Customer value) => set.Remove(value);
            }
            """;
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(source, """
            { "version": 1, "rules": { "HAW003": { "enabled": false }, "HAW020": { "enabled": true } } }
            """);
        Assert.Contains(diagnostics, d => d.Id == "HAW020");
    }

    [Fact]
    public async Task RepositoryLayer_CanBeExplicitlyDisabled()
    {
        const string source = """
            namespace Microsoft.EntityFrameworkCore { public class DbSet<T> { public T Find(int id) => default; public void Add(T value) { } public void Remove(T value) { } } }
            class Customer { }
            class CustomerRepository
            {
                private Microsoft.EntityFrameworkCore.DbSet<Customer> set;
                public Customer Get(int id) => set.Find(id);
                public void Add(Customer value) => set.Add(value);
                public void Remove(Customer value) => set.Remove(value);
            }
            """;
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(source, """
            { "version": 1, "rules": { "HAW003": { "enabled": false }, "HAW020": { "enabled": false } } }
            """);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "HAW020");
    }

    [Fact]
    public async Task RepositoryLayer_DoesNotReportBelowTheForwardingMethodBoundary()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            namespace Microsoft.EntityFrameworkCore { public class DbSet<T> { public T Find(int id) => default; public void Add(T value) { } } }
            class Customer { }
            class CustomerRepository
            {
                private Microsoft.EntityFrameworkCore.DbSet<Customer> set;
                public Customer Get(int id) => set.Find(id);
                public void Add(Customer value) => set.Add(value);
            }
            """, """
            { "version": 1, "rules": { "HAW003": { "enabled": false }, "HAW020": { "enabled": true } } }
            """);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "HAW020");
    }

    [Fact]
    public async Task DeadExtensionPoint_ReportsUnsubscribedPrivateEvent()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System;
            class Host { private event EventHandler Changed; public void Run() { } }
            """, """
            { "version": 1, "rules": { "HAW023": { "enabled": true } } }
            """);
        Assert.Contains(diagnostics, d => d.Id == "HAW023" && d.GetMessage().Contains("Changed"));
    }

    [Fact]
    public async Task DeadExtensionPoint_DoesNotReportSourceSubscription()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            delegate void ChangedHandler();
            class Host
            {
                private event ChangedHandler Changed;
                private void OnChanged() { }
                public void Use() { Changed += OnChanged; }
            }
            """, """
            { "version": 1, "rules": { "HAW023": { "enabled": true } } }
            """);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "HAW023");
    }

    [Fact]
    public async Task ConfigurationFlow_ReportsTwoUnchangedHopsWithRelatedLocation()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Options { public int Value; }
            class Pipeline
            {
                void Entry(Options options) => Middle(options);
                void Middle(Options options) => Leaf(options);
                void Leaf(Options options) { }
            }
            """, """
            { "version": 1, "rules": { "HAW025": { "enabled": true } } }
            """);
        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "HAW025"));
        Assert.True(diagnostic.AdditionalLocations.Count == 1);
        Assert.Contains("2 source boundaries", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ConfigurationFlow_DoesNotReportWhenTheTerminalLayerReadsOptions()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Options { public int Value; }
            class Pipeline
            {
                void Entry(Options options) => Middle(options);
                void Middle(Options options) => Leaf(options);
                void Leaf(Options options) { var value = options.Value; }
            }
            """, """
            { "version": 1, "rules": { "HAW025": { "enabled": true } } }
            """);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "HAW025");
    }

    [Fact]
    public async Task ConfigurationFlow_DoesNotReportBelowTheHopBoundary()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Options { }
            class Pipeline
            {
                void Entry(Options options) => Leaf(options);
                void Leaf(Options options) { }
            }
            """, """
            { "version": 1, "rules": { "HAW025": { "enabled": true, "minimumForwardingHops": 2 } } }
            """);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "HAW025");
    }

    [Fact]
    public async Task LoggingNoise_UsesLoggerSymbolsAndLifecycleTemplates()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            namespace Microsoft.Extensions.Logging { public interface ILogger { void LogInformation(string message); } }
            class Logger : Microsoft.Extensions.Logging.ILogger { public void LogInformation(string message) { } }
            class Busy
            {
                private readonly Microsoft.Extensions.Logging.ILogger logger = new Logger();
                public void A() => logger.LogInformation("enter A");
                public void B() => logger.LogInformation("exit B");
                public void C() => logger.LogInformation("success C");
                public void D() => logger.LogInformation("start D");
                public void E() => logger.LogInformation("finish E");
            }
            """, """
            { "version": 1, "rules": { "HAW029": { "enabled": true } } }
            """);
        Assert.Contains(diagnostics, d => d.Id == "HAW029");
    }

    [Fact]
    public async Task LoggingNoise_ExcludesErrorLogs()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            namespace Microsoft.Extensions.Logging { public interface ILogger { void LogError(string message); } }
            class Logger : Microsoft.Extensions.Logging.ILogger { public void LogError(string message) { } }
            class Busy
            {
                private readonly Microsoft.Extensions.Logging.ILogger logger = new Logger();
                public void A() => logger.LogError("enter A");
                public void B() => logger.LogError("exit B");
                public void C() => logger.LogError("success C");
                public void D() => logger.LogError("start D");
                public void E() => logger.LogError("finish E");
            }
            """, """
            { "version": 1, "rules": { "HAW029": { "enabled": true } } }
            """);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "HAW029");
    }

    [Fact]
    public async Task LoggingNoise_IgnoresSynthesizedRecordMembers()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("public record Activity(string Name);", """
            { "version": 1, "rules": { "HAW029": { "enabled": true } } }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "AD0001");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "HAW029");
    }

    [Fact]
    public async Task CopyPaste_ReportsOneStableClusterWithRelatedMethods()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Mappers
            {
                int A(int value) { var result = value + 1; result++; return result; }
                int B(int value) { var result = value + 1; result++; return result; }
                int C(int value) { var result = value + 1; result++; return result; }
            }
            """, """
            { "version": 1, "rules": { "HAW030": { "enabled": true } } }
            """);
        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "HAW030"));
        Assert.True(diagnostic.AdditionalLocations.Count == 2);
    }

    [Fact]
    public async Task CopyPaste_DoesNotReportAClusterBelowTheMethodBoundary()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Mappers
            {
                int A(int value) { var result = value + 1; result++; return result; }
                int B(int value) { var result = value + 1; result++; return result; }
            }
            """, """
            { "version": 1, "rules": { "HAW030": { "enabled": true } } }
            """);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "HAW030");
    }

    [Fact]
    public async Task AbstractionDensity_ReportsInvariantMetricPropertiesWhenThresholdIsExceeded()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            interface IThing { }
            abstract class BaseThing { }
            class Service { public int Run() => 1; }
            """, """
            { "version": 1, "rules": { "HAW100": { "enabled": true, "maximumDensity": 1, "minimumBehavioralTypes": 1 } } }
            """);
        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "HAW100"));
        Assert.Equal("3", diagnostic.Properties["abstractionCount"]);
        Assert.Equal("1", diagnostic.Properties["behavioralCount"]);
        Assert.Equal("3.00", diagnostic.Properties["density"]);
    }

    [Fact]
    public async Task CopyPaste_IndexesMethodsAcrossMultipleSourceFilesWithoutDuplicateReports()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeSourcesAsync(new[]
        {
            "class First { int A(int value) { var result = value + 1; result++; return result; } }",
            "class Second { int B(int value) { var result = value + 1; result++; return result; } }",
            "class Third { int C(int value) { var result = value + 1; result++; return result; } }",
        }, """{ "version": 1, "rules": { "HAW030": { "enabled": true } } }""");

        Assert.Single(diagnostics.Where(d => d.Id == "HAW030"));
    }

    [Fact]
    public async Task CopyPaste_ProducesDeterministicDiagnosticsAcrossConcurrentRuns()
    {
        var sources = new[]
        {
            "class First { int A(int value) { var result = value + 1; result++; return result; } }",
            "class Second { int B(int value) { var result = value + 1; result++; return result; } }",
            "class Third { int C(int value) { var result = value + 1; result++; return result; } }",
        };
        var runs = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => AnalyzerTestHost.AnalyzeSourcesAsync(sources, """{ "version": 1, "rules": { "HAW030": { "enabled": true } } }""")));
        var signatures = runs.Select(run => string.Join("|", run.Where(d => d.Id == "HAW030").Select(d => $"{d.Id}:{d.Location.SourceSpan.Start}:{d.AdditionalLocations.Count}:{d.GetMessage()}"))).ToArray();

        Assert.All(signatures, signature => Assert.Equal(signatures[0], signature));
    }

    [Fact]
    public async Task AbstractionDensity_OnlyInventoriesSourceTypesNotReferencedAssemblies()
    {
        var reference = MetadataReference.CreateFromFile(typeof(Hawthorne.Analyzers.HawthorneAnalyzerBootstrap).Assembly.Location);

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            "class SourceService { public int Run() => 1; }",
            """{ "version": 1, "rules": { "HAW100": { "enabled": true, "minimumBehavioralTypes": 1 } } }""",
            reference);
        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "HAW100"));

        Assert.Equal("1", diagnostic.Properties["behavioralCount"]);
        Assert.Equal("1", diagnostic.Properties["abstractionCount"]);
    }

    [Fact]
    public async Task HealthMetric_UsesConfiguredSeverityOverride()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("class Service { public int Run() => 1; }", """
            { "version": 1, "rules": { "HAW100": { "severity": "warning", "minimumBehavioralTypes": 1 } } }
            """);

        Assert.Equal(DiagnosticSeverity.Warning, Assert.Single(diagnostics.Where(d => d.Id == "HAW100")).Severity);
    }

    [Fact]
    public async Task HealthMetric_DoesNotReportWhenThereAreNoBehavioralTypes()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("interface IThing { }", """
            { "version": 1, "rules": { "HAW100": { "enabled": true, "minimumBehavioralTypes": 1 } } }
            """);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "HAW100");
    }

    [Fact]
    public async Task CompilationWideRules_HonorCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AnalyzerTestHost.AnalyzeSourcesAsync(
            new[] { "class Service { public int Run() => 1; }" },
            cancellationToken: cancellation.Token));
    }
}
