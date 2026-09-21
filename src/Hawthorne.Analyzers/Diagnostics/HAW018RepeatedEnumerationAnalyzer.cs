using Hawthorne.Analyzers.Configuration;
using Hawthorne.Analyzers.Analysis.Architecture;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW018RepeatedEnumerationAnalyzer
{
    private static readonly HashSet<string> TerminalOperations = new(StringComparer.Ordinal)
    {
        "Any", "All", "Count", "LongCount", "First", "FirstOrDefault", "Single", "SingleOrDefault",
        "Last", "LastOrDefault", "ToList", "ToArray",
    };

    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSyntaxNodeAction(
            c => Analyze((MethodDeclarationSyntax)c.Node, c, configuration),
            SyntaxKind.MethodDeclaration);

    private static void Analyze(
        MethodDeclarationSyntax method,
        SyntaxNodeAnalysisContext context,
        HawthorneConfiguration configuration)
    {
        if (method.Body is null)
        {
            return;
        }

        var states = new Dictionary<ISymbol, EnumerationState>(SymbolEqualityComparer.Default);
        foreach (var parameter in method.ParameterList.Parameters)
        {
            if (context.SemanticModel.GetDeclaredSymbol(parameter) is IParameterSymbol symbol && EnumerableClassifier.IsCandidateEnumerable(symbol.Type))
            {
                states[symbol] = new EnumerationState();
            }
        }

        foreach (var statement in method.Body.Statements)
        {
            AddDeclaredEnumerableLocals(statement, context.SemanticModel, states);
            if (TryResetMaterializedAssignment(statement, context.SemanticModel, states))
            {
                continue;
            }

            foreach (var receiver in GetEnumerationReceivers(statement, context.SemanticModel))
            {
                if (!states.TryGetValue(receiver, out var state) || state.Reported)
                {
                    continue;
                }

                state.Count++;
                if (state.Count >= configuration.RepeatedEnumeration.MinimumEnumerations)
                {
                    var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(
                        HawthorneDiagnosticDescriptors.HAW018,
                        receiver.Locations.FirstOrDefault(location => location.IsInSource) ?? statement.GetLocation(),
                        configuration,
                        receiver.Name,
                        state.Count);
                    if (diagnostic is not null)
                    {
                        context.ReportDiagnostic(diagnostic);
                    }

                    state.Reported = true;
                }
            }
        }
    }

    private static void AddDeclaredEnumerableLocals(
        StatementSyntax statement,
        SemanticModel semanticModel,
        Dictionary<ISymbol, EnumerationState> states)
    {
        foreach (var variable in statement.DescendantNodesAndSelf().OfType<VariableDeclaratorSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(variable) is ILocalSymbol symbol && EnumerableClassifier.IsCandidateEnumerable(symbol.Type))
            {
                if (!states.ContainsKey(symbol))
                {
                    states.Add(symbol, new EnumerationState());
                }
            }
        }
    }

    private static bool TryResetMaterializedAssignment(
        StatementSyntax statement,
        SemanticModel semanticModel,
        Dictionary<ISymbol, EnumerationState> states)
    {
        if (statement is not ExpressionStatementSyntax
            {
                Expression: AssignmentExpressionSyntax
                {
                    Left: IdentifierNameSyntax left,
                    Right: InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } invocation,
                },
            } ||
            access.Name.Identifier.ValueText is not ("ToList" or "ToArray") ||
            access.Expression is not IdentifierNameSyntax receiver ||
            !SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(left).Symbol, semanticModel.GetSymbolInfo(receiver).Symbol) ||
            semanticModel.GetSymbolInfo(left).Symbol is not { } symbol ||
            !states.TryGetValue(symbol, out var state))
        {
            return false;
        }

        state.Count = 0;
        state.Reported = false;
        return true;
    }

    private static IEnumerable<ISymbol> GetEnumerationReceivers(StatementSyntax statement, SemanticModel semanticModel)
    {
        foreach (var foreachStatement in statement.DescendantNodesAndSelf().OfType<ForEachStatementSyntax>())
        {
            if (semanticModel.GetSymbolInfo(foreachStatement.Expression).Symbol is { } symbol)
            {
                yield return symbol;
            }
        }

        foreach (var invocation in statement.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax access ||
                access.Expression is not IdentifierNameSyntax receiver ||
                semanticModel.GetSymbolInfo(receiver).Symbol is not { } symbol ||
                semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
                !IsEnumerationMethod(method))
            {
                continue;
            }

            yield return symbol;
        }
    }

    private static bool IsEnumerationMethod(IMethodSymbol method) =>
        method.Name == "GetEnumerator" ||
        TerminalOperations.Contains(method.Name) && EnumerableClassifier.IsLinqOperation(method);

    private sealed class EnumerationState
    {
        internal int Count { get; set; }
        internal bool Reported { get; set; }
    }
}
