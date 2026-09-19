using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW003PassThroughAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSyntaxNodeAction(c => Analyze((ClassDeclarationSyntax)c.Node, c, configuration), SyntaxKind.ClassDeclaration);

    private static void Analyze(ClassDeclarationSyntax type, SyntaxNodeAnalysisContext context, HawthorneConfiguration configuration)
    {
        var methods = type.Members.OfType<MethodDeclarationSyntax>().ToArray();
        var forwarding = methods.Where(method => IsForwarding(method, context.SemanticModel)).ToArray();
        if (forwarding.Length >= 3 && forwarding.Length / (double)methods.Length >= 0.80)
        {
            context.ReportHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW003, type.Identifier.GetLocation(), configuration,
                $"Type '{type.Identifier.ValueText}' forwards {forwarding.Length / (double)methods.Length:P0} of its eligible methods without adding behavior. Remove or collapse the forwarding layer.");
            return;
        }

        foreach (var method in forwarding)
        {
            context.ReportHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW003, method.Identifier.GetLocation(), configuration,
                $"Method '{method.Identifier.ValueText}' only forwards its arguments without adding behavior. Remove the method or add the responsibility that justifies it.");
        }
    }

    private static bool IsForwarding(MethodDeclarationSyntax method, SemanticModel semanticModel)
    {
        var methodSymbol = semanticModel.GetDeclaredSymbol(method);
        if (methodSymbol is null || IsRequiredContractMethod(methodSymbol)) return false;

        var expression = method.ExpressionBody?.Expression ??
            (method.Body?.Statements.Count == 1 && method.Body.Statements[0] is ReturnStatementSyntax returned ? returned.Expression : null);
        var invocation = expression as InvocationExpressionSyntax ?? (expression as AwaitExpressionSyntax)?.Expression as InvocationExpressionSyntax;
        if (invocation?.Expression is not MemberAccessExpressionSyntax memberAccess ||
            !IsDependencyReceiver(memberAccess.Expression, semanticModel))
        {
            return false;
        }

        return invocation.ArgumentList.Arguments.Count == method.ParameterList.Parameters.Count &&
            invocation.ArgumentList.Arguments.Zip(method.ParameterList.Parameters, (argument, parameter) =>
                argument.Expression is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == parameter.Identifier.ValueText).All(value => value);
    }

    private static bool IsDependencyReceiver(ExpressionSyntax receiver, SemanticModel semanticModel) =>
        semanticModel.GetSymbolInfo(receiver).Symbol is IFieldSymbol or IPropertySymbol or IParameterSymbol;

    private static bool IsRequiredContractMethod(IMethodSymbol method)
    {
        if (method.IsOverride || method.ExplicitInterfaceImplementations.Length > 0) return true;

        return method.ContainingType.AllInterfaces
            .SelectMany(@interface => @interface.GetMembers().OfType<IMethodSymbol>())
            .Any(interfaceMethod => SymbolEqualityComparer.Default.Equals(
                method.ContainingType.FindImplementationForInterfaceMember(interfaceMethod), method));
    }
}
