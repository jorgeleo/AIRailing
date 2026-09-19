using Hawthorne.Analyzers.Analysis.Complexity;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW101CyclomaticComplexityAnalyzer
{
    private const int Maximum = 10;
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSyntaxNodeAction(c => Analyze((MethodDeclarationSyntax)c.Node, c, configuration), SyntaxKind.MethodDeclaration);

    private static void Analyze(MethodDeclarationSyntax method, SyntaxNodeAnalysisContext context, HawthorneConfiguration configuration)
    {
        var complexity = CyclomaticComplexityCalculator.Calculate(method);
        if (complexity > Maximum)
        {
            context.ReportHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW101, method.Identifier.GetLocation(), configuration,
                method.Identifier.ValueText, complexity, Maximum);
        }
    }
}
