using Hawthorne.Analyzers.Analysis.Metrics;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW105MethodLengthAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration)
    {
        context.RegisterSyntaxNodeAction(
            syntaxContext => Analyze((MethodDeclarationSyntax)syntaxContext.Node, syntaxContext, configuration),
            SyntaxKind.MethodDeclaration);
    }

    private static void Analyze(MethodDeclarationSyntax method, SyntaxNodeAnalysisContext context, HawthorneConfiguration configuration)
    {
        var statements = MethodLengthCalculator.CountExecutableStatements(method);
        if (statements > configuration.MethodLength.MaximumExecutableStatements)
        {
            context.ReportHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW105, method.Identifier.GetLocation(), configuration,
                $"Method '{method.Identifier.ValueText}' contains {statements} executable statements; maximum allowed is {configuration.MethodLength.MaximumExecutableStatements}");
            return;
        }

        var lines = MethodLengthCalculator.CountPhysicalLines(method);
        if (lines > configuration.MethodLength.MaximumPhysicalLines)
        {
            context.ReportHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW105, method.Identifier.GetLocation(), configuration,
                $"Method '{method.Identifier.ValueText}' spans {lines} physical lines; maximum allowed is {configuration.MethodLength.MaximumPhysicalLines}");
        }
    }
}
