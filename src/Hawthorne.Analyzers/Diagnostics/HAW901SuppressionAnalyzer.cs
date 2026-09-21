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

    private static void AnalyzePragma(
        PragmaWarningDirectiveTriviaSyntax directive,
        SyntaxNodeAnalysisContext context,
        HawthorneConfiguration configuration)
    {
        if (!directive.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword)) return;

        var suppressedIds = directive.ErrorCodes
            .Select(code => code.ToString())
            .Where(id => HawthorneRuleIds.Contains(id))
            .ToArray();

        if (suppressedIds.Length > 0)
        {
            ReportPragmaSuppressions(suppressedIds, directive.GetLocation(), context, configuration);
            return;
        }

        if (directive.ErrorCodes.Count == 0)
        {
            // A codeless disable suppresses every warning, including all Hawthorne rules.
            ReportNonSuppressibleSuppression("all diagnostics", directive.GetLocation(), context);
        }
    }

    private static void AnalyzeSuppressMessage(
        AttributeSyntax attribute,
        SyntaxNodeAnalysisContext context,
        HawthorneConfiguration configuration)
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

        if (HawthorneDiagnosticDescriptors.NonSuppressibleRuleIds.Contains(checkId))
        {
            ReportNonSuppressibleSuppression(checkId, attribute.GetLocation(), context);
            return;
        }

        var justification = arguments.Value
            .FirstOrDefault(argument => argument.NameEquals?.Name.Identifier.ValueText == "Justification");
        if (justification is null || string.IsNullOrWhiteSpace(GetConstantString(context.SemanticModel, justification.Expression)))
        {
            ReportJustificationRequired(checkId, attribute.GetLocation(), context, configuration);
        }
    }

    private static void ReportPragmaSuppressions(
        IEnumerable<string> suppressedIds,
        Location location,
        SyntaxNodeAnalysisContext context,
        HawthorneConfiguration configuration)
    {
        var nonSuppressibleIds = suppressedIds
            .Where(HawthorneDiagnosticDescriptors.NonSuppressibleRuleIds.Contains)
            .ToArray();
        if (nonSuppressibleIds.Length > 0)
        {
            ReportNonSuppressibleSuppression(string.Join(", ", nonSuppressibleIds), location, context);
        }

        var suppressibleIds = suppressedIds
            .Where(id => !HawthorneDiagnosticDescriptors.NonSuppressibleRuleIds.Contains(id))
            .ToArray();
        if (suppressibleIds.Length > 0)
        {
            context.ReportHawthorneDiagnostic(
                HawthorneDiagnosticDescriptors.HAW901,
                location,
                configuration,
                string.Join(", ", suppressibleIds),
                "use SuppressMessageAttribute with a specific non-empty Justification instead");
        }
    }

    private static void ReportJustificationRequired(
        string ruleId,
        Location location,
        SyntaxNodeAnalysisContext context,
        HawthorneConfiguration configuration) =>
        context.ReportHawthorneDiagnostic(
            HawthorneDiagnosticDescriptors.HAW901,
            location,
            configuration,
            ruleId,
            "it must include a non-empty Justification");

    private static void ReportNonSuppressibleSuppression(
        string ruleIds,
        Location location,
        SyntaxNodeAnalysisContext context) =>
        ReportInvalidSuppression(
            ruleIds,
            location,
            context,
            GetRequiredRemediation(ruleIds));

    private static string GetRequiredRemediation(string ruleIds)
    {
        if (ruleIds.Contains("HAW900", StringComparison.Ordinal))
        {
            return "the configuration must be corrected rather than suppressed";
        }

        if (ruleIds.Contains("HAW901", StringComparison.Ordinal))
        {
            return "this validation must remain enabled rather than suppressed";
        }

        return "this rule must be simplified rather than suppressed";
    }

    private static void ReportInvalidSuppression(
        string ruleIds,
        Location location,
        SyntaxNodeAnalysisContext context,
        string reason)
    {
        // HAW901's own suppression attempt cannot be reported at the suppressed syntax.
        // A location-free compiler error remains visible while preserving the attempted
        // suppression as the diagnostic message argument.
        var reportLocation = ruleIds.Contains("HAW901", StringComparison.Ordinal)
            ? Location.None
            : location;
        context.ReportDiagnostic(Diagnostic.Create(
            HawthorneDiagnosticDescriptors.HAW901,
            reportLocation,
            DiagnosticSeverity.Error,
            additionalLocations: null,
            properties: null,
            messageArgs: new object[]
            {
                ruleIds,
                reason,
            }));
    }

    private static string? GetConstantString(SemanticModel semanticModel, ExpressionSyntax expression) =>
        semanticModel.GetConstantValue(expression) is { HasValue: true, Value: string value }
            ? value
            : null;
}
