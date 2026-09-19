using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW004SingletonAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSymbolAction(c => Analyze((INamedTypeSymbol)c.Symbol, c, configuration), SymbolKind.NamedType);

    private static void Analyze(INamedTypeSymbol type, SymbolAnalysisContext context, HawthorneConfiguration configuration)
    {
        if (type.TypeKind != TypeKind.Class || (configuration.RequireMutableSingletonState && !HasMutableInstanceState(type))) return;
        var staticSelfMember = type.GetMembers().FirstOrDefault(member => member.IsStatic &&
            ((member is IPropertySymbol property && SymbolEqualityComparer.Default.Equals(property.Type, type)) ||
             (member is IMethodSymbol method && SymbolEqualityComparer.Default.Equals(method.ReturnType, type))));
        if (staticSelfMember is null) return;

        var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(
            HawthorneDiagnosticDescriptors.HAW004, staticSelfMember.Locations.FirstOrDefault() ?? Location.None, configuration, type.Name);
        if (diagnostic is not null) context.ReportDiagnostic(diagnostic);
    }

    private static bool HasMutableInstanceState(INamedTypeSymbol type) =>
        type.GetMembers().OfType<IFieldSymbol>().Any(field => !field.IsStatic && !field.IsReadOnly) ||
        type.GetMembers().OfType<IPropertySymbol>().Any(property => !property.IsStatic && property.SetMethod is not null);
}
