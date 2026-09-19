using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW004SingletonAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSymbolAction(c => Analyze((INamedTypeSymbol)c.Symbol, c, configuration), SymbolKind.NamedType);

    private static void Analyze(INamedTypeSymbol type, SymbolAnalysisContext context, HawthorneConfiguration configuration)
    {
        if (type.TypeKind != TypeKind.Class || (configuration.RequireMutableSingletonState && !HasMutableInstanceState(type))) return;
        var staticSelfMember = FindSharedStaticSelfMember(type, context.Compilation);
        if (staticSelfMember is null) return;

        var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(
            HawthorneDiagnosticDescriptors.HAW004, staticSelfMember.Locations.FirstOrDefault() ?? Location.None, configuration, type.Name);
        if (diagnostic is not null) context.ReportDiagnostic(diagnostic);
    }

    private static bool HasMutableInstanceState(INamedTypeSymbol type) =>
        type.GetMembers().OfType<IFieldSymbol>().Any(field => !field.IsStatic && !field.IsReadOnly) ||
        type.GetMembers().OfType<IPropertySymbol>().Any(property => !property.IsStatic && property.SetMethod is { IsInitOnly: false });

    private static ISymbol? FindSharedStaticSelfMember(INamedTypeSymbol type, Compilation compilation)
    {
        var field = type.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(candidate =>
            candidate.IsStatic && SymbolEqualityComparer.Default.Equals(candidate.Type, type));
        if (field is not null) return field;

        var property = type.GetMembers().OfType<IPropertySymbol>().FirstOrDefault(candidate =>
            candidate.IsStatic && SymbolEqualityComparer.Default.Equals(candidate.Type, type) &&
            IsStoredStaticProperty(candidate, type, compilation));
        if (property is not null) return property;

        return type.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(candidate =>
            candidate.IsStatic && SymbolEqualityComparer.Default.Equals(candidate.ReturnType, type) &&
            ReturnsStaticSelfField(candidate, type, compilation));
    }

    private static bool IsStoredStaticProperty(IPropertySymbol property, INamedTypeSymbol type, Compilation compilation)
    {
        foreach (var reference in property.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is not PropertyDeclarationSyntax declaration) continue;
            if (declaration.Initializer is not null) return true;

            var expression = declaration.ExpressionBody?.Expression ??
                declaration.AccessorList?.Accessors
                    .Where(accessor => accessor.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.GetAccessorDeclaration))
                    .Select(accessor => accessor.ExpressionBody?.Expression ??
                        (accessor.Body?.Statements.Count == 1 && accessor.Body.Statements[0] is ReturnStatementSyntax returned ? returned.Expression : null))
                    .FirstOrDefault(candidate => candidate is not null);
            if (expression is not null && IsStaticSelfField(expression, type, compilation.GetSemanticModel(reference.SyntaxTree))) return true;
        }

        return false;
    }

    private static bool ReturnsStaticSelfField(IMethodSymbol method, INamedTypeSymbol type, Compilation compilation) =>
        method.DeclaringSyntaxReferences.Any(reference =>
        {
            var declaration = reference.GetSyntax() as MethodDeclarationSyntax;
            var expression = declaration?.ExpressionBody?.Expression ??
                (declaration?.Body?.Statements.Count == 1 && declaration.Body.Statements[0] is ReturnStatementSyntax returned ? returned.Expression : null);
            return expression is not null && IsStaticSelfField(expression, type, compilation.GetSemanticModel(reference.SyntaxTree));
        });

    private static bool IsStaticSelfField(ExpressionSyntax expression, INamedTypeSymbol type, SemanticModel semanticModel) =>
        semanticModel.GetSymbolInfo(expression).Symbol is IFieldSymbol field &&
        field.IsStatic && SymbolEqualityComparer.Default.Equals(field.Type, type);
}
