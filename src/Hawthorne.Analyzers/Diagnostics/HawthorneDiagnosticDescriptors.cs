using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HawthorneDiagnosticDescriptors
{
    private const string ArchitectureCategory = "Hawthorne.Architecture";
    private const string ComplexityCategory = "Hawthorne.Complexity";
    private const string ConfigurationCategory = "Hawthorne.Configuration";

    internal static readonly DiagnosticDescriptor HAW001 = CreateArchitectureWarning(
        "HAW001",
        "Redundant abstraction",
        "Interface '{0}' has exactly one detected implementation, '{1}'");

    internal static readonly DiagnosticDescriptor HAW002 = CreateArchitectureWarning(
        "HAW002",
        "Speculative factory",
        "Factory '{0}' always constructs '{1}' and performs no meaningful selection, configuration, or lifecycle behavior");

    internal static readonly DiagnosticDescriptor HAW003 = CreateArchitectureWarning(
        "HAW003",
        "Pass-through indirection",
        "Pass-through indirection detected: {0}");

    internal static readonly DiagnosticDescriptor HAW004 = CreateArchitectureWarning(
        "HAW004",
        "Hidden singleton state",
        "Type '{0}' exposes a static shared instance and contains mutable state");

    internal static readonly DiagnosticDescriptor HAW101 = CreateComplexityWarning(
        "HAW101",
        "Cyclomatic complexity",
        "Method '{0}' has cyclomatic complexity {1}; maximum allowed is {2}");

    internal static readonly DiagnosticDescriptor HAW102 = CreateComplexityWarning(
        "HAW102",
        "Cognitive complexity",
        "Method '{0}' has cognitive complexity {1}; maximum allowed is {2}");

    internal static readonly DiagnosticDescriptor HAW103 = CreateComplexityWarning(
        "HAW103",
        "Maximum nesting depth",
        "Method '{0}' reaches nesting depth {1}; maximum allowed is {2}");

    internal static readonly DiagnosticDescriptor HAW104 = CreateComplexityWarning(
        "HAW104",
        "Class coupling",
        "Type '{0}' depends on {1} distinct external types; maximum allowed is {2}");

    internal static readonly DiagnosticDescriptor HAW105 = CreateComplexityWarning(
        "HAW105",
        "Method length",
        "Method length exceeds the configured limit: {0}");

    internal static readonly DiagnosticDescriptor HAW900 = new(
        "HAW900",
        "Invalid Hawthorne configuration",
        "Invalid configuration: {0}",
        ConfigurationCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Hawthorne configuration must be valid before normal analyzer diagnostics are produced.");

    internal static readonly DiagnosticDescriptor HAW901 = new(
        "HAW901",
        "Hawthorne pragma suppression is not permitted",
        "Pragma suppresses Hawthorne diagnostic(s): {0}. Use a documented file exception in hawthorne.json instead.",
        ConfigurationCategory,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Hawthorne diagnostics must be governed by hawthorne.json rather than source pragmas.");

    internal static ImmutableArray<DiagnosticDescriptor> All { get; } = ImmutableArray.Create(
        HAW001, HAW002, HAW003, HAW004, HAW101, HAW102, HAW103, HAW104, HAW105, HAW900, HAW901);

    private static DiagnosticDescriptor CreateArchitectureWarning(string id, string title, string messageFormat) =>
        new(id, title, messageFormat, ArchitectureCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static DiagnosticDescriptor CreateComplexityWarning(string id, string title, string messageFormat) =>
        new(id, title, messageFormat, ComplexityCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true);
}
