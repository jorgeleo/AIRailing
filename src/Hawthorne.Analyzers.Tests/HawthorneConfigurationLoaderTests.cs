using System.Collections.Immutable;
using Hawthorne.Analyzers.Configuration;
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

    private sealed class TestAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
