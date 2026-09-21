using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;

namespace Hawthorne.Analyzers.Analysis.Architecture;

internal static class ServiceDependencyClassifier
{
    internal static bool IsServiceLike(ITypeSymbol type, Hawthorne007Configuration configuration)
    {
        if (type.SpecialType != SpecialType.None || type.TypeKind == TypeKind.Enum || type.IsValueType ||
            type is IArrayTypeSymbol || configuration.IsConfigurationType(type.Name))
        {
            return false;
        }

        if (configuration.ExcludeOptionWrappers && IsOptionsWrapper(type))
        {
            return false;
        }

        return type.TypeKind is TypeKind.Class or TypeKind.Interface;
    }

    private static bool IsOptionsWrapper(ITypeSymbol type) =>
        type is INamedTypeSymbol named &&
        named.OriginalDefinition.ContainingNamespace.ToDisplayString() == "Microsoft.Extensions.Options" &&
        named.OriginalDefinition.Name is "IOptions" or "IOptionsSnapshot" or "IOptionsMonitor" &&
        named.OriginalDefinition.Arity == 1;
}
