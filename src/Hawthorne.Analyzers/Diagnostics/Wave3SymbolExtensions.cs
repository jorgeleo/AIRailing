using Microsoft.CodeAnalysis;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class Wave3SymbolExtensions
{
    internal static IEnumerable<INamedTypeSymbol> GetNamespaceTypes(this INamespaceSymbol namespaceSymbol)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers()) yield return type;
        foreach (var child in namespaceSymbol.GetNamespaceMembers())
            foreach (var type in child.GetNamespaceTypes()) yield return type;
    }

    internal static bool InheritsFrom(this ITypeSymbol type, ITypeSymbol baseType)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current, baseType)) return true;
        return false;
    }
}
