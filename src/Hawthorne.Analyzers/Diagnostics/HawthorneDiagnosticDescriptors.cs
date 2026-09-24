using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HawthorneDiagnosticDescriptors
{
    internal const string NonSuppressibleTag = "Hawthorne.NonSuppressible";

    private const string ArchitectureCategory = "Hawthorne.Architecture";
    private const string ComplexityCategory = "Hawthorne.Complexity";
    private const string ReliabilityCategory = "Hawthorne.Reliability";
    private const string HealthCategory = "Hawthorne.Health";
    private const string FormattingCategory = "Hawthorne.Formatting";
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

    internal static readonly DiagnosticDescriptor HAW005 = CreateArchitectureWarning(
        "HAW005",
        "Needless wrapper explosion",
        "Wrapper '{0}' forwards {1:P0} of its eligible public methods to '{2}' without adding behavior. Remove the wrapper or add the responsibility that justifies it.");

    internal static readonly DiagnosticDescriptor HAW006 = CreateArchitectureWarning(
        "HAW006",
        "Premature generalization",
        "Abstract type '{0}' has {1} and no significant shared behavior. Use the concrete type until another use justifies the abstraction.");

    internal static readonly DiagnosticDescriptor HAW007 = CreateArchitectureWarning(
        "HAW007",
        "Constructor dependency explosion",
        "Constructor '{0}' receives {1} service-like dependencies; maximum allowed is {2}. Split the responsibility or introduce a cohesive collaborator.");

    internal static readonly DiagnosticDescriptor HAW008 = CreateArchitectureWarning(
        "HAW008",
        "Boolean parameter control flow",
        "Method '{0}' uses {1} boolean parameters to control separate behavior branches. Replace behavior flags with focused operations or an explicit options model.");

    internal static readonly DiagnosticDescriptor HAW010 = CreateArchitectureWarning(
        "HAW010",
        "One-method service class",
        "Service-style type '{0}' has one small public operation with {1} source caller(s). Co-locate the operation with its cohesive owner unless a boundary is demonstrated.");

    internal static readonly DiagnosticDescriptor HAW011 = CreateArchitectureWarning(
        "HAW011",
        "Excessive micro-methods",
        "Type '{0}' has {1:P0} tiny private methods; maximum allowed is {2:P0}. Inline obvious helpers or group cohesive behavior.");

    internal static readonly DiagnosticDescriptor HAW013 = CreateReliabilityWarning(
        "HAW013",
        "Exception laundering",
        "Catch block wraps '{0}' in generic exception '{1}' without meaningful handling or context. Preserve the original exception or add recoverable domain context.");

    internal static readonly DiagnosticDescriptor HAW014 = CreateArchitectureWarning(
        "HAW014",
        "Redundant defensive null check",
        "Method '{0}' guards non-nullable parameter '{1}' although all known source callers provide a non-null value. Remove the redundant private-boundary check.");

    internal static readonly DiagnosticDescriptor HAW015 = CreateArchitectureWarning(
        "HAW015",
        "Dead configuration",
        "Configuration property '{0}' is never meaningfully read in this compilation. Remove it or connect it to behavior.");

    internal static readonly DiagnosticDescriptor HAW016 = CreateReliabilityWarning(
        "HAW016",
        "Fake async",
        "Method '{0}' exposes asynchronous semantics but only performs synchronous work. Return the synchronous result or introduce genuine asynchronous work.");

    internal static readonly DiagnosticDescriptor HAW017 = CreateReliabilityWarning(
        "HAW017",
        "Unnecessary LINQ materialization",
        "Materialization by '{0}' is immediately followed by '{1}', which can operate on the original enumerable. Remove the unnecessary materialization.");

    internal static readonly DiagnosticDescriptor HAW018 = CreateReliabilityWarning(
        "HAW018",
        "Repeated enumeration",
        "Enumerable '{0}' is consumed {1} times without materialization. Materialize once or avoid repeated enumeration.");

    internal static readonly DiagnosticDescriptor HAW020 = CreateArchitectureWarning(
        "HAW020",
        "Boilerplate repository layer",
        "Repository '{0}' forwards {1:P0} of its eligible methods directly to '{2}'. Remove the layer or add domain behavior that justifies it.");

    internal static readonly DiagnosticDescriptor HAW021 = CreateReliabilityWarning(
        "HAW021",
        "Excessive try/catch",
        "Excessive try/catch: {0}");

    internal static readonly DiagnosticDescriptor HAW023 = CreateArchitectureWarning(
        "HAW023",
        "Dead extension point",
        "Extension point '{0}' has no source consumer. Remove it or add the demonstrated extension behavior that requires it.");

    internal static readonly DiagnosticDescriptor HAW024 = CreateReliabilityWarning(
        "HAW024",
        "Unused cancellation token plumbing",
        "Cancellation token '{0}' is not used or forwarded to '{1}'. Propagate it to the cancellable operation or remove it from this boundary.");

    internal static readonly DiagnosticDescriptor HAW025 = CreateArchitectureWarning(
        "HAW025",
        "Configuration pass-through",
        "Configuration value '{0}' is forwarded unchanged across {1} source boundaries. Consume, validate, or remove the needless configuration layer.");

    internal static readonly DiagnosticDescriptor HAW029 = CreateArchitectureWarning(
        "HAW029",
        "Excessive logging noise",
        "Type '{0}' emits lifecycle logs in {1:P0} of its eligible methods. Keep logging for operationally meaningful events.");

    internal static readonly DiagnosticDescriptor HAW030 = CreateArchitectureWarning(
        "HAW030",
        "Copy-paste near duplication",
        "Method '{0}' belongs to a cluster of {1} structurally similar methods. Consolidate shared behavior when the differences do not justify duplication.");

    internal static readonly DiagnosticDescriptor HAW100 = new(
        "HAW100",
        "Abstraction density",
        "Abstraction density is {0:F2}: {1} abstraction types and {2} behavioral types",
        HealthCategory,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Reports the ratio of source-defined abstraction-role types to behavioral types.",
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd, NonSuppressibleTag });

    internal static readonly DiagnosticDescriptor HAW101 = CreateNonSuppressibleComplexityWarning(
        "HAW101",
        "Cyclomatic complexity",
        "Method '{0}' has cyclomatic complexity {1}; maximum allowed is {2}. Split branches into smaller methods or simplify the control flow.");

    internal static readonly DiagnosticDescriptor HAW102 = CreateNonSuppressibleComplexityWarning(
        "HAW102",
        "Cognitive complexity",
        "Method '{0}' has cognitive complexity {1}; maximum allowed is {2}. Flatten nesting or extract a focused method.");

    internal static readonly DiagnosticDescriptor HAW103 = CreateNonSuppressibleComplexityWarning(
        "HAW103",
        "Maximum nesting depth",
        "Method '{0}' reaches nesting depth {1}; maximum allowed is {2}. Use guard clauses or extract the nested work.");

    internal static readonly DiagnosticDescriptor HAW104 = CreateNonSuppressibleComplexityWarning(
        "HAW104",
        "Class coupling",
        "Type '{0}' depends on {1} distinct external types; maximum allowed is {2}. API surface: {3}; state/dependency: {4}; implementation: {5}. Examples: {6}. Split responsibilities or introduce a focused boundary.");

    internal static readonly DiagnosticDescriptor HAW105 = CreateNonSuppressibleComplexityWarning(
        "HAW105",
        "Method length",
        "Method length exceeds the configured limit: {0}. Split the method into focused operations.");

    internal static readonly DiagnosticDescriptor HAW106 = new(
        "HAW106",
        "Multi-statement method on one line",
        "Method '{0}' has multiple statements on one line; put the body on separate lines",
        FormattingCategory,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A block-bodied method with multiple executable statements must span multiple physical lines.",
        customTags: new[] { NonSuppressibleTag });

    internal static readonly DiagnosticDescriptor HAW900 = new(
        "HAW900",
        "Invalid Hawthorne configuration",
        "Invalid configuration: {0}. Correct hawthorne.json before running normal Hawthorne analysis.",
        ConfigurationCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Hawthorne configuration must be valid before normal analyzer diagnostics are produced.",
        customTags: new[]
        {
            WellKnownDiagnosticTags.CompilationEnd,
            WellKnownDiagnosticTags.NotConfigurable,
            NonSuppressibleTag,
        });

    internal static readonly DiagnosticDescriptor HAW901 = new(
        "HAW901",
        "Invalid Hawthorne suppression",
        "Hawthorne suppression '{0}' is invalid: {1}",
        ConfigurationCategory,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Hawthorne diagnostics may be suppressed only with a non-empty justification when the selected rule permits suppression.",
        customTags: new[] { NonSuppressibleTag });

    internal static ImmutableArray<DiagnosticDescriptor> All { get; } = ImmutableArray.Create(
        HAW001, HAW002, HAW003, HAW004, HAW005, HAW006, HAW007, HAW008, HAW010, HAW011,
        HAW013, HAW014, HAW015, HAW016, HAW017, HAW018, HAW020, HAW021, HAW023, HAW024,
        HAW025, HAW029, HAW030, HAW100, HAW101, HAW102, HAW103, HAW104, HAW105, HAW106,
        HAW900, HAW901);

    internal static ImmutableHashSet<string> NonSuppressibleRuleIds { get; } = All
        .Where(descriptor => descriptor.CustomTags.Contains(NonSuppressibleTag))
        .Select(descriptor => descriptor.Id)
        .ToImmutableHashSet(StringComparer.Ordinal);

    private static DiagnosticDescriptor CreateArchitectureWarning(string id, string title, string messageFormat) =>
        new(id, title, messageFormat, ArchitectureCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static DiagnosticDescriptor CreateComplexityWarning(string id, string title, string messageFormat) =>
        new(id, title, messageFormat, ComplexityCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static DiagnosticDescriptor CreateNonSuppressibleComplexityWarning(string id, string title, string messageFormat) =>
        new(
            id,
            title,
            messageFormat,
            ComplexityCategory,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            customTags: new[] { NonSuppressibleTag });

    private static DiagnosticDescriptor CreateReliabilityWarning(string id, string title, string messageFormat) =>
        new(id, title, messageFormat, ReliabilityCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true);
}
