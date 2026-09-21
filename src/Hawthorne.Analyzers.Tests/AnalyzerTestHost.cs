using System.Collections.Immutable;
using Hawthorne.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Hawthorne.Analyzers.Tests;

internal static class AnalyzerTestHost
{
    internal static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        string? configuration = null,
        params MetadataReference[] additionalReferences)
    {
        var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) }
            .Concat(additionalReferences);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source, path: "/project/Test.cs") },
            references);
        var options = CreateOptions(configuration);

        return await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HawthorneAnalyzerBootstrap()), options)
            .GetAnalyzerDiagnosticsAsync();
    }

    private static CompilationWithAnalyzersOptions CreateOptions(string? configuration) => new(
        configuration is null
            ? new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty)
            : new AnalyzerOptions(ImmutableArray.Create<AdditionalText>(
                new TestAdditionalText("/project/hawthorne.json", configuration))),
        onAnalyzerException: null,
        concurrentAnalysis: true,
        logAnalyzerExecutionTime: false,
        reportSuppressedDiagnostics: false);

    private sealed class TestAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
