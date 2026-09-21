using Hawthorne.Analyzers.Configuration;
using Hawthorne.Analyzers.Analysis.Architecture;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW017UnnecessaryLinqMaterializationAnalyzer
{
    private static readonly HashSet<string> SupportedFollowUpOperations = new(StringComparer.Ordinal)
    {
        "Where", "Select", "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending",
        "Skip", "Take", "Any", "All", "Count", "LongCount", "First", "FirstOrDefault",
        "Single", "SingleOrDefault", "ToList", "ToArray",
    };

    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterOperationAction(
            c => Analyze((IInvocationOperation)c.Operation, c, configuration),
            OperationKind.Invocation);

    private static void Analyze(
        IInvocationOperation operation,
        OperationAnalysisContext context,
        HawthorneConfiguration configuration)
    {
        if (!IsEnumerableOperation(operation.TargetMethod) ||
            !SupportedFollowUpOperations.Contains(operation.TargetMethod.Name) ||
            GetReceiver(operation) is not IInvocationOperation materialization ||
            !IsEnumerableOperation(materialization.TargetMethod) ||
            !IsEnabledMaterialization(materialization.TargetMethod.Name, configuration.UnnecessaryLinqMaterialization))
        {
            return;
        }

        var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(
            HawthorneDiagnosticDescriptors.HAW017,
            materialization.Syntax.GetLocation(),
            configuration,
            materialization.TargetMethod.Name,
            operation.TargetMethod.Name);
        if (diagnostic is not null)
        {
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static IOperation? GetReceiver(IInvocationOperation operation) =>
        UnwrapImplicitConversion(operation.Instance ??
            (operation.Arguments.Length > 0 ? operation.Arguments[0].Value : null));

    private static IOperation? UnwrapImplicitConversion(IOperation? operation)
    {
        while (operation is IConversionOperation { IsImplicit: true } conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    private static bool IsEnabledMaterialization(string methodName, Hawthorne017Configuration configuration) =>
        methodName == "ToList" && configuration.AnalyzeToList ||
        methodName == "ToArray" && configuration.AnalyzeToArray;

    private static bool IsEnumerableOperation(IMethodSymbol method) =>
        EnumerableClassifier.IsLinqOperation(method);
}
