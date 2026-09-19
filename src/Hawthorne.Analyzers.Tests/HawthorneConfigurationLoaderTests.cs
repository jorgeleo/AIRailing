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
    public void Load_WhenMultipleConfigurationFilesAreSupplied_ReturnsAnError()
    {
        var result = HawthorneConfigurationLoader.Load(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/one/hawthorne.json", "{ \"version\": 1 }"),
            new TestAdditionalText("/two/hawthorne.json", "{ \"version\": 1 }")));

        Assert.False(result.IsValid);
        Assert.Equal("Only one hawthorne.json may be supplied to a compilation.", result.ErrorMessage);
    }

    [Fact]
    public void IsExcepted_WhenSourceMatchesConfiguredProjectRelativePath_ReturnsTrueAcrossWindowsStylePaths()
    {
        var result = HawthorneConfigurationLoader.Load(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("C:\\project\\hawthorne.json", """
                {
                  "version": 1,
                  "exceptions": [
                    { "file": "Infrastructure/LegacyBridge.cs", "rules": ["HAW105"], "reason": "Required boundary." }
                  ]
                }
                """)));

        var syntaxTree = CSharpSyntaxTree.ParseText("class LegacyBridge { }", path: "C:\\project\\Infrastructure\\LegacyBridge.cs");

        Assert.True(result.IsValid);
        var configuration = Assert.IsType<HawthorneConfiguration>(result.Configuration);
        var evaluator = new HawthorneExceptionEvaluator(configuration);
        Assert.True(evaluator.IsExcepted("HAW105", syntaxTree));
        Assert.False(evaluator.IsExcepted("HAW104", syntaxTree));
    }

    [Fact]
    public void Load_WhenExceptionPathUsesWildcard_ReturnsAnError()
    {
        var result = HawthorneConfigurationLoader.Load(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", """
                { "version": 1, "exceptions": [{ "file": "*.cs", "rules": ["HAW105"], "reason": "Not allowed." }] }
                """)));

        Assert.False(result.IsValid);
        Assert.Equal("Each exception file must be a non-wildcard project-relative path.", result.ErrorMessage);
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

    [Fact]
    public void CreateHawthorneDiagnostic_WhenRuleIsExcepted_ReturnsNull()
    {
        var result = HawthorneConfigurationLoader.Load(ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("/project/hawthorne.json", """
                { "version": 1, "exceptions": [{ "file": "Example.cs", "rules": ["HAW105"], "reason": "Required." }] }
                """)));
        var tree = CSharpSyntaxTree.ParseText("class Example { }", path: "/project/Example.cs");

        var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(
            HawthorneDiagnosticDescriptors.HAW105,
            tree.GetRoot().GetLocation(),
            Assert.IsType<HawthorneConfiguration>(result.Configuration),
            "detail");

        Assert.Null(diagnostic);
    }

    private sealed class TestAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
