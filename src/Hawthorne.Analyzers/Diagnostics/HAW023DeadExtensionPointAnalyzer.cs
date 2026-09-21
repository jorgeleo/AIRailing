using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW023DeadExtensionPointAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration)
    {
        context.RegisterSymbolAction(_ => { }, SymbolKind.NamedType);
        context.RegisterCompilationEndAction(c => Analyze(c, configuration));
    }

    private static void Analyze(CompilationAnalysisContext context, HawthorneConfiguration configuration)
    {
        var options = configuration.DeadExtensionPoints;
        if (!options.IncludePrivateMembers && !options.IncludeExternallyAccessibleMembers) return;

        var candidates = new List<(ISymbol Symbol, Location Location)>();
        foreach (var tree in context.Compilation.SyntaxTrees)
        {
            var model = context.Compilation.GetSemanticModel(tree);
            foreach (var eventDeclaration in tree.GetRoot(context.CancellationToken).DescendantNodes().OfType<EventDeclarationSyntax>())
            {
                if (!options.AnalyzeEvents || model.GetDeclaredSymbol(eventDeclaration, context.CancellationToken) is not IEventSymbol symbol || !IsCandidate(symbol, options)) continue;
                candidates.Add((symbol, eventDeclaration.Identifier.GetLocation()));
            }
            foreach (var field in tree.GetRoot(context.CancellationToken).DescendantNodes().OfType<EventFieldDeclarationSyntax>())
            {
                if (!options.AnalyzeEvents) continue;
                foreach (var variable in field.Declaration.Variables)
                {
                    if (model.GetDeclaredSymbol(variable, context.CancellationToken) is IEventSymbol symbol && IsCandidate(symbol, options))
                        candidates.Add((symbol, variable.Identifier.GetLocation()));
                }
            }
            foreach (var property in tree.GetRoot(context.CancellationToken).DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                if (!options.AnalyzeCallbacks || model.GetDeclaredSymbol(property, context.CancellationToken) is not IPropertySymbol symbol || !IsCandidate(symbol, options) || symbol.Type.TypeKind != TypeKind.Delegate) continue;
                candidates.Add((symbol, property.Identifier.GetLocation()));
            }
            foreach (var method in tree.GetRoot(context.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (!options.AnalyzeVirtualHooks || model.GetDeclaredSymbol(method, context.CancellationToken) is not IMethodSymbol symbol || !symbol.IsVirtual || !IsCandidate(symbol, options)) continue;
                if (symbol.DeclaredAccessibility == Accessibility.Protected && !symbol.ContainingType.IsSealed && HasSourceDerivedType(symbol.ContainingType, context.Compilation)) continue;
                candidates.Add((symbol, method.Identifier.GetLocation()));
            }
        }

        var used = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var tree in context.Compilation.SyntaxTrees)
        {
            var model = context.Compilation.GetSemanticModel(tree);
            foreach (var identifier in tree.GetRoot(context.CancellationToken).DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var symbol = model.GetSymbolInfo(identifier, context.CancellationToken).Symbol;
                if (symbol is not null) used.Add(symbol);
            }
        }

        foreach (var candidate in candidates.GroupBy(c => c.Symbol, SymbolEqualityComparer.Default).Select(group => group.First()))
        {
            if (used.Contains(candidate.Symbol)) continue;
            var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW023, candidate.Location, configuration, candidate.Symbol.Name);
            if (diagnostic is not null) context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsCandidate(ISymbol symbol, Hawthorne023Configuration options) =>
        options.IncludeExternallyAccessibleMembers ||
        (options.IncludePrivateMembers && (symbol.DeclaredAccessibility == Accessibility.Private ||
            (symbol.DeclaredAccessibility == Accessibility.Protected && symbol.ContainingType?.IsSealed == true)));

    private static bool HasSourceDerivedType(INamedTypeSymbol type, Compilation compilation) =>
        compilation.Assembly.GlobalNamespace.GetNamespaceTypes().Any(candidate => candidate.BaseType is not null && SymbolEqualityComparer.Default.Equals(candidate.BaseType, type));
}
