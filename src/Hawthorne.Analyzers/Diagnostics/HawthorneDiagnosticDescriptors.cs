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
        "Interface '{0}' has exactly one detected implementation, '{1}'. Remove the interface or add a demonstrated alternative implementation.");

    internal static readonly DiagnosticDescriptor HAW002 = CreateArchitectureWarning(
        "HAW002",
        "Speculative factory",
        "Factory '{0}' always constructs '{1}' and performs no meaningful selection, configuration, or lifecycle behavior. Instantiate '{1}' directly or add the responsibility that justifies a factory.");

    internal static readonly DiagnosticDescriptor HAW003 = CreateArchitectureWarning(
        "HAW003",
        "Pass-through indirection",
        "Pass-through indirection detected: {0}. Remove the forwarding layer or add behavior that justifies it.");

    internal static readonly DiagnosticDescriptor HAW004 = CreateArchitectureWarning(
        "HAW004",
        "Hidden singleton state",
        "Type '{0}' exposes a static shared instance and contains mutable state. Use dependency injection with an explicit service lifetime instead.");

    internal static readonly DiagnosticDescriptor HAW101 = CreateComplexityWarning(
        "HAW101",
        "Cyclomatic complexity",
        "Method '{0}' has cyclomatic complexity {1}; maximum allowed is {2}. Split branches into smaller methods or simplify the control flow.");

    internal static readonly DiagnosticDescriptor HAW102 = CreateComplexityWarning(
        "HAW102",
        "Cognitive complexity",
        "Method '{0}' has cognitive complexity {1}; maximum allowed is {2}. Flatten nesting or extract a focused method.");

    internal static readonly DiagnosticDescriptor HAW103 = CreateComplexityWarning(
        "HAW103",
        "Maximum nesting depth",
        "Method '{0}' reaches nesting depth {1}; maximum allowed is {2}. Use guard clauses or extract the nested work.");

    internal static readonly DiagnosticDescriptor HAW104 = CreateComplexityWarning(
        "HAW104",
        "Class coupling",
        "Type '{0}' depends on {1} distinct external types; maximum allowed is {2}. API surface: {3}; state/dependency: {4}; implementation: {5}. Examples: {6}. Split responsibilities or introduce a focused boundary.");

    internal static readonly DiagnosticDescriptor HAW105 = CreateComplexityWarning(
        "HAW105",
        "Method length",
        "Method length exceeds the configured limit: {0}. Split the method into focused operations.");

    internal static readonly DiagnosticDescriptor HAW900 = new(
        "HAW900",
        "Invalid Hawthorne configuration",
        "Invalid configuration: {0}. Correct hawthorne.json before running normal Hawthorne analysis.",
        ConfigurationCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Hawthorne configuration must be valid before normal analyzer diagnostics are produced.",
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    internal static readonly DiagnosticDescriptor HAW901 = new(
        "HAW901",
        "Hawthorne suppression must be justified",
        "Hawthorne suppression '{0}' must include a non-empty Justification",
        ConfigurationCategory,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Hawthorne diagnostics may be suppressed only with a non-empty justification.");

    internal static ImmutableArray<DiagnosticDescriptor> All { get; } = ImmutableArray.Create(
        HAW001, HAW002, HAW003, HAW004, HAW101, HAW102, HAW103, HAW104, HAW105, HAW900, HAW901);

    private static DiagnosticDescriptor CreateArchitectureWarning(string id, string title, string messageFormat) =>
        new(id, title, messageFormat, ArchitectureCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static DiagnosticDescriptor CreateComplexityWarning(string id, string title, string messageFormat) =>
        new(id, title, messageFormat, ComplexityCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true);
}
