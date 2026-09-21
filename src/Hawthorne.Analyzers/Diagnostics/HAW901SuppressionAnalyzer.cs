using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW901SuppressionAnalyzer
{
    private static readonly HashSet<string> HawthorneRuleIds =
        new(HawthorneDiagnosticDescriptors.All.Select(descriptor => descriptor.Id), StringComparer.Ordinal);

    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration)
    {
        context.RegisterSyntaxNodeAction(
            c => AnalyzePragma((PragmaWarningDirectiveTriviaSyntax)c.Node, c, configuration),
            SyntaxKind.PragmaWarningDirectiveTrivia);
        context.RegisterSyntaxNodeAction(
            c => AnalyzeSuppressMessage((AttributeSyntax)c.Node, c, configuration),
            SyntaxKind.Attribute);
    }

    private static void AnalyzePragma(PragmaWarningDirectiveTriviaSyntax directive, SyntaxNodeAnalysisContext context, HawthorneConfiguration configuration)
    {
        if (!directive.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword)) return;

        var suppressedIds = directive.ErrorCodes
            .Select(code => code.ToString())
            .Where(id => HawthorneRuleIds.Contains(id))
            .ToArray();

        if (suppressedIds.Length > 0)
        {
            context.ReportHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW901, directive.GetLocation(), configuration, string.Join(", ", suppressedIds));
            return;
        }

        if (directive.ErrorCodes.Count == 0)
        {
            // A codeless disable suppresses every warning, including all Hawthorne rules.
            context.ReportHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW901, directive.GetLocation(), configuration, "all diagnostics");
        }
    }

    private static void AnalyzeSuppressMessage(AttributeSyntax attribute, SyntaxNodeAnalysisContext context, HawthorneConfiguration configuration)
    {
        var suppressMessageType = context.SemanticModel.Compilation.GetTypeByMetadataName(
            "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute");
        if (suppressMessageType is null ||
            !SymbolEqualityComparer.Default.Equals(context.SemanticModel.GetTypeInfo(attribute).Type, suppressMessageType))
        {
            return;
        }

        var arguments = attribute.ArgumentList?.Arguments;
        if (arguments is null || arguments.Value.Count < 2)
        {
            return;
        }

        var checkId = GetConstantString(context.SemanticModel, arguments.Value[1].Expression);
        if (checkId is null || !HawthorneRuleIds.Contains(checkId))
        {
            return;
        }

        var justification = arguments.Value
            .FirstOrDefault(argument => argument.NameEquals?.Name.Identifier.ValueText == "Justification");
        if (justification is null || string.IsNullOrWhiteSpace(GetConstantString(context.SemanticModel, justification.Expression)))
        {
            context.ReportHawthorneDiagnostic(
                HawthorneDiagnosticDescriptors.HAW901,
                attribute.GetLocation(),
                configuration,
                checkId);
        }
    }

    private static string? GetConstantString(SemanticModel semanticModel, ExpressionSyntax expression) =>
        semanticModel.GetConstantValue(expression) is { HasValue: true, Value: string value }
            ? value
            : null;
}
