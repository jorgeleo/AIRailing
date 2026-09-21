using Microsoft.CodeAnalysis;

namespace Hawthorne.Analyzers.Analysis.Coupling;

[Flags]
internal enum CouplingSource
{
    ApiSurface = 1,
    StateOrDependency = 2,
    Implementation = 4,
}

internal sealed class CouplingInventory
{
    private readonly Dictionary<INamedTypeSymbol, CouplingSource> _sources =
        new(SymbolEqualityComparer.Default);

    internal int Count => _sources.Count;

    internal void Add(ITypeSymbol? symbol, INamedTypeSymbol owner, CouplingSource source)
    {
        if (symbol is not INamedTypeSymbol named ||
            SymbolEqualityComparer.Default.Equals(named, owner) ||
            named.SpecialType != SpecialType.None)
        {
            return;
        }

        if (named.IsGenericType)
        {
            foreach (var argument in named.TypeArguments)
            {
                Add(argument, owner, source);
            }
        }

        if (IsTransparentWrapper(named))
        {
            return;
        }

        _sources.TryGetValue(named, out var existing);
        _sources[named] = existing | source;
    }

    internal int CountBySource(CouplingSource source) =>
        _sources.Values.Count(sources => (sources & source) != 0);

    internal string FormatExamples() => string.Join(", ", _sources.Keys
        .OrderBy(type => type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), StringComparer.Ordinal)
        .Take(5)
        .Select(type => type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));

    private static bool IsTransparentWrapper(INamedTypeSymbol type)
    {
        if (type.IsGenericType && !type.IsUnboundGenericType)
        {
            return IsTransparentWrapper(type.ConstructUnboundGenericType());
        }

        var containingNamespace = type.ContainingNamespace.ToDisplayString();
        return (containingNamespace, type.Name) is
            ("System.Collections.Generic", "List") or
            ("System.Collections.Generic", "IEnumerable") or
            ("System.Collections.Generic", "ICollection") or
            ("System.Collections.Generic", "Dictionary") or
            ("System.Collections", "IEnumerable") or
            ("System.Collections", "ICollection") or
            ("System.Collections", "Dictionary") or
            ("System.Threading.Tasks", "Task") or
            ("System.Threading.Tasks", "ValueTask") or
            ("System", "Nullable");
    }
}
