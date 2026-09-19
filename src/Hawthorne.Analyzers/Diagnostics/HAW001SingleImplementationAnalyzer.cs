using System.Collections.Concurrent;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW001SingleImplementationAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration)
    {
        var implementations = new ConcurrentDictionary<INamedTypeSymbol, ConcurrentBag<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
        context.RegisterSymbolAction(c =>
        {
            var type = (INamedTypeSymbol)c.Symbol;
            if (type.TypeKind != TypeKind.Class || type.IsAbstract) return;
            foreach (var @interface in type.AllInterfaces)
                implementations.GetOrAdd(@interface, _ => new ConcurrentBag<INamedTypeSymbol>()).Add(type);
        }, SymbolKind.NamedType);
        context.RegisterCompilationEndAction(c =>
        {
            foreach (var entry in implementations.Where(entry => entry.Value.Count == 1))
            {
                var implementation = entry.Value.Single();
                var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW001,
                    entry.Key.Locations.FirstOrDefault() ?? Location.None, configuration, entry.Key.Name, implementation.Name);
                if (diagnostic is not null) c.ReportDiagnostic(diagnostic);
            }
        });
    }
}
