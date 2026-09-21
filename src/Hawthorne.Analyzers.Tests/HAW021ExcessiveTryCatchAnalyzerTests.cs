using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class HAW021ExcessiveTryCatchAnalyzerTests
{
    [Fact]
    public async Task Analyze_WhenCatchAllReturnsNullWithoutRecovery_ReportsHAW021()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Reader
            {
                string? Read()
                {
                    try { return "value"; }
                    catch (System.Exception) { return null; }
                }
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW021", diagnostic.Id);
        Assert.Contains("returns a default value", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenCatchAllReturnsAValueTypeDefault_ReportsHAW021()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Reader
            {
                int Read()
                {
                    try { return 1; }
                    catch (System.Exception) { return 0; }
                }
            }
            """);

        Assert.Equal("HAW021", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenCatchAllReturnsExplicitDefault_ReportsHAW021()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Reader
            {
                bool Read()
                {
                    try { return true; }
                    catch (System.Exception) { return default; }
                }
            }
            """);

        Assert.Equal("HAW021", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenSpecificExceptionReturnsADefault_DoesNotReportHAW021()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Reader
            {
                string? Read()
                {
                    try { return "value"; }
                    catch (System.InvalidOperationException) { return null; }
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenCatchAllPerformsRecoveryBeforeReturning_DoesNotReportHAW021()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Reader
            {
                string? Read()
                {
                    try { return "value"; }
                    catch (System.Exception)
                    {
                        Notify();
                        return null;
                    }
                }
                void Notify() { }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTypeExceedsTryDensity_ReportsHAW021AtTheType()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class ExcessiveHandler
            {
                void One() { try { } catch (System.Exception) { } }
                void Two() { try { } catch (System.Exception) { } }
                void Three() { try { } catch (System.Exception) { } }
                void Four() { try { } catch (System.Exception) { } }
                void Five() { }
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW021", diagnostic.Id);
        Assert.Contains("0.80", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenTypeIsAtTheTryDensityBoundary_DoesNotReportHAW021()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class BalancedHandler
            {
                public void One() { try { } catch (System.Exception) { } }
                public void Two() { try { } catch (System.Exception) { } }
                public void Three() { try { } catch (System.Exception) { } }
                public void Four() { try { } catch (System.Exception) { } }
                public void Five() { try { } catch (System.Exception) { } }
                public void Six() { try { } catch (System.Exception) { } }
                public void Seven() { }
                public void Eight() { }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenConfiguredDensityIsExceeded_ReportsHAW021()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Handler
            {
                void One() { try { } catch (System.Exception) { } }
                void Two() { }
            }
            """, """
            { "version": 1, "rules": { "HAW021": { "minimumMethodCount": 2, "maximumTryBlocksPerMethod": 0.49 } } }
            """);

        Assert.Equal("HAW021", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenCatchAllDefaultReturnIsDisabled_DoesNotReportThatSignal()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Reader
            {
                string? Read()
                {
                    try { return "value"; }
                    catch (System.Exception) { return null; }
                }
            }
            """, """
            { "version": 1, "rules": { "HAW021": { "reportCatchAllDefaultReturn": false } } }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTypeDensityIsSuppressedWithJustification_DoesNotReportHAW021()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Diagnostics.CodeAnalysis;
            [SuppressMessage("Hawthorne.Reliability", "HAW021", Justification = "Legacy protocol requires independent fallback boundaries.")]
            class ExcessiveHandler
            {
                void One() { try { } catch (System.Exception) { } }
                void Two() { try { } catch (System.Exception) { } }
                void Three() { try { } catch (System.Exception) { } }
                void Four() { try { } catch (System.Exception) { } }
                void Five() { }
            }
            """, null, MetadataReference.CreateFromFile(typeof(SuppressMessageAttribute).Assembly.Location));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenMaximumTryBlocksPerMethodIsOutsideTheUnitInterval_ReportsHAW900()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Handler { }
            """, """
            { "version": 1, "rules": { "HAW021": { "maximumTryBlocksPerMethod": 1.1 } } }
            """);

        Assert.Equal("HAW900", Assert.Single(diagnostics).Id);
    }
}
