using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW106LineBreakAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSyntaxTreeAction(c => Analyze(c, configuration));

    private static void Analyze(SyntaxTreeAnalysisContext context, HawthorneConfiguration configuration)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);
        var text = context.Tree.GetText(context.CancellationToken);
        foreach (var token in root.DescendantTokens(descendIntoTrivia: false))
        {
            if (token.Kind() is not (SyntaxKind.OpenBraceToken or SyntaxKind.SemicolonToken) ||
                HasCarriageReturnLineFeedImmediatelyAfter(text, token.Span.End))
            {
                continue;
            }

            context.ReportHawthorneDiagnostic(
                HawthorneDiagnosticDescriptors.HAW106,
                token.GetLocation(),
                configuration,
                token.Text);
        }
    }

    private static bool HasCarriageReturnLineFeedImmediatelyAfter(SourceText text, int offset) =>
        offset + 1 < text.Length && text[offset] == '\r' && text[offset + 1] == '\n';
}
