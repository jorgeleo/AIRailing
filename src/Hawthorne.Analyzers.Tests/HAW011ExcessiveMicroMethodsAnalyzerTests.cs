using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class HAW011ExcessiveMicroMethodsAnalyzerTests
{
    [Fact]
    public async Task Analyze_WhenEightPrivateMethodsAreTiny_ReportsHAW011()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class FragmentedWorkflow
            {
                private void One() { }
                private void Two() { }
                private void Three() { }
                private void Four() { }
                private void Five() { }
                private void Six() { }
                private void Seven() { }
                private void Eight() { }
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW011", diagnostic.Id);
        Assert.Contains("100", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenTinyMethodRatioIsAtTheBoundary_DoesNotReportHAW011()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class BalancedWorkflow
            {
                private void One() { }
                private void Two() { }
                private void Three() { }
                private void Four() { }
                private void Five() { }
                private void Six() { }
                private void Seven() { var one = 1; var two = 2; var three = 3; }
                private void Eight() { var one = 1; var two = 2; var three = 3; }
                private void Nine() { var one = 1; var two = 2; var three = 3; }
                private void Ten() { var one = 1; var two = 2; var three = 3; }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTinyMethodRatioExceedsTheBoundary_ReportsHAW011()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class FragmentedWorkflow
            {
                private void One() { }
                private void Two() { }
                private void Three() { }
                private void Four() { }
                private void Five() { }
                private void Six() { var one = 1; var two = 2; var three = 3; }
                private void Seven() { var one = 1; var two = 2; var three = 3; }
                private void Eight() { var one = 1; var two = 2; var three = 3; }
            }
            """);

        Assert.Equal("HAW011", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenFewerThanTheMinimumPrivateMethodsExist_DoesNotReportHAW011()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class SmallWorkflow
            {
                private void One() { }
                private void Two() { }
                private void Three() { }
                private void Four() { }
                private void Five() { }
                private void Six() { }
                private void Seven() { }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenPublicMethodsAndAccessorsAreTiny_DoesNotCountThem()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Workflow
            {
                public int Value { get; private set; }
                public void Execute() { }
                private void One() { }
                private void Two() { }
                private void Three() { }
                private void Four() { }
                private void Five() { }
                private void Six() { }
                private void Seven() { }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenConfiguredRatioIsNotExceeded_DoesNotReportHAW011()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class FragmentedWorkflow
            {
                private void One() { }
                private void Two() { }
                private void Three() { }
                private void Four() { }
                private void Five() { }
                private void Six() { }
                private void Seven() { }
                private void Eight() { }
            }
            """, """
            { "version": 1, "rules": { "HAW011": { "maxTinyMethodRatio": 1 } } }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenConfiguredStatementLimitIncludesThreeStatementMethods_ReportsHAW011()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class FragmentedWorkflow
            {
                private void One() { var one = 1; var two = 2; var three = 3; }
                private void Two() { var one = 1; var two = 2; var three = 3; }
                private void Three() { var one = 1; var two = 2; var three = 3; }
                private void Four() { var one = 1; var two = 2; var three = 3; }
                private void Five() { var one = 1; var two = 2; var three = 3; }
                private void Six() { var one = 1; var two = 2; var three = 3; }
                private void Seven() { var one = 1; var two = 2; var three = 3; }
                private void Eight() { var one = 1; var two = 2; var three = 3; }
            }
            """, """
            { "version": 1, "rules": { "HAW011": { "tinyMethodStatementLimit": 3 } } }
            """);

        Assert.Equal("HAW011", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenSuppressedWithJustification_DoesNotReportHAW011()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Diagnostics.CodeAnalysis;
            [SuppressMessage("Hawthorne.Architecture", "HAW011", Justification = "Generated protocol adapter helpers remain independently addressable.")]
            class FragmentedWorkflow
            {
                private void One() { }
                private void Two() { }
                private void Three() { }
                private void Four() { }
                private void Five() { }
                private void Six() { }
                private void Seven() { }
                private void Eight() { }
            }
            """, null, MetadataReference.CreateFromFile(typeof(SuppressMessageAttribute).Assembly.Location));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenMinimumMethodCountIsNotPositive_ReportsHAW900()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Workflow { }
            """, """
            { "version": 1, "rules": { "HAW011": { "minimumMethodCount": 0 } } }
            """);

        Assert.Equal("HAW900", Assert.Single(diagnostics).Id);
    }
}
