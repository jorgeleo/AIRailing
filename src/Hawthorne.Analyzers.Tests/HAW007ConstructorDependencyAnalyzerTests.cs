using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class HAW007ConstructorDependencyAnalyzerTests
{
    [Fact]
    public async Task Analyze_WhenConstructorExceedsServiceDependencyLimit_ReportsHAW007()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class D1 { } class D2 { } class D3 { } class D4 { }
            class D5 { } class D6 { } class D7 { } class D8 { }
            class Host
            {
                public Host(D1 d1, D2 d2, D3 d3, D4 d4, D5 d5, D6 d6, D7 d7, D8 d8) { }
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW007", diagnostic.Id);
        Assert.Contains("8 service-like dependencies", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenConstructorHasAtMostSevenServiceDependencies_DoesNotReportHAW007()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class D1 { } class D2 { } class D3 { } class D4 { }
            class D5 { } class D6 { } class D7 { }
            class Host
            {
                public Host(D1 d1, D2 d2, D3 d3, D4 d4, D5 d5, D6 d6, D7 d7) { }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenConstructorIncludesValuesAndOptions_DoesNotCountThemAsServices()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            namespace Microsoft.Extensions.Options { interface IOptions<T> { } }
            class D1 { } class D2 { } class D3 { } class D4 { }
            class D5 { } class D6 { } class D7 { }
            class AppOptions { }
            class Host
            {
                public Host(
                    D1 d1, D2 d2, D3 d3, D4 d4, D5 d5, D6 d6, D7 d7,
                    string name, int limit, AppOptions options,
                    Microsoft.Extensions.Options.IOptions<AppOptions> wrapped) { }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenConfiguredLimitIsExceeded_ReportsHAW007()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class D1 { } class D2 { } class D3 { }
            class Host { public Host(D1 d1, D2 d2, D3 d3) { } }
            """, """
            { "version": 1, "rules": { "HAW007": { "maximumDependencies": 2 } } }
            """);

        Assert.Equal("HAW007", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenSuppressedWithJustification_DoesNotReportHAW007()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Diagnostics.CodeAnalysis;
            class D1 { } class D2 { } class D3 { } class D4 { }
            class D5 { } class D6 { } class D7 { } class D8 { }
            class Host
            {
                [SuppressMessage("Hawthorne.Architecture", "HAW007", Justification = "Composition root adapter.")]
                public Host(D1 d1, D2 d2, D3 d3, D4 d4, D5 d5, D6 d6, D7 d7, D8 d8) { }
            }
            """, null, MetadataReference.CreateFromFile(typeof(SuppressMessageAttribute).Assembly.Location));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenConfiguredLimitIsNotPositive_ReportsHAW900()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Host { }
            """, """
            { "version": 1, "rules": { "HAW007": { "maximumDependencies": 0 } } }
            """);

        Assert.Equal("HAW900", Assert.Single(diagnostics).Id);
    }
}
