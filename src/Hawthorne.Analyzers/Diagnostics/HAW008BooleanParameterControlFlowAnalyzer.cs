using Hawthorne.Analyzers.Analysis.Architecture;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW008BooleanParameterControlFlowAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSyntaxNodeAction(
            c => Analyze((MethodDeclarationSyntax)c.Node, c, configuration),
            SyntaxKind.MethodDeclaration);

    private static void Analyze(
        MethodDeclarationSyntax method,
        SyntaxNodeAnalysisContext context,
        HawthorneConfiguration configuration)
    {
        if (context.SemanticModel.GetDeclaredSymbol(method) is not IMethodSymbol methodSymbol ||
            ForwardingMethodClassifier.IsRequiredContractMethod(methodSymbol))
        {
            return;
        }

        var controllingParameters = methodSymbol.Parameters
            .Where(IsBoolean)
            .Where(parameter => ControlsFlow(
                method,
                parameter,
                context.SemanticModel,
                configuration.BooleanControlFlow.RequireDirectControlFlowUse))
            .ToArray();
        if (controllingParameters.Length < configuration.BooleanControlFlow.WarningParameterCount)
        {
            return;
        }

        var defaultSeverity = controllingParameters.Length >= configuration.BooleanControlFlow.ErrorParameterCount
            ? DiagnosticSeverity.Error
            : DiagnosticSeverity.Warning;
        var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnosticWithDefaultSeverity(
            HawthorneDiagnosticDescriptors.HAW008,
            method.Identifier.GetLocation(),
            configuration,
            defaultSeverity,
            method.Identifier.ValueText,
            controllingParameters.Length);
        if (diagnostic is not null)
        {
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsBoolean(IParameterSymbol parameter)
    {
        if (parameter.Type.SpecialType == SpecialType.System_Boolean)
        {
            return true;
        }

        return parameter.Type is INamedTypeSymbol named &&
            named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            named.TypeArguments.Length == 1 &&
            named.TypeArguments[0].SpecialType == SpecialType.System_Boolean;
    }

    private static bool ControlsFlow(
        MethodDeclarationSyntax method,
        IParameterSymbol parameter,
        SemanticModel semanticModel,
        bool requireDirectUse) =>
        GetConditions(method).Any(condition => condition
            .DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier =>
                SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(identifier).Symbol, parameter) &&
                (!requireDirectUse || IsDirectControlFlowUse(identifier, condition))));

    private static IEnumerable<ExpressionSyntax> GetConditions(MethodDeclarationSyntax method)
    {
        foreach (var conditional in method.DescendantNodes().OfType<IfStatementSyntax>())
        {
            yield return conditional.Condition;
        }

        foreach (var conditional in method.DescendantNodes().OfType<WhileStatementSyntax>())
        {
            yield return conditional.Condition;
        }

        foreach (var conditional in method.DescendantNodes().OfType<DoStatementSyntax>())
        {
            yield return conditional.Condition;
        }

        foreach (var conditional in method.DescendantNodes().OfType<ForStatementSyntax>())
        {
            if (conditional.Condition is not null)
            {
                yield return conditional.Condition;
            }
        }

        foreach (var conditional in method.DescendantNodes().OfType<ConditionalExpressionSyntax>())
        {
            yield return conditional.Condition;
        }

        foreach (var conditional in method.DescendantNodes().OfType<WhenClauseSyntax>())
        {
            yield return conditional.Condition;
        }
    }

    private static bool IsDirectControlFlowUse(IdentifierNameSyntax identifier, ExpressionSyntax condition)
    {
        for (SyntaxNode? current = identifier; current is not null && current != condition; current = current.Parent)
        {
            if (current.Parent is null || !IsDirectControlFlowContainer(current.Parent))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDirectControlFlowContainer(SyntaxNode node) => node.Kind() is
        SyntaxKind.ParenthesizedExpression or
        SyntaxKind.LogicalNotExpression or
        SyntaxKind.LogicalAndExpression or
        SyntaxKind.LogicalOrExpression or
        SyntaxKind.BitwiseAndExpression or
        SyntaxKind.BitwiseOrExpression or
        SyntaxKind.EqualsExpression or
        SyntaxKind.NotEqualsExpression or
        SyntaxKind.CoalesceExpression or
        SyntaxKind.ConditionalExpression;
}
