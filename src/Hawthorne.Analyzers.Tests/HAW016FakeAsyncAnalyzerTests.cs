using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class HAW016FakeAsyncAnalyzerTests
{
    private static readonly MetadataReference TaskReference =
        MetadataReference.CreateFromFile(typeof(Task).Assembly.Location);

    [Fact]
    public async Task Analyze_WhenAsyncTaskMethodHasNoAwait_ReportsHAW016()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Threading.Tasks;
            class Host
            {
                async Task<int> GetAsync() { return 42; }
            }
            """, null, TaskReference);

        Assert.Equal("HAW016", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenMethodReturnsTaskFromResult_ReportsHAW016()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Threading.Tasks;
            class Host
            {
                Task<int> GetAsync() => Task.FromResult(42);
            }
            """, null, TaskReference);

        Assert.Equal("HAW016", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenMethodReturnsCompletedTask_ReportsHAW016()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Threading.Tasks;
            class Host
            {
                Task SaveAsync() => Task.CompletedTask;
            }
            """, null, TaskReference);

        Assert.Equal("HAW016", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenMethodConstructsValueTaskFromValue_ReportsHAW016()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Threading.Tasks;
            class Host
            {
                ValueTask<int> GetAsync() => new ValueTask<int>(42);
            }
            """, null, TaskReference);

        Assert.Equal("HAW016", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenMethodReturnsGenuineAsyncOperation_DoesNotReportHAW016()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Threading.Tasks;
            class Host
            {
                Task DelayAsync() => Task.Delay(1);
            }
            """, null, TaskReference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTaskRunIsTrivialButHeuristicIsDisabled_DoesNotReportHAW016()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Threading.Tasks;
            class Host
            {
                Task<int> GetAsync() => Task.Run(() => 42);
            }
            """, null, TaskReference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTrivialTaskRunHeuristicIsEnabled_ReportsHAW016()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Threading.Tasks;
            class Host
            {
                Task<int> GetAsync() => Task.Run(() => 42);
            }
            """, """
            { "version": 1, "rules": { "HAW016": { "reportTrivialTaskRun": true } } }
            """, TaskReference);

        Assert.Equal("HAW016", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenMethodImplementsAnInterface_DoesNotReportHAW016ByDefault()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Threading.Tasks;
            interface IHost { Task<int> GetAsync(); }
            class Host : IHost
            {
                public async Task<int> GetAsync() { return 42; }
            }
            """, """
            { "version": 1, "rules": { "HAW001": { "enabled": false } } }
            """, TaskReference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenSuppressedWithJustification_DoesNotReportHAW016()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Diagnostics.CodeAnalysis;
            using System.Threading.Tasks;
            class Host
            {
                [SuppressMessage("Hawthorne.Reliability", "HAW016", Justification = "Framework callback contract.")]
                async Task<int> GetAsync() { return 42; }
            }
            """, null,
            TaskReference,
            MetadataReference.CreateFromFile(typeof(SuppressMessageAttribute).Assembly.Location));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenIgnoreContractMethodsIsNotBoolean_ReportsHAW900()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Host { }
            """, """
            { "version": 1, "rules": { "HAW016": { "ignoreContractMethods": "yes" } } }
            """);

        Assert.Equal("HAW900", Assert.Single(diagnostics).Id);
    }
}
