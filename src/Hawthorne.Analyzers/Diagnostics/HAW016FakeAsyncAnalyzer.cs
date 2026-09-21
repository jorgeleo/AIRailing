using Hawthorne.Analyzers.Analysis.Architecture;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW016FakeAsyncAnalyzer
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
            !IsAsynchronousReturnType(methodSymbol.ReturnType) ||
            configuration.FakeAsync.IgnoreContractMethods &&
            ForwardingMethodClassifier.IsRequiredContractMethod(methodSymbol))
        {
            return;
        }

        if ((!HasAwait(method) && method.Modifiers.Any(SyntaxKind.AsyncKeyword)) ||
            IsImmediateCompletedReturn(method, context.SemanticModel, configuration.FakeAsync))
        {
            context.ReportHawthorneDiagnostic(
                HawthorneDiagnosticDescriptors.HAW016,
                method.Identifier.GetLocation(),
                configuration,
                method.Identifier.ValueText);
        }
    }

    private static bool IsAsynchronousReturnType(ITypeSymbol type) =>
        type is INamedTypeSymbol named &&
        named.OriginalDefinition.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks" &&
        named.OriginalDefinition.Name is "Task" or "ValueTask" &&
        named.OriginalDefinition.Arity is 0 or 1;

    private static bool HasAwait(MethodDeclarationSyntax method) =>
        method.DescendantNodes().OfType<AwaitExpressionSyntax>().Any();

    private static bool IsImmediateCompletedReturn(
        MethodDeclarationSyntax method,
        SemanticModel semanticModel,
        Hawthorne016Configuration configuration)
    {
        var expression = GetSingleReturnedExpression(method);
        if (expression is null || semanticModel.GetOperation(expression) is not IOperation operation)
        {
            return false;
        }

        return operation is IInvocationOperation invocation &&
            (IsTaskFromResult(invocation) || configuration.ReportTrivialTaskRun && IsTrivialTaskRun(invocation)) ||
            operation is IPropertyReferenceOperation property && IsCompletedTask(property) ||
            operation is IObjectCreationOperation creation && IsValueTask(creation.Type);
    }

    private static ExpressionSyntax? GetSingleReturnedExpression(MethodDeclarationSyntax method) =>
        method.ExpressionBody?.Expression ??
        (method.Body?.Statements.Count == 1 && method.Body.Statements[0] is ReturnStatementSyntax returned
            ? returned.Expression
            : null);

    private static bool IsTaskFromResult(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name == "FromResult" && IsTask(invocation.TargetMethod.ContainingType);

    private static bool IsCompletedTask(IPropertyReferenceOperation property) =>
        property.Property.Name == "CompletedTask" && IsTask(property.Property.ContainingType);

    private static bool IsTrivialTaskRun(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name == "Run" &&
        IsTask(invocation.TargetMethod.ContainingType) &&
        invocation.Syntax is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 1 } syntax &&
        syntax.ArgumentList.Arguments[0].Expression is LambdaExpressionSyntax
        {
            Body: LiteralExpressionSyntax,
        };

    private static bool IsTask(ITypeSymbol? type) =>
        type is INamedTypeSymbol named &&
        named.OriginalDefinition.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks" &&
        named.OriginalDefinition.Name == "Task";

    private static bool IsValueTask(ITypeSymbol? type) =>
        type is INamedTypeSymbol named &&
        named.OriginalDefinition.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks" &&
        named.OriginalDefinition.Name == "ValueTask";
}
