using Hawthorne.Analyzers.Analysis.Architecture;
using Hawthorne.Analyzers.Analysis.Metrics;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW006PrematureGeneralizationAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration)
    {
        var derivedTypes = new DerivedTypeIndex();
        context.RegisterSymbolAction(
            c => derivedTypes.RecordConcreteDerivedType((INamedTypeSymbol)c.Symbol),
            SymbolKind.NamedType);
        context.RegisterSyntaxNodeAction(
            c => derivedTypes.RecordClosedGenericConstruction(
                c.SemanticModel.GetTypeInfo((GenericNameSyntax)c.Node).Type as INamedTypeSymbol),
            SyntaxKind.GenericName);
        context.RegisterCompilationEndAction(c => Analyze(derivedTypes, c, configuration));
    }

    private static void Analyze(
        DerivedTypeIndex derivedTypes,
        CompilationAnalysisContext context,
        HawthorneConfiguration configuration)
    {
        foreach (var type in derivedTypes.GetCandidates())
        {
            if (HasSignificantSharedBehavior(type, configuration.PrematureGeneralization))
            {
                continue;
            }

            var concreteDerivedTypeCount = derivedTypes.GetConcreteDerivedTypeCount(type);
            var closedGenericConstructionCount = derivedTypes.GetClosedGenericConstructionCount(type);
            var hasSingleDerivedType = concreteDerivedTypeCount == configuration.PrematureGeneralization.MinimumDerivedTypes;
            var hasSingleClosedGenericConstruction = configuration.PrematureGeneralization.AnalyzeSingleClosedGenericUse &&
                closedGenericConstructionCount == 1;
            if (!hasSingleDerivedType && !hasSingleClosedGenericConstruction)
            {
                continue;
            }

            var evidence = GetEvidence(hasSingleDerivedType, hasSingleClosedGenericConstruction);
            var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(
                HawthorneDiagnosticDescriptors.HAW006,
                type.Locations.First(location => location.IsInSource),
                configuration,
                type.Name,
                evidence);
            if (diagnostic is not null)
            {
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool HasSignificantSharedBehavior(
        INamedTypeSymbol type,
        Hawthorne006Configuration configuration)
    {
        if (type.GetMembers().Any(member =>
                member.Locations.Any(location => location.IsInSource) &&
                member is IFieldSymbol { IsConst: false } or IPropertySymbol { IsAbstract: false } or IEventSymbol))
        {
            return true;
        }

        foreach (var member in type.GetMembers().OfType<IMethodSymbol>())
        {
            if (member.IsAbstract || member.MethodKind is not (MethodKind.Ordinary or MethodKind.Constructor))
            {
                continue;
            }

            foreach (var syntaxReference in member.DeclaringSyntaxReferences)
            {
                var statements = syntaxReference.GetSyntax() switch
                {
                    MethodDeclarationSyntax method => MethodLengthCalculator.CountExecutableStatements(method),
                    ConstructorDeclarationSyntax constructor => CountExecutableStatements(constructor),
                    _ => 0,
                };
                if (statements > configuration.MaximumSharedExecutableStatements)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int CountExecutableStatements(ConstructorDeclarationSyntax constructor) =>
        constructor.Body?.DescendantNodes().OfType<StatementSyntax>().Count(statement => statement is not BlockSyntax) ??
        (constructor.ExpressionBody is null ? 0 : 1);

    private static string GetEvidence(bool hasSingleDerivedType, bool hasSingleClosedGenericConstruction) =>
        (hasSingleDerivedType, hasSingleClosedGenericConstruction) switch
        {
            (true, true) => "one source concrete derived type and one source-observed closed generic construction",
            (true, false) => "one source concrete derived type",
            _ => "one source-observed closed generic construction",
        };
}
