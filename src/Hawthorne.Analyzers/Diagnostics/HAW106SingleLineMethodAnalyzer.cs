using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW106SingleLineMethodAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSyntaxNodeAction(
            syntaxContext => Analyze((MethodDeclarationSyntax)syntaxContext.Node, syntaxContext, configuration),
            SyntaxKind.MethodDeclaration);

    private static void Analyze(
        MethodDeclarationSyntax method,
        SyntaxNodeAnalysisContext context,
        HawthorneConfiguration configuration)
    {
        var body = method.Body;
        if (body is null || IsMultiline(body) || CountExecutableStatements(body) < 2)
        {
            return;
        }

        context.ReportHawthorneDiagnostic(
            HawthorneDiagnosticDescriptors.HAW106,
            method.Identifier.GetLocation(),
            configuration,
            method.Identifier.ValueText);
    }

    private static bool IsMultiline(BlockSyntax body)
    {
        var lineSpan = body.GetLocation().GetLineSpan();
        return lineSpan.StartLinePosition.Line != lineSpan.EndLinePosition.Line;
    }

    private static int CountExecutableStatements(BlockSyntax body) => body
        .DescendantNodesAndSelf(descendIntoChildren: node =>
            node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax)
        .OfType<StatementSyntax>()
        .Count(statement => statement is not BlockSyntax and not LocalFunctionStatementSyntax);
}
