using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class HAW008BooleanParameterControlFlowAnalyzerTests
{
    [Fact]
    public async Task Analyze_WhenTwoBooleanParametersDirectlyControlFlow_ReportsWarningHAW008()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Host
            {
                void Process(bool validate, bool notify)
                {
                    if (validate) { }
                    if (notify) { }
                }
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW008", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public async Task Analyze_WhenThreeBooleanParametersDirectlyControlFlow_ReportsErrorHAW008()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Host
            {
                void Process(bool validate, bool notify, bool persist)
                {
                    if (validate) { }
                    if (notify) { }
                    if (persist) { }
                }
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW008", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public async Task Analyze_WhenBooleanParameterIsOnlyData_DoesNotReportHAW008()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Host
            {
                bool Echo(bool value, bool other) => value;
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenBooleanParametersArePassedToHelpers_DoesNotReportHAW008ByDefault()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Host
            {
                bool IsEnabled(bool value) => value;
                void Process(bool validate, bool notify)
                {
                    if (IsEnabled(validate)) { }
                    if (IsEnabled(notify)) { }
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenBooleanControlFlowImplementsAnInterface_DoesNotReportHAW008()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            interface IHost { void Process(bool validate, bool notify); }
            class Host : IHost
            {
                public void Process(bool validate, bool notify)
                {
                    if (validate) { }
                    if (notify) { }
                }
            }
            """, """
            { "version": 1, "rules": { "HAW001": { "enabled": false } } }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenWarningThresholdIsConfigured_ReportsHAW008()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Host
            {
                void Process(bool validate)
                {
                    if (validate) { }
                }
            }
            """, """
            { "version": 1, "rules": { "HAW008": { "warningParameterCount": 1 } } }
            """);

        Assert.Equal("HAW008", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenSeverityIsExplicitlyConfigured_UsesItInsteadOfEscalation()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Host
            {
                void Process(bool validate, bool notify, bool persist)
                {
                    if (validate) { }
                    if (notify) { }
                    if (persist) { }
                }
            }
            """, """
            { "version": 1, "rules": { "HAW008": { "severity": "warning" } } }
            """);

        Assert.Equal(DiagnosticSeverity.Warning, Assert.Single(diagnostics).Severity);
    }

    [Fact]
    public async Task Analyze_WhenSuppressedWithJustification_DoesNotReportHAW008()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Diagnostics.CodeAnalysis;
            class Host
            {
                [SuppressMessage("Hawthorne.Architecture", "HAW008", Justification = "Legacy protocol flags.")]
                void Process(bool validate, bool notify)
                {
                    if (validate) { }
                    if (notify) { }
                }
            }
            """, null, MetadataReference.CreateFromFile(typeof(SuppressMessageAttribute).Assembly.Location));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenErrorThresholdIsLowerThanWarningThreshold_ReportsHAW900()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Host { }
            """, """
            { "version": 1, "rules": { "HAW008": { "warningParameterCount": 3, "errorParameterCount": 2 } } }
            """);

        Assert.Equal("HAW900", Assert.Single(diagnostics).Id);
    }
}
