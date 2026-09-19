using System.Collections.Immutable;
using Hawthorne.Analyzers.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HawthorneAnalyzerBootstrap : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => HawthorneDiagnosticDescriptors.All;

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
    }
}
