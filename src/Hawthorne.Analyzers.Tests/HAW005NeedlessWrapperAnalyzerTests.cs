using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class HAW005NeedlessWrapperAnalyzerTests
{
    [Fact]
    public async Task Analyze_WhenNamedWrapperPredominantlyForwardsOneDependency_ReportsHAW005WithoutHAW003Duplicate()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class CustomerClient
            {
                public int Get(int id) => id;
                public int Save(int id) => id;
                public int Delete(int id) => id;
            }
            class CustomerClientWrapper
            {
                private readonly CustomerClient client = new CustomerClient();
                public int Get(int id) => client.Get(id);
                public int Save(int id) => client.Save(id);
                public int Delete(int id) => client.Delete(id);
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW005", diagnostic.Id);
        Assert.Contains("CustomerClientWrapper", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenWrapperAddsBehavior_DoesNotReportHAW005()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class CustomerClient
            {
                public int Get(int id) => id;
                public int Save(int id) => id;
                public int Delete(int id) => id;
            }
            class CustomerClientWrapper
            {
                private readonly CustomerClient client = new CustomerClient();
                public int Get(int id) => client.Get(id + 1);
                public int Save(int id) => client.Save(id + 1);
                public int Delete(int id) => client.Delete(id + 1);
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenConfiguredMinimumIsMet_ReportsHAW005()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class CustomerClient
            {
                public int Get(int id) => id;
                public int Save(int id) => id;
            }
            class CustomerClientWrapper
            {
                private readonly CustomerClient client = new CustomerClient();
                public int Get(int id) => client.Get(id);
                public int Save(int id) => client.Save(id);
            }
            """, """
            {
              "version": 1,
              "rules": {
                "HAW005": {
                  "minimumForwardingMethods": 2,
                  "minimumForwardingRatio": 0.80
                }
              }
            }
            """);

        Assert.Equal("HAW005", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenRoleSuffixRequirementIsDisabled_ReportsHAW005()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class CustomerClient
            {
                public int Get(int id) => id;
                public int Save(int id) => id;
            }
            class CustomerGateway
            {
                private readonly CustomerClient client = new CustomerClient();
                public int Get(int id) => client.Get(id);
                public int Save(int id) => client.Save(id);
            }
            """, """
            {
              "version": 1,
              "rules": {
                "HAW005": {
                  "minimumForwardingMethods": 2,
                  "requireRoleSuffix": false
                }
              }
            }
            """);

        Assert.Equal("HAW005", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenDisabled_AllowsHAW003ToReportTheGenericForwardingLayer()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class CustomerClient
            {
                public int Get(int id) => id;
                public int Save(int id) => id;
                public int Delete(int id) => id;
            }
            class CustomerClientWrapper
            {
                private readonly CustomerClient client = new CustomerClient();
                public int Get(int id) => client.Get(id);
                public int Save(int id) => client.Save(id);
                public int Delete(int id) => client.Delete(id);
            }
            """, """
            { "version": 1, "rules": { "HAW005": { "enabled": false } } }
            """);

        Assert.Equal("HAW003", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenWrapperRuleIsSuppressedWithJustification_DoesNotReport()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Diagnostics.CodeAnalysis;
            class CustomerClient
            {
                public int Get(int id) => id;
                public int Save(int id) => id;
                public int Delete(int id) => id;
            }
            [SuppressMessage("Hawthorne.Architecture", "HAW005", Justification = "Protocol compatibility boundary.")]
            class CustomerClientWrapper
            {
                private readonly CustomerClient client = new CustomerClient();
                public int Get(int id) => client.Get(id);
                public int Save(int id) => client.Save(id);
                public int Delete(int id) => client.Delete(id);
            }
            """, null, MetadataReference.CreateFromFile(typeof(SuppressMessageAttribute).Assembly.Location));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenWrapperRatioIsInvalid_ReportsHAW900WithoutNormalAnalysis()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class CustomerClientWrapper { }
            """, """
            { "version": 1, "rules": { "HAW005": { "minimumForwardingRatio": 1.1 } } }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW900", diagnostic.Id);
        Assert.Contains("minimumForwardingRatio", diagnostic.GetMessage());
    }
}
