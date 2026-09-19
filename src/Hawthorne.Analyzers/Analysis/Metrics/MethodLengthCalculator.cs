using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Hawthorne.Analyzers.Analysis.Metrics;

internal static class MethodLengthCalculator
{
    internal static int CountExecutableStatements(MethodDeclarationSyntax method) =>
        method.Body?.DescendantNodes().OfType<StatementSyntax>().Count(statement => statement is not BlockSyntax) ??
        (method.ExpressionBody is null ? 0 : 1);

    internal static int CountPhysicalLines(MethodDeclarationSyntax method)
    {
        var lineSpan = method.GetLocation().GetLineSpan();
        return lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
    }
}
