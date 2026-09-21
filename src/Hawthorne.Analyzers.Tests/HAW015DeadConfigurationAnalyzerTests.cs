using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class HAW015DeadConfigurationAnalyzerTests
{
    [Fact]
    public async Task Analyze_WhenPrivateConfigurationPropertyIsNeverRead_ReportsHAW015()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class ApiOptions
            {
                private string ApiKey { get; set; } = "default";
            }
            """, """
            { "version": 1, "rules": { "HAW015": { "enabled": true } } }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW015", diagnostic.Id);
        Assert.Contains("ApiKey", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenConfigurationPropertyIsReadByBehavior_DoesNotReportHAW015()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class ApiOptions
            {
                private string ApiKey { get; set; } = "default";
                public int GetKeyLength() => ApiKey.Length;
            }
            """, """
            { "version": 1, "rules": { "HAW015": { "enabled": true } } }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenConfigurationPropertyIsPublic_DoesNotReportHAW015()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class ApiOptions
            {
                public string ApiKey { get; set; } = "default";
            }
            """, """
            { "version": 1, "rules": { "HAW015": { "enabled": true } } }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenInternalConfigurationPropertyIsExcludedByDefault_DoesNotReportHAW015()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class ApiOptions
            {
                internal string ApiKey { get; set; } = "default";
            }
            """, """
            { "version": 1, "rules": { "HAW015": { "enabled": true } } }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenInternalConfigurationPropertiesAreIncluded_ReportsHAW015()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class ApiOptions
            {
                internal string ApiKey { get; set; } = "default";
            }
            """, """
            { "version": 1, "rules": { "HAW015": { "enabled": true, "includeInternalProperties": true } } }
            """);

        Assert.Equal("HAW015", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenTypeIsNotConfigurationShaped_DoesNotReportHAW015()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Customer
            {
                private string ApiKey { get; set; } = "default";
            }
            """, """
            { "version": 1, "rules": { "HAW015": { "enabled": true } } }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenPropertyHasAnAttribute_DoesNotReportHAW015()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class ApiOptions
            {
                [System.Obsolete]
                private string ApiKey { get; set; } = "default";
            }
            """, """
            { "version": 1, "rules": { "HAW015": { "enabled": true } } }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenConfiguredSuffixMatchesType_ReportsHAW015()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class FeatureTuning
            {
                private string ApiKey { get; set; } = "default";
            }
            """, """
            { "version": 1, "rules": { "HAW015": { "enabled": true, "configurationTypeSuffixes": ["Tuning"] } } }
            """);

        Assert.Equal("HAW015", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenIncludeInternalPropertiesIsNotBoolean_ReportsHAW900()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class ApiOptions { }
            """, """
            { "version": 1, "rules": { "HAW015": { "includeInternalProperties": "yes" } } }
            """);

        Assert.Equal("HAW900", Assert.Single(diagnostics).Id);
    }
}
