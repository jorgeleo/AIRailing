using System.Collections.Immutable;
using Hawthorne.Analyzers.Configuration;
using Hawthorne.Analyzers.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class HawthorneConfigurationLoaderTests
{
    [Fact]
    public void Load_WhenConfigurationIsAbsent_UsesWarningDefaults()
    {
        var result = HawthorneConfigurationLoader.Load(ImmutableArray<AdditionalText>.Empty);

        Assert.True(result.IsValid);
        Assert.Equal(DiagnosticSeverity.Warning, result.Configuration!.GetRule("HAW105").Severity);
        Assert.True(result.Configuration.GetRule("HAW105").IsEnabled);
    }

    [Fact]
    public void Load_WhenSeverityIsConfigured_UsesConfiguredEffectiveSeverity()
    {
        var result = HawthorneConfigurationLoader.Load(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", """
                { "version": 1, "rules": { "HAW105": { "severity": "error", "enabled": false } } }
                """)));

        Assert.True(result.IsValid);
        Assert.Equal(DiagnosticSeverity.Error, result.Configuration!.GetRule("HAW105").Severity);
        Assert.False(result.Configuration.GetRule("HAW105").IsEnabled);
    }

    [Fact]
    public void Load_WhenMethodLengthThresholdsAreConfigured_UsesConfiguredValues()
    {
        var result = HawthorneConfigurationLoader.Load(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", """
                { "version": 1, "rules": { "HAW105": { "maximumExecutableStatements": 2, "maximumPhysicalLines": 3 } } }
                """)));

        var configuration = Assert.IsType<HawthorneConfiguration>(result.Configuration);
        Assert.Equal(2, configuration.MethodLength.MaximumExecutableStatements);
        Assert.Equal(3, configuration.MethodLength.MaximumPhysicalLines);
    }

    [Fact]
    public void Load_WhenCyclomaticThresholdIsConfigured_UsesConfiguredValue()
    {
        var result = HawthorneConfigurationLoader.Load(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", "{ \"version\": 1, \"rules\": { \"HAW101\": { \"maximum\": 2 } } }")));

        Assert.Equal(2, Assert.IsType<HawthorneConfiguration>(result.Configuration).MaximumCyclomaticComplexity);
    }

    [Fact]
    public void Load_WhenClassCouplingThresholdIsConfigured_UsesConfiguredValue()
    {
        var result = HawthorneConfigurationLoader.Load(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", "{ \"version\": 1, \"rules\": { \"HAW104\": { \"maximum\": 2 } } }")));

        Assert.Equal(2, Assert.IsType<HawthorneConfiguration>(result.Configuration).MaximumClassCoupling);
    }

    [Fact]
    public void Load_WhenMultipleConfigurationFilesAreSupplied_ReturnsAnError()
    {
        var result = HawthorneConfigurationLoader.Load(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/one/hawthorne.json", "{ \"version\": 1 }"),
            new TestAdditionalText("/two/hawthorne.json", "{ \"version\": 1 }")));

        Assert.False(result.IsValid);
        Assert.Equal("Only one hawthorne.json may be supplied to a compilation.", result.ErrorMessage);
    }

    [Fact]
    public void Load_WhenLegacyExceptionsPropertyIsPresent_ReturnsMigrationError()
    {
        var result = HawthorneConfigurationLoader.Load(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", """
                { "version": 1, "exceptions": [{ "file": "Example.cs", "rules": ["HAW105"], "reason": "Legacy." }] }
                """)));

        Assert.False(result.IsValid);
        Assert.Contains("SuppressMessageAttribute", result.ErrorMessage);
    }

    [Fact]
    public void CreateHawthorneDiagnostic_WhenSeverityIsConfigured_UsesEffectiveSeverity()
    {
        var result = HawthorneConfigurationLoader.Load(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", """
                { "version": 1, "rules": { "HAW105": { "severity": "error" } } }
                """)));
        var tree = CSharpSyntaxTree.ParseText("class Example { }", path: "/project/Example.cs");

        var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(
            HawthorneDiagnosticDescriptors.HAW105,
            tree.GetRoot().GetLocation(),
            Assert.IsType<HawthorneConfiguration>(result.Configuration),
            "detail");

        Assert.NotNull(diagnostic);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    private sealed class TestAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
