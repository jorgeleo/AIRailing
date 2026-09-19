using Hawthorne.Analyzers.Analysis.Complexity;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW102CognitiveComplexityAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSyntaxNodeAction(c => Analyze((MethodDeclarationSyntax)c.Node, c, configuration), SyntaxKind.MethodDeclaration);

    private static void Analyze(MethodDeclarationSyntax method, SyntaxNodeAnalysisContext context, HawthorneConfiguration configuration)
    {
        var complexity = CognitiveComplexityCalculator.Calculate(method);
        if (complexity > configuration.MaximumCognitiveComplexity)
        {
            context.ReportHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW102, method.Identifier.GetLocation(), configuration,
                method.Identifier.ValueText, complexity, configuration.MaximumCognitiveComplexity);
        }
    }
}
