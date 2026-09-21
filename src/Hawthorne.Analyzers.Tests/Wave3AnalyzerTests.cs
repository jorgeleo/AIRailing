using Microsoft.CodeAnalysis;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class Wave3AnalyzerTests
{
    [Fact]
    public async Task RepositoryLayer_RecognizesEfSymbolsAndRequiresOptIn()
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
        Assert.DoesNotContain((await AnalyzerTestHost.AnalyzeAsync(source)).Select(d => d.Id), id => id == "HAW020");
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(source, """
            { "version": 1, "rules": { "HAW003": { "enabled": false }, "HAW020": { "enabled": true } } }
            """);
        Assert.Contains(diagnostics, d => d.Id == "HAW020");
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
    public async Task AbstractionDensity_ReportsInvariantMetricPropertiesWhenThresholdIsExceeded()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            interface IThing { }
            abstract class BaseThing { }
            class Service { public int Run() => 1; }
            """, """
            { "version": 1, "rules": { "HAW100": { "enabled": true, "maximumDensity": 1 } } }
            """);
        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "HAW100"));
        Assert.Equal("3", diagnostic.Properties["abstractionCount"]);
        Assert.Equal("1", diagnostic.Properties["behavioralCount"]);
        Assert.Equal("3.00", diagnostic.Properties["density"]);
    }
}
