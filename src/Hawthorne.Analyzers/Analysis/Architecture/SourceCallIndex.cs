using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;

namespace Hawthorne.Analyzers.Analysis.Architecture;

internal sealed class SourceCallIndex
{
    private readonly ConcurrentDictionary<IMethodSymbol, ConcurrentDictionary<IMethodSymbol, byte>> callers =
        new(SymbolEqualityComparer.Default);

    internal void Record(IMethodSymbol target, ISymbol? caller)
    {
        if (caller is not IMethodSymbol callerMethod ||
            !callerMethod.Locations.Any(location => location.IsInSource) ||
            !target.Locations.Any(location => location.IsInSource))
        {
            return;
        }

        var targetCallers = callers.GetOrAdd(
            target.OriginalDefinition,
            _ => new ConcurrentDictionary<IMethodSymbol, byte>(SymbolEqualityComparer.Default));
        targetCallers.TryAdd(callerMethod.OriginalDefinition, 0);
    }

    internal int GetSourceCallerCount(IMethodSymbol method) =>
        callers.TryGetValue(method.OriginalDefinition, out var sourceCallers)
            ? sourceCallers.Count
            : 0;
}
