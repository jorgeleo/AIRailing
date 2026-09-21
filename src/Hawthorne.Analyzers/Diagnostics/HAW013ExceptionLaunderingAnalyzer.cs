using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW013ExceptionLaunderingAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSyntaxNodeAction(
            c => Analyze((CatchClauseSyntax)c.Node, c, configuration),
            SyntaxKind.CatchClause);

    private static void Analyze(
        CatchClauseSyntax catchClause,
        SyntaxNodeAnalysisContext context,
        HawthorneConfiguration configuration)
    {
        if (catchClause.Declaration is null ||
            context.SemanticModel.GetDeclaredSymbol(catchClause.Declaration) is not ILocalSymbol caughtException)
        {
            return;
        }

        if (TryGetGenericWrapper(catchClause, caughtException, context.SemanticModel, configuration.ExceptionLaundering, out var wrapper))
        {
            Report(context, configuration, caughtException, wrapper.GetLocation(),
                context.SemanticModel.GetTypeInfo(wrapper).Type?.Name ?? "Exception");
            return;
        }

        if (configuration.ExceptionLaundering.ReportLogAndRethrow &&
            TryGetLogAndRethrow(catchClause, caughtException, context.SemanticModel, out var logInvocation))
        {
            Report(context, configuration, caughtException, logInvocation.GetLocation(), "logging and rethrow");
        }
    }

    private static bool TryGetGenericWrapper(
        CatchClauseSyntax catchClause,
        ILocalSymbol caughtException,
        SemanticModel semanticModel,
        Hawthorne013Configuration configuration,
        out ObjectCreationExpressionSyntax wrapper)
    {
        wrapper = null!;
        if (catchClause.Block.Statements.Count != 1 ||
            catchClause.Block.Statements[0] is not ThrowStatementSyntax
            {
                Expression: ObjectCreationExpressionSyntax creation,
            })
        {
            return false;
        }

        var exceptionType = semanticModel.GetTypeInfo(creation).Type;
        if (exceptionType is null || !configuration.IsGenericException(exceptionType) ||
            creation.ArgumentList is null)
        {
            return false;
        }

        var originalExceptionArguments = creation.ArgumentList.Arguments
            .Where(argument => SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(argument.Expression).Symbol,
                caughtException))
            .ToArray();
        if (originalExceptionArguments.Length != 1)
        {
            return false;
        }

        if (creation.ArgumentList.Arguments.Any(argument =>
            !SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(argument.Expression).Symbol,
                caughtException) &&
            argument.Expression is not LiteralExpressionSyntax))
        {
            return false;
        }

        wrapper = creation;
        return true;
    }

    private static bool TryGetLogAndRethrow(
        CatchClauseSyntax catchClause,
        ILocalSymbol caughtException,
        SemanticModel semanticModel,
        out InvocationExpressionSyntax logInvocation)
    {
        logInvocation = null!;
        if (catchClause.Block.Statements.Count != 2 ||
            catchClause.Block.Statements[0] is not ExpressionStatementSyntax
            {
                Expression: InvocationExpressionSyntax invocation,
            } ||
            catchClause.Block.Statements[1] is not ThrowStatementSyntax { Expression: null } ||
            semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol { Name: var methodName } ||
            !methodName.StartsWith("Log", StringComparison.Ordinal) ||
            !invocation.ArgumentList.Arguments.Any(argument => SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(argument.Expression).Symbol,
                caughtException)))
        {
            return false;
        }

        logInvocation = invocation;
        return true;
    }

    private static void Report(
        SyntaxNodeAnalysisContext context,
        HawthorneConfiguration configuration,
        ILocalSymbol caughtException,
        Location location,
        string replacement)
    {
        context.ReportHawthorneDiagnostic(
            HawthorneDiagnosticDescriptors.HAW013,
            location,
            configuration,
            caughtException.Type.Name,
            replacement);
    }
}
