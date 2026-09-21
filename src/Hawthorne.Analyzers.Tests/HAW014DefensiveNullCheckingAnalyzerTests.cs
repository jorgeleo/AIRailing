using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class HAW014DefensiveNullCheckingAnalyzerTests
{
    [Fact]
    public async Task Analyze_WhenPrivateNonNullableParameterIsAlwaysCalledWithANonNullValue_ReportsHAW014()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            #nullable enable
            class Processor
            {
                void Run() => Validate("value");
                private void Validate(string value)
                {
                    if (value is null) throw new System.ArgumentNullException(nameof(value));
                }
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW014", diagnostic.Id);
        Assert.Contains("Validate", diagnostic.GetMessage());
        Assert.Contains("value", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenPrivateGuardUsesThrowIfNull_ReportsHAW014()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            #nullable enable
            class Processor
            {
                void Run() => Validate("value");
                private void Validate(string value)
                {
                    System.ArgumentNullException.ThrowIfNull(value);
                }
            }
            """);

        Assert.Equal("HAW014", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenAnySourceCallMayProvideNull_DoesNotReportHAW014()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            #nullable enable
            class Processor
            {
                void Run(string? value) => Validate(value);
                private void Validate(string value)
                {
                    if (value == null) throw new System.ArgumentNullException(nameof(value));
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenParameterIsNullable_DoesNotReportHAW014()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            #nullable enable
            class Processor
            {
                void Run() => Validate(null);
                private void Validate(string? value)
                {
                    if (value is null) throw new System.ArgumentNullException(nameof(value));
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenGuardedMethodIsPublic_DoesNotReportHAW014()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            #nullable enable
            class Processor
            {
                void Run() => Validate("value");
                public void Validate(string value)
                {
                    if (ReferenceEquals(value, null)) throw new System.ArgumentNullException(nameof(value));
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenMethodIsUsedAsADelegate_DoesNotReportHAW014()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            #nullable enable
            class Processor
            {
                void Run()
                {
                    System.Action<string> validate = Validate;
                    validate("value");
                }
                private void Validate(string value)
                {
                    if (value is null) throw new System.ArgumentNullException(nameof(value));
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenInternalMethodsAreIncludedByConfiguration_ReportsHAW014()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            #nullable enable
            class Processor
            {
                void Run() => Validate("value");
                internal void Validate(string value)
                {
                    if (value is null) throw new System.ArgumentNullException(nameof(value));
                }
            }
            """, """
            { "version": 1, "rules": { "HAW014": { "includeInternalMethods": true } } }
            """);

        Assert.Equal("HAW014", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenTheGuardIsSuppressedWithJustification_DoesNotReportHAW014()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            #nullable enable
            using System.Diagnostics.CodeAnalysis;
            class Processor
            {
                void Run() => Validate("value");
                [SuppressMessage("Hawthorne.Architecture", "HAW014", Justification = "Private reflection entry point remains supported.")]
                private void Validate(string value)
                {
                    if (value is null) throw new System.ArgumentNullException(nameof(value));
                }
            }
            """, null, MetadataReference.CreateFromFile(typeof(SuppressMessageAttribute).Assembly.Location));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenIncludeInternalMethodsIsNotBoolean_ReportsHAW900()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Processor { }
            """, """
            { "version": 1, "rules": { "HAW014": { "includeInternalMethods": "yes" } } }
            """);

        Assert.Equal("HAW900", Assert.Single(diagnostics).Id);
    }
}
