using System.Collections.Concurrent;
using Hawthorne.Analyzers.Analysis.Architecture;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW015DeadConfigurationAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration)
    {
        var sourceTypes = new ConcurrentBag<INamedTypeSymbol>();
        var readProperties = new ConcurrentDictionary<IPropertySymbol, byte>(SymbolEqualityComparer.Default);
        var optionPayloads = new ConcurrentDictionary<INamedTypeSymbol, byte>(SymbolEqualityComparer.Default);

        context.RegisterSymbolAction(
            c => sourceTypes.Add((INamedTypeSymbol)c.Symbol),
            SymbolKind.NamedType);
        context.RegisterSyntaxNodeAction(
            c => RecordPropertyRead((IdentifierNameSyntax)c.Node, c, readProperties),
            SyntaxKind.IdentifierName);
        context.RegisterSyntaxNodeAction(
            c => RecordOptionsPayload((GenericNameSyntax)c.Node, c, optionPayloads),
            SyntaxKind.GenericName);
        context.RegisterCompilationEndAction(c => Analyze(
            sourceTypes,
            optionPayloads,
            readProperties,
            c,
            configuration));
    }

    private static void RecordPropertyRead(
        IdentifierNameSyntax identifier,
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<IPropertySymbol, byte> readProperties)
    {
        if (context.SemanticModel.GetSymbolInfo(identifier).Symbol is not IPropertySymbol property ||
            IsWriteTarget(identifier) ||
            identifier.Ancestors().Any(ancestor => ancestor is AttributeSyntax))
        {
            return;
        }

        readProperties.TryAdd(property.OriginalDefinition, 0);
    }

    private static void RecordOptionsPayload(
        GenericNameSyntax genericName,
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<INamedTypeSymbol, byte> optionPayloads)
    {
        if (context.SemanticModel.GetTypeInfo(genericName).Type is not INamedTypeSymbol optionsType ||
            !ConfigurationValueClassifier.IsOptionsWrapper(optionsType) ||
            optionsType.TypeArguments.Length != 1 ||
            optionsType.TypeArguments[0] is not INamedTypeSymbol payload ||
            !payload.Locations.Any(location => location.IsInSource))
        {
            return;
        }

        optionPayloads.TryAdd(payload.OriginalDefinition, 0);
    }

    private static void Analyze(
        ConcurrentBag<INamedTypeSymbol> sourceTypes,
        ConcurrentDictionary<INamedTypeSymbol, byte> optionPayloads,
        ConcurrentDictionary<IPropertySymbol, byte> readProperties,
        CompilationAnalysisContext context,
        HawthorneConfiguration configuration)
    {
        foreach (var type in sourceTypes)
        {
            if (!type.Locations.Any(location => location.IsInSource) ||
                (!configuration.DeadConfiguration.IsConfigurationType(type.Name) &&
                 !optionPayloads.ContainsKey(type.OriginalDefinition)))
            {
                continue;
            }

            foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
            {
                if (!IsCandidate(property, configuration.DeadConfiguration) ||
                    readProperties.ContainsKey(property.OriginalDefinition))
                {
                    continue;
                }

                var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(
                    HawthorneDiagnosticDescriptors.HAW015,
                    property.Locations.First(location => location.IsInSource),
                    configuration,
                    property.Name);
                if (diagnostic is not null)
                {
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private static bool IsCandidate(IPropertySymbol property, Hawthorne015Configuration configuration) =>
        !property.IsIndexer &&
        property.DeclaredAccessibility is Accessibility.Private or Accessibility.Internal &&
        (property.DeclaredAccessibility == Accessibility.Private || configuration.IncludeInternalProperties) &&
        property.GetAttributes().Length == 0 &&
        property.Locations.Any(location => location.IsInSource);

    private static bool IsWriteTarget(IdentifierNameSyntax identifier) =>
        identifier.Parent is AssignmentExpressionSyntax assignment &&
        assignment.Left.DescendantNodesAndSelf().Contains(identifier);
}
