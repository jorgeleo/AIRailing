using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Hawthorne.Analyzers.Analysis.Architecture;

internal static class ForwardingMethodClassifier
{
    internal static bool TryGetForwardedDependency(
        MethodDeclarationSyntax method,
        SemanticModel semanticModel,
        out ISymbol? dependency)
    {
        dependency = null;
        if (semanticModel.GetDeclaredSymbol(method) is not IMethodSymbol methodSymbol ||
            IsRequiredContractMethod(methodSymbol))
        {
            return false;
        }

        var expression = GetSingleExpression(method);
        var invocation = expression as InvocationExpressionSyntax ??
            (expression as AwaitExpressionSyntax)?.Expression as InvocationExpressionSyntax;
        if (invocation?.Expression is not MemberAccessExpressionSyntax memberAccess ||
            !TryGetDependencyReceiver(memberAccess.Expression, semanticModel, out dependency))
        {
            return false;
        }

        return invocation.ArgumentList.Arguments.Count == method.ParameterList.Parameters.Count &&
            invocation.ArgumentList.Arguments.Zip(method.ParameterList.Parameters, (argument, parameter) =>
                argument.Expression is IdentifierNameSyntax identifier &&
                identifier.Identifier.ValueText == parameter.Identifier.ValueText).All(value => value);
    }

    private static ExpressionSyntax? GetSingleExpression(MethodDeclarationSyntax method) =>
        method.ExpressionBody?.Expression ??
        (method.Body?.Statements.Count == 1 && method.Body.Statements[0] is ReturnStatementSyntax returned
            ? returned.Expression
            : null);

    private static bool TryGetDependencyReceiver(
        ExpressionSyntax receiver,
        SemanticModel semanticModel,
        out ISymbol? dependency)
    {
        dependency = semanticModel.GetSymbolInfo(receiver).Symbol;
        return dependency is IFieldSymbol or IPropertySymbol or IParameterSymbol;
    }

    internal static bool IsRequiredContractMethod(IMethodSymbol method)
    {
        if (method.IsOverride || method.ExplicitInterfaceImplementations.Length > 0)
        {
            return true;
        }

        return method.ContainingType.AllInterfaces
            .SelectMany(@interface => @interface.GetMembers().OfType<IMethodSymbol>())
            .Any(interfaceMethod => SymbolEqualityComparer.Default.Equals(
                method.ContainingType.FindImplementationForInterfaceMember(interfaceMethod), method));
    }
}
