using System.Collections.Concurrent;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW014DefensiveNullCheckingAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration)
    {
        var invocations = new SourceInvocationNullabilityIndex();
        var candidates = new ConcurrentBag<GuardCandidate>();
        context.RegisterSyntaxNodeAction(
            c => RecordInvocation((InvocationExpressionSyntax)c.Node, c, invocations),
            SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(
            c => RecordMethodGroupUse((IdentifierNameSyntax)c.Node, c, invocations),
            SyntaxKind.IdentifierName);
        context.RegisterSyntaxNodeAction(
            c => RecordCandidate((MethodDeclarationSyntax)c.Node, c, candidates, configuration),
            SyntaxKind.MethodDeclaration);
        context.RegisterCompilationEndAction(c => Analyze(candidates, invocations, c, configuration));
    }

    private static void RecordInvocation(
        InvocationExpressionSyntax invocation,
        SyntaxNodeAnalysisContext context,
        SourceInvocationNullabilityIndex index)
    {
        if (context.SemanticModel.GetOperation(invocation) is not IInvocationOperation operation)
        {
            return;
        }

        var nonNullParameters = operation.Arguments
            .Where(argument => context.SemanticModel.GetTypeInfo(argument.Value.Syntax).Nullability.FlowState == NullableFlowState.NotNull)
            .Select(argument => argument.Parameter?.Ordinal)
            .Where(ordinal => ordinal.HasValue)
            .Select(ordinal => ordinal!.Value);
        index.RecordInvocation(operation.TargetMethod, nonNullParameters);
    }

    private static void RecordMethodGroupUse(
        IdentifierNameSyntax identifier,
        SyntaxNodeAnalysisContext context,
        SourceInvocationNullabilityIndex index)
    {
        if (context.SemanticModel.GetSymbolInfo(identifier).Symbol is not IMethodSymbol method ||
            IsDirectInvocationExpression(identifier))
        {
            return;
        }

        index.RecordIndirectUse(method);
    }

    private static void RecordCandidate(
        MethodDeclarationSyntax methodDeclaration,
        SyntaxNodeAnalysisContext context,
        ConcurrentBag<GuardCandidate> candidates,
        HawthorneConfiguration configuration)
    {
        if (context.SemanticModel.GetDeclaredSymbol(methodDeclaration) is not IMethodSymbol method ||
            !IsCandidateMethod(method, configuration.DefensiveNullChecking))
        {
            return;
        }

        foreach (var parameter in method.Parameters)
        {
            if (!IsNonNullableReferenceParameter(parameter) || HasAllowNullAttribute(parameter) ||
                !TryGetDirectNullGuard(methodDeclaration, parameter, context.SemanticModel, out var guardLocation))
            {
                continue;
            }

            candidates.Add(new GuardCandidate(method, parameter, guardLocation));
        }
    }

    private static void Analyze(
        ConcurrentBag<GuardCandidate> candidates,
        SourceInvocationNullabilityIndex invocations,
        CompilationAnalysisContext context,
        HawthorneConfiguration configuration)
    {
        foreach (var candidate in candidates)
        {
            if (!invocations.HasOnlyNonNullDirectSourceArguments(candidate.Method, candidate.Parameter.Ordinal))
            {
                continue;
            }

            var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(
                HawthorneDiagnosticDescriptors.HAW014,
                candidate.Location,
                configuration,
                candidate.Method.Name,
                candidate.Parameter.Name);
            if (diagnostic is not null)
            {
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool IsCandidateMethod(IMethodSymbol method, Hawthorne014Configuration configuration) =>
        method.Locations.Any(location => location.IsInSource) &&
        !method.IsVirtual &&
        !method.IsOverride &&
        method.ExplicitInterfaceImplementations.Length == 0 &&
        (method.DeclaredAccessibility == Accessibility.Private ||
         configuration.IncludeInternalMethods && method.DeclaredAccessibility == Accessibility.Internal);

    private static bool IsNonNullableReferenceParameter(IParameterSymbol parameter) =>
        parameter.Type.IsReferenceType && parameter.NullableAnnotation == NullableAnnotation.NotAnnotated;

    private static bool HasAllowNullAttribute(IParameterSymbol parameter) =>
        parameter.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == "System.Diagnostics.CodeAnalysis.AllowNullAttribute");

    private static bool TryGetDirectNullGuard(
        MethodDeclarationSyntax method,
        IParameterSymbol parameter,
        SemanticModel semanticModel,
        out Location location)
    {
        location = Location.None;
        if (method.Body is null)
        {
            return false;
        }

        foreach (var statement in method.Body.Statements)
        {
            if (IsDirectNullGuard(statement, parameter, semanticModel))
            {
                location = statement.GetLocation();
                return true;
            }

            if (statement.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(identifier =>
                    SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(identifier).Symbol, parameter)))
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsDirectNullGuard(StatementSyntax statement, IParameterSymbol parameter, SemanticModel semanticModel) =>
        statement is IfStatementSyntax ifStatement &&
        ContainsThrow(ifStatement.Statement) &&
        IsNullCheck(ifStatement.Condition, parameter, semanticModel) ||
        statement is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation } &&
        IsThrowIfNull(invocation, parameter, semanticModel);

    private static bool ContainsThrow(StatementSyntax statement) =>
        statement.DescendantNodesAndSelf().OfType<ThrowStatementSyntax>().Any();

    private static bool IsNullCheck(ExpressionSyntax expression, IParameterSymbol parameter, SemanticModel semanticModel) =>
        expression switch
        {
            BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression } binary =>
                IsParameterReference(binary.Left, parameter, semanticModel) && IsNullLiteral(binary.Right) ||
                IsParameterReference(binary.Right, parameter, semanticModel) && IsNullLiteral(binary.Left),
            IsPatternExpressionSyntax { Pattern: ConstantPatternSyntax { Expression: var constant } } pattern =>
                IsParameterReference(pattern.Expression, parameter, semanticModel) && IsNullLiteral(constant),
            InvocationExpressionSyntax invocation => IsReferenceEqualsNullCheck(invocation, parameter, semanticModel),
            _ => false,
        };

    private static bool IsReferenceEqualsNullCheck(
        InvocationExpressionSyntax invocation,
        IParameterSymbol parameter,
        SemanticModel semanticModel) =>
        semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol
        {
            Name: "ReferenceEquals",
            ContainingType.SpecialType: SpecialType.System_Object,
        } &&
        invocation.ArgumentList.Arguments.Count == 2 &&
        ((IsParameterReference(invocation.ArgumentList.Arguments[0].Expression, parameter, semanticModel) &&
          IsNullLiteral(invocation.ArgumentList.Arguments[1].Expression)) ||
         (IsParameterReference(invocation.ArgumentList.Arguments[1].Expression, parameter, semanticModel) &&
          IsNullLiteral(invocation.ArgumentList.Arguments[0].Expression)));

    private static bool IsThrowIfNull(
        InvocationExpressionSyntax invocation,
        IParameterSymbol parameter,
        SemanticModel semanticModel) =>
        semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol
        {
            Name: "ThrowIfNull",
            ContainingType: { } containingType,
        } &&
        containingType.ToDisplayString() == "System.ArgumentNullException" &&
        invocation.ArgumentList.Arguments.Count > 0 &&
        IsParameterReference(invocation.ArgumentList.Arguments[0].Expression, parameter, semanticModel);

    private static bool IsParameterReference(ExpressionSyntax expression, IParameterSymbol parameter, SemanticModel semanticModel) =>
        SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(expression).Symbol, parameter);

    private static bool IsNullLiteral(ExpressionSyntax expression) => expression.IsKind(SyntaxKind.NullLiteralExpression);

    private static bool IsDirectInvocationExpression(IdentifierNameSyntax identifier)
    {
        SyntaxNode expression = identifier;
        while (expression.Parent is MemberAccessExpressionSyntax { Name: var name } && name == expression ||
               expression.Parent is GenericNameSyntax)
        {
            expression = expression.Parent;
        }

        return expression.Parent is InvocationExpressionSyntax { Expression: var invocationExpression } &&
            invocationExpression == expression;
    }

    private sealed class GuardCandidate(IMethodSymbol method, IParameterSymbol parameter, Location location)
    {
        internal IMethodSymbol Method { get; } = method;
        internal IParameterSymbol Parameter { get; } = parameter;
        internal Location Location { get; } = location;
    }

    private sealed class SourceInvocationNullabilityIndex
    {
        private readonly ConcurrentDictionary<IMethodSymbol, InvocationEvidence> evidence =
            new(SymbolEqualityComparer.Default);

        internal void RecordInvocation(IMethodSymbol method, IEnumerable<int> nonNullParameterOrdinals) =>
            evidence.GetOrAdd(method.OriginalDefinition, _ => new InvocationEvidence())
                .RecordInvocation(nonNullParameterOrdinals, method.Parameters.Length);

        internal void RecordIndirectUse(IMethodSymbol method) =>
            evidence.GetOrAdd(method.OriginalDefinition, _ => new InvocationEvidence()).RecordIndirectUse();

        internal bool HasOnlyNonNullDirectSourceArguments(IMethodSymbol method, int parameterOrdinal) =>
            evidence.TryGetValue(method.OriginalDefinition, out var invocationEvidence) &&
            invocationEvidence.HasOnlyNonNullArguments(parameterOrdinal);

        private sealed class InvocationEvidence
        {
            private readonly ConcurrentDictionary<int, byte> nullableOrMissingArguments = new();
            private int directInvocationCount;
            private int hasIndirectUse;

            internal void RecordInvocation(IEnumerable<int> nonNullParameterOrdinals, int parameterCount)
            {
                var nonNull = new HashSet<int>(nonNullParameterOrdinals);
                Interlocked.Increment(ref directInvocationCount);
                // Parameters absent from the set are either nullable, omitted, or unknown.
                foreach (var ordinal in Enumerable.Range(0, parameterCount).Where(ordinal => !nonNull.Contains(ordinal)))
                {
                    nullableOrMissingArguments.TryAdd(ordinal, 0);
                }
            }

            internal void RecordIndirectUse() => Interlocked.Exchange(ref hasIndirectUse, 1);

            internal bool HasOnlyNonNullArguments(int parameterOrdinal) =>
                Volatile.Read(ref directInvocationCount) > 0 &&
                Volatile.Read(ref hasIndirectUse) == 0 &&
                !nullableOrMissingArguments.ContainsKey(parameterOrdinal);
        }
    }
}
