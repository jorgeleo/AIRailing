using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;

namespace Hawthorne.Analyzers.Analysis.Architecture;

internal sealed class DerivedTypeIndex
{
    private readonly ConcurrentDictionary<INamedTypeSymbol, ConcurrentDictionary<INamedTypeSymbol, byte>> concreteDerivedTypes =
        new(SymbolEqualityComparer.Default);
    private readonly ConcurrentDictionary<INamedTypeSymbol, ConcurrentDictionary<string, byte>> closedGenericConstructions =
        new(SymbolEqualityComparer.Default);
    private readonly ConcurrentDictionary<INamedTypeSymbol, byte> candidates =
        new(SymbolEqualityComparer.Default);

    internal void RecordConcreteDerivedType(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class || type.IsAbstract || !HasSourceLocation(type))
        {
            return;
        }

        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            var baseDefinition = baseType.OriginalDefinition;
            if (!IsSourceAbstractClass(baseDefinition))
            {
                continue;
            }

            candidates.TryAdd(baseDefinition, 0);
            var derivedTypes = concreteDerivedTypes.GetOrAdd(
                baseDefinition,
                _ => new ConcurrentDictionary<INamedTypeSymbol, byte>(SymbolEqualityComparer.Default));
            derivedTypes.TryAdd(type.OriginalDefinition, 0);
        }
    }

    internal void RecordClosedGenericConstruction(INamedTypeSymbol? type)
    {
        if (type is null ||
            !type.IsGenericType ||
            type.IsUnboundGenericType ||
            type.TypeArguments.Any(argument => argument.TypeKind == TypeKind.TypeParameter))
        {
            return;
        }

        var definition = type.OriginalDefinition;
        if (!IsSourceAbstractClass(definition))
        {
            return;
        }

        candidates.TryAdd(definition, 0);
        var constructions = closedGenericConstructions.GetOrAdd(
            definition,
            _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
        constructions.TryAdd(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), 0);
    }

    internal IEnumerable<INamedTypeSymbol> GetCandidates() => candidates.Keys;

    internal int GetConcreteDerivedTypeCount(INamedTypeSymbol type) =>
        concreteDerivedTypes.TryGetValue(type.OriginalDefinition, out var derivedTypes)
            ? derivedTypes.Count
            : 0;

    internal int GetClosedGenericConstructionCount(INamedTypeSymbol type) =>
        closedGenericConstructions.TryGetValue(type.OriginalDefinition, out var constructions)
            ? constructions.Count
            : 0;

    private static bool IsSourceAbstractClass(INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Class && type.IsAbstract && HasSourceLocation(type);

    private static bool HasSourceLocation(INamedTypeSymbol type) =>
        type.Locations.Any(location => location.IsInSource);
}
