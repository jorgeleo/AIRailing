using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Hawthorne.Analyzers.Analysis.Architecture;

internal static class TypeRoleClassifier
{
    internal static bool HasAnySuffix(string typeName, ImmutableArray<string> suffixes) =>
        suffixes.Any(suffix => typeName.EndsWith(suffix, StringComparison.Ordinal));

    internal static bool IsEntityFrameworkType(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol named) return false;
        var original = named.OriginalDefinition;
        return original.ContainingNamespace?.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true;
    }
}

internal static class EnumerableClassifier
{
    internal static bool IsLinqOperation(IMethodSymbol method) =>
        method.ContainingType.Name is "Enumerable" or "Queryable" &&
        method.ContainingNamespace.ToDisplayString() == "System.Linq";

    internal static bool IsCandidateEnumerable(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol || type.SpecialType == SpecialType.System_String || type.TypeKind == TypeKind.Error) return false;
        var interfaces = type.AllInterfaces.Append(type).OfType<INamedTypeSymbol>()
            .Select(@interface => @interface.OriginalDefinition.ToDisplayString());
        return interfaces.Contains("System.Collections.Generic.IEnumerable<T>", StringComparer.Ordinal) &&
            !interfaces.Any(@interface => @interface is "System.Collections.Generic.ICollection<T>" or
                "System.Collections.Generic.IReadOnlyCollection<T>" or
                "System.Collections.Generic.IList<T>" or "System.Collections.Generic.IReadOnlyList<T>");
    }
}

internal static class AsyncClassifier
{
    internal static bool IsAsyncReturnType(ITypeSymbol type) => IsTaskLike(type);

    internal static bool IsTaskLike(ITypeSymbol? type) =>
        type is INamedTypeSymbol named &&
        named.OriginalDefinition.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks" &&
        named.OriginalDefinition.Name is "Task" or "ValueTask" && named.OriginalDefinition.Arity is 0 or 1;

    internal static bool IsTask(ITypeSymbol? type) =>
        type is INamedTypeSymbol named && named.OriginalDefinition.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks" && named.OriginalDefinition.Name == "Task";

    internal static bool IsValueTask(ITypeSymbol? type) =>
        type is INamedTypeSymbol named && named.OriginalDefinition.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks" && named.OriginalDefinition.Name == "ValueTask";
}

internal static class ConfigurationValueClassifier
{
    internal static bool IsConfigurationType(ITypeSymbol type, ImmutableArray<string> suffixes) =>
        TypeRoleClassifier.HasAnySuffix(type.Name, suffixes);

    internal static bool IsOptionsWrapper(ITypeSymbol type) =>
        type is INamedTypeSymbol named &&
        named.OriginalDefinition.ContainingNamespace.ToDisplayString() == "Microsoft.Extensions.Options" &&
        named.OriginalDefinition.Name is "IOptions" or "IOptionsSnapshot" or "IOptionsMonitor" &&
        named.OriginalDefinition.Arity == 1;
}

internal static class LoggingClassifier
{
    internal static bool IsLoggerType(ITypeSymbol? type, ImmutableArray<string> configuredNames)
    {
        if (type is not INamedTypeSymbol named) return false;
        var displayName = named.OriginalDefinition.ToDisplayString();
        return configuredNames.Any(configured => displayName.Contains(configured, StringComparison.Ordinal) ||
            named.Name.StartsWith(configured.TrimEnd('<'), StringComparison.Ordinal));
    }

    internal static bool IsLifecycleTemplate(string template, ImmutableArray<string> terms) =>
        terms.Any(term => template.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);

    internal static bool IsLifecycleMethod(IMethodSymbol method) =>
        method.Name.StartsWith("Log", StringComparison.Ordinal) &&
        !method.Name.Contains("Error", StringComparison.Ordinal) &&
        !method.Name.Contains("Warning", StringComparison.Ordinal) &&
        !method.Name.Contains("Critical", StringComparison.Ordinal);
}
