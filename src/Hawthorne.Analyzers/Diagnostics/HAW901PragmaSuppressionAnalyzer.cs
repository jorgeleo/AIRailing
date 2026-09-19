using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW901PragmaSuppressionAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSyntaxNodeAction(c => Analyze((PragmaWarningDirectiveTriviaSyntax)c.Node, c, configuration), SyntaxKind.PragmaWarningDirectiveTrivia);

    private static void Analyze(PragmaWarningDirectiveTriviaSyntax directive, SyntaxNodeAnalysisContext context, HawthorneConfiguration configuration)
    {
        if (!directive.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword)) return;
        var suppressedIds = directive.ErrorCodes.Select(code => code.ToString()).Where(id => id.StartsWith("HAW", StringComparison.Ordinal)).ToArray();
        if (suppressedIds.Length == 0) return;
        context.ReportHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW901, directive.GetLocation(), configuration, string.Join(", ", suppressedIds));
    }
}
