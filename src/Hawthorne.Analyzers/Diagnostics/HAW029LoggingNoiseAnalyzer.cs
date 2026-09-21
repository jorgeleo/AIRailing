using Hawthorne.Analyzers.Analysis.Architecture;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW029LoggingNoiseAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSymbolAction(c => Analyze((INamedTypeSymbol)c.Symbol, c, configuration), SymbolKind.NamedType);

    private static void Analyze(INamedTypeSymbol type, SymbolAnalysisContext context, HawthorneConfiguration configuration)
    {
        var options = configuration.LoggingNoise;
        if (!type.Locations.Any(l => l.IsInSource)) return;
        var methods = type.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary && !m.IsAbstract && m.DeclaringSyntaxReferences.Length > 0).ToArray();
        if (methods.Length < options.MinimumMethodCount) return;
        var lifecycle = methods.Count(m => HasLifecycleLog(m, context.Compilation, options, context.CancellationToken));
        var ratio = lifecycle / (double)methods.Length;
        if (ratio < options.MaximumLifecycleLogRatio) return;
        var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW029, type.Locations.First(l => l.IsInSource), configuration, type.Name, ratio);
        if (diagnostic is not null) context.ReportDiagnostic(diagnostic);
    }

    private static bool HasLifecycleLog(IMethodSymbol method, Compilation compilation, Hawthorne029Configuration options, CancellationToken cancellationToken)
    {
        if (method.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) is not MethodDeclarationSyntax declaration)
        {
            return false;
        }

        var model = compilation.GetSemanticModel(declaration.SyntaxTree);
        foreach (var invocation in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol target || !LoggingClassifier.IsLifecycleMethod(target)) continue;
            if (invocation.Expression is not MemberAccessExpressionSyntax access || !LoggingClassifier.IsLoggerType(model.GetTypeInfo(access.Expression, cancellationToken).Type, options.LoggerTypeNames)) continue;
            if (invocation.ArgumentList.Arguments.Count == 0 || model.GetConstantValue(invocation.ArgumentList.Arguments[0].Expression, cancellationToken) is not { HasValue: true, Value: string template } ||
                !LoggingClassifier.IsLifecycleTemplate(template, options.LifecycleTerms)) continue;
            if (invocation.ArgumentList.Arguments.Skip(1).Any(argument => IsException(model.GetTypeInfo(argument.Expression, cancellationToken).Type, compilation))) continue;
            return true;
        }
        return false;
    }

    private static bool IsException(ITypeSymbol? type, Compilation compilation)
    {
        var exception = compilation.GetTypeByMetadataName("System.Exception");
        return type is not null && exception is not null && (SymbolEqualityComparer.Default.Equals(type, exception) || type.InheritsFrom(exception));
    }
}
