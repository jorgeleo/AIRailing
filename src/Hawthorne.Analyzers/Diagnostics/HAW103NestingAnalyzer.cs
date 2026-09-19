using Hawthorne.Analyzers.Analysis.Complexity;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW103NestingAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSyntaxNodeAction(c => Analyze((MethodDeclarationSyntax)c.Node, c, configuration), SyntaxKind.MethodDeclaration);

    private static void Analyze(MethodDeclarationSyntax method, SyntaxNodeAnalysisContext context, HawthorneConfiguration configuration)
    {
        var depth = NestingDepthCalculator.Calculate(method);
        if (depth > configuration.MaximumNestingDepth)
        {
            context.ReportHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW103, method.Identifier.GetLocation(), configuration,
                method.Identifier.ValueText, depth, configuration.MaximumNestingDepth);
        }
    }
}
