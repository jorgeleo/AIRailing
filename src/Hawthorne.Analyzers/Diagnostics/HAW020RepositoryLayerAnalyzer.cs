using Hawthorne.Analyzers.Analysis.Architecture;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW020RepositoryLayerAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSymbolAction(c => Analyze((INamedTypeSymbol)c.Symbol, c, configuration), SymbolKind.NamedType);

    private static void Analyze(INamedTypeSymbol type, SymbolAnalysisContext context, HawthorneConfiguration configuration)
    {
        var options = configuration.RepositoryLayer;
        if (!type.Locations.Any(l => l.IsInSource) || type.TypeKind != TypeKind.Class ||
            (options.RequireRepositorySuffix && !options.HasRepositorySuffix(type.Name)))
        {
            return;
        }

        var eligible = type.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && m.DeclaredAccessibility == Accessibility.Public && !m.IsOverride && m.DeclaringSyntaxReferences.Length > 0)
            .ToArray();
        if (eligible.Length == 0) return;

        var forwarded = new Dictionary<ISymbol, int>(SymbolEqualityComparer.Default);
        foreach (var method in eligible)
        {
            if (method.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken) is not MethodDeclarationSyntax declaration ||
                !ForwardingMethodClassifier.TryGetForwardedDependency(declaration, context.Compilation.GetSemanticModel(declaration.SyntaxTree), out var dependency) ||
                dependency is null || !IsEntityFrameworkDependency(dependency))
            {
                continue;
            }

            forwarded[dependency] = forwarded.TryGetValue(dependency, out var count) ? count + 1 : 1;
        }

        var dominant = forwarded.OrderByDescending(p => p.Value).ThenBy(p => p.Key.Name, StringComparer.Ordinal).FirstOrDefault();
        var ratio = dominant.Value / (double)eligible.Length;
        if (dominant.Key is null || dominant.Value < options.MinimumForwardingMethods || ratio < options.MinimumForwardingRatio) return;

        var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(
            HawthorneDiagnosticDescriptors.HAW020,
            type.Locations.First(l => l.IsInSource),
            configuration,
            type.Name,
            ratio,
            dominant.Key.Name);
        if (diagnostic is not null) context.ReportDiagnostic(diagnostic);
    }

    private static bool IsEntityFrameworkDependency(ISymbol dependency)
    {
        var type = dependency switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            IParameterSymbol parameter => parameter.Type,
            _ => null,
        };
        return TypeRoleClassifier.IsEntityFrameworkType(type);
    }
}
