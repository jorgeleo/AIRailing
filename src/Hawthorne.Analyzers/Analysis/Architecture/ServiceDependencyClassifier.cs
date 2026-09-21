using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;

namespace Hawthorne.Analyzers.Analysis.Architecture;

internal static class ServiceDependencyClassifier
{
    internal static bool IsServiceLike(ITypeSymbol type, Hawthorne007Configuration configuration)
    {
        if (type.SpecialType != SpecialType.None || type.TypeKind == TypeKind.Enum || type.IsValueType ||
            type is IArrayTypeSymbol || ConfigurationValueClassifier.IsConfigurationType(type, configuration.ConfigurationTypeSuffixes))
        {
            return false;
        }

        if (configuration.ExcludeOptionWrappers && ConfigurationValueClassifier.IsOptionsWrapper(type))
        {
            return false;
        }

        return type.TypeKind is TypeKind.Class or TypeKind.Interface;
    }

}
