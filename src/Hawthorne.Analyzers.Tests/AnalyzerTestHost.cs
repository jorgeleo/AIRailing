using System.Collections.Immutable;
using Hawthorne.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hawthorne.Analyzers.Tests;

internal static class AnalyzerTestHost
{
    internal static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        string? configuration = null,
        params MetadataReference[] additionalReferences)
        => await AnalyzeSourcesAsync(new[] { source }, configuration, CancellationToken.None, additionalReferences);

    internal static async Task<ImmutableArray<Diagnostic>> AnalyzeSourcesAsync(
        IReadOnlyList<string> sources,
        string? configuration = null,
        CancellationToken cancellationToken = default,
        params MetadataReference[] additionalReferences)
    {
        var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) }
            .Concat(additionalReferences);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            sources.Select((source, index) => CSharpSyntaxTree.ParseText(source, path: $"/project/Test{index}.cs")),
            references);
        var options = CreateOptions(configuration);

        return await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap()), options)
            .GetAnalyzerDiagnosticsAsync(cancellationToken);
    }

    private static CompilationWithAnalyzersOptions CreateOptions(string? configuration)
    {
        var normalizedConfiguration = NormalizeFocusedConfiguration(configuration);
        var analyzerOptions = normalizedConfiguration is null
            ? new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty)
            : new AnalyzerOptions(ImmutableArray.Create<AdditionalText>(
                new TestAdditionalText("/project/hawthorne.json", normalizedConfiguration)));

        return new CompilationWithAnalyzersOptions(
            analyzerOptions,
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false);
    }

    private static string? NormalizeFocusedConfiguration(string? configuration)
    {
        var text = configuration ?? "{ \"version\": 1, \"rules\": {} }";
        JsonObject root;
        try
        {
            root = JsonNode.Parse(text)?.AsObject() ?? new JsonObject();
        }
        catch (JsonException)
        {
            return configuration;
        }

        var rules = root["rules"] as JsonObject ?? new JsonObject();
        root["rules"] = rules;
        foreach (var id in new[] { "HAW015", "HAW020", "HAW023", "HAW025", "HAW029", "HAW030", "HAW100" })
        {
            if (!rules.ContainsKey(id))
            {
                rules[id] = new JsonObject { ["enabled"] = false };
            }
        }

        return root.ToJsonString();
    }

    private sealed class TestAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
