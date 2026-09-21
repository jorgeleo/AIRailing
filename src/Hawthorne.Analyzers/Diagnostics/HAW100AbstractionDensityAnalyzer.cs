using System.Collections.Immutable;
using System.Globalization;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW100AbstractionDensityAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration)
    {
        context.RegisterSymbolAction(_ => { }, SymbolKind.NamedType);
        context.RegisterCompilationEndAction(c => Analyze(c, configuration));
    }

    private static void Analyze(CompilationAnalysisContext context, HawthorneConfiguration configuration)
    {
        var options = configuration.AbstractionDensity;
        var abstractions = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var behavioral = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        Location? first = null;
        foreach (var tree in context.Compilation.SyntaxTrees)
        {
            var model = context.Compilation.GetSemanticModel(tree);
            foreach (var declaration in tree.GetRoot(context.CancellationToken).DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol type || !type.Locations.Any(l => l.IsInSource)) continue;
                first ??= declaration.Identifier.GetLocation();
                if (type.TypeKind == TypeKind.Interface || type.IsAbstract || options.AbstractionRoleSuffixes.Any(suffix => type.Name.EndsWith(suffix, StringComparison.Ordinal))) abstractions.Add(type);
                if (type.TypeKind == TypeKind.Class && !type.IsAbstract && type.GetMembers().OfType<IMethodSymbol>().Any(m => m.MethodKind == MethodKind.Ordinary && !m.IsAbstract && m.DeclaringSyntaxReferences.Length > 0)) behavioral.Add(type);
            }
        }
        if (first is null || behavioral.Count < options.MinimumBehavioralTypes) return;
        var density = abstractions.Count / (double)behavioral.Count;
        if (options.MaximumDensity is double maximum && density <= maximum) return;
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add("abstractionCount", abstractions.Count.ToString(CultureInfo.InvariantCulture))
            .Add("behavioralCount", behavioral.Count.ToString(CultureInfo.InvariantCulture))
            .Add("density", density.ToString("F2", CultureInfo.InvariantCulture));
        var withEvidence = DiagnosticReportingExtensions.CreateHawthorneDiagnosticWithEvidence(HawthorneDiagnosticDescriptors.HAW100, first, configuration, ImmutableArray<Location>.Empty, properties, density, abstractions.Count, behavioral.Count);
        if (withEvidence is not null) context.ReportDiagnostic(withEvidence);
    }
}
