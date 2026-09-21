using Hawthorne.Analyzers.Analysis.Architecture;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW024CancellationTokenAnalyzer
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
            methodSymbol.IsAbstract ||
            configuration.CancellationTokens.IgnoreContractMethods &&
            ForwardingMethodClassifier.IsRequiredContractMethod(methodSymbol))
        {
            return;
        }

        foreach (var token in methodSymbol.Parameters.Where(parameter => IsCancellationToken(parameter.Type)))
        {
            var missingCalls = configuration.CancellationTokens.ReportMissingForwarding
                ? GetMissingForwardingCalls(method, context.SemanticModel, configuration.CancellationTokens)
                : Array.Empty<IInvocationOperation>();
            if (missingCalls.Length > 0)
            {
                foreach (var invocation in missingCalls)
                {
                    context.ReportHawthorneDiagnostic(
                        HawthorneDiagnosticDescriptors.HAW024,
                        invocation.Syntax.GetLocation(),
                        configuration,
                        token.Name,
                        invocation.TargetMethod.Name);
                }

                continue;
            }

            if (!IsRead(method, token, context.SemanticModel))
            {
                context.ReportHawthorneDiagnostic(
                    HawthorneDiagnosticDescriptors.HAW024,
                    token.Locations.FirstOrDefault(location => location.IsInSource) ?? method.Identifier.GetLocation(),
                    configuration,
                    token.Name,
                    "a cancellable operation");
            }
        }
    }

    private static IInvocationOperation[] GetMissingForwardingCalls(
        MethodDeclarationSyntax method,
        SemanticModel semanticModel,
        Hawthorne024Configuration configuration) =>
        method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(syntax => semanticModel.GetOperation(syntax) as IInvocationOperation)
            .Where(invocation => invocation is not null)
            .Where(invocation => OmitsOrDiscardsToken(invocation!, configuration))
            .Cast<IInvocationOperation>()
            .ToArray();

    private static bool OmitsOrDiscardsToken(
        IInvocationOperation invocation,
        Hawthorne024Configuration configuration)
    {
        var tokenParameters = invocation.TargetMethod.Parameters
            .Where(parameter => IsCancellationToken(parameter.Type))
            .ToArray();
        if (tokenParameters.Length == 0)
        {
            return false;
        }

        return tokenParameters.Any(parameter =>
        {
            var argument = invocation.Arguments.FirstOrDefault(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate.Parameter, parameter));
            return argument is null || argument.IsImplicit ||
                configuration.TreatNoneAsMissingForwarding && IsCancellationTokenNone(argument.Value);
        });
    }

    private static bool IsRead(MethodDeclarationSyntax method, IParameterSymbol token, SemanticModel semanticModel) =>
        method.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(identifier).Symbol,
                token));

    private static bool IsCancellationTokenNone(IOperation operation) =>
        operation is IPropertyReferenceOperation property &&
        property.Property.Name == "None" &&
        IsCancellationToken(property.Property.ContainingType);

    private static bool IsCancellationToken(ITypeSymbol? type) =>
        type is INamedTypeSymbol named &&
        named.ContainingNamespace.ToDisplayString() == "System.Threading" &&
        named.Name == "CancellationToken";
}
