using Hawthorne.Analyzers.Analysis.Architecture;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW007ConstructorDependencyAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSymbolAction(
            c => Analyze((INamedTypeSymbol)c.Symbol, c, configuration),
            SymbolKind.NamedType);

    private static void Analyze(
        INamedTypeSymbol type,
        SymbolAnalysisContext context,
        HawthorneConfiguration configuration)
    {
        if (type.TypeKind != TypeKind.Class || type.IsAbstract || !type.Locations.Any(location => location.IsInSource))
        {
            return;
        }

        foreach (var constructor in type.InstanceConstructors.Where(constructor => !constructor.IsImplicitlyDeclared))
        {
            var dependencyCount = constructor.Parameters.Count(parameter =>
                ServiceDependencyClassifier.IsServiceLike(parameter.Type, configuration.ConstructorDependencies));
            if (dependencyCount <= configuration.ConstructorDependencies.MaximumDependencies)
            {
                continue;
            }

            var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(
                HawthorneDiagnosticDescriptors.HAW007,
                constructor.Locations.FirstOrDefault(location => location.IsInSource) ?? type.Locations[0],
                configuration,
                constructor.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                dependencyCount,
                configuration.ConstructorDependencies.MaximumDependencies);
            if (diagnostic is not null)
            {
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
