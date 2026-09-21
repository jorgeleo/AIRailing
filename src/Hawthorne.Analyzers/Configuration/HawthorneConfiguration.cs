using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Hawthorne.Analyzers.Configuration;

internal sealed class HawthorneConfiguration
{
    internal const string FileName = "hawthorne.json";

    private HawthorneConfiguration(
        ImmutableDictionary<string, HawthorneRuleConfiguration> rules,
        Hawthorne105Configuration methodLength,
        int maximumNestingDepth,
        int maximumCyclomaticComplexity,
        int maximumCognitiveComplexity,
        bool requireMutableSingletonState, int maximumClassCoupling)
    {
        Rules = rules;
        MethodLength = methodLength;
        MaximumNestingDepth = maximumNestingDepth;
        MaximumCyclomaticComplexity = maximumCyclomaticComplexity;
        MaximumCognitiveComplexity = maximumCognitiveComplexity;
        RequireMutableSingletonState = requireMutableSingletonState;
        MaximumClassCoupling = maximumClassCoupling;
    }

    internal ImmutableDictionary<string, HawthorneRuleConfiguration> Rules { get; }

    internal Hawthorne105Configuration MethodLength { get; }
    internal int MaximumNestingDepth { get; }
    internal int MaximumCyclomaticComplexity { get; }
    internal int MaximumCognitiveComplexity { get; }
    internal bool RequireMutableSingletonState { get; }
    internal int MaximumClassCoupling { get; }

    internal HawthorneRuleConfiguration GetRule(string diagnosticId) => Rules[diagnosticId];

    internal HawthorneConfiguration WithRule(string diagnosticId, HawthorneRuleConfiguration rule) =>
        new(Rules.SetItem(diagnosticId, rule), MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling);

    internal HawthorneConfiguration WithMethodLength(Hawthorne105Configuration methodLength) =>
        new(Rules, methodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling);
    internal HawthorneConfiguration WithMaximumNestingDepth(int maximum) =>
        new(Rules, MethodLength, maximum, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling);
    internal HawthorneConfiguration WithMaximumCyclomaticComplexity(int maximum) =>
        new(Rules, MethodLength, MaximumNestingDepth, maximum, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling);
    internal HawthorneConfiguration WithMaximumCognitiveComplexity(int maximum) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, maximum, RequireMutableSingletonState, MaximumClassCoupling);
    internal HawthorneConfiguration WithRequireMutableSingletonState(bool value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, value, MaximumClassCoupling);
    internal HawthorneConfiguration WithMaximumClassCoupling(int value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, value);

    internal static HawthorneConfiguration CreateDefaults(IEnumerable<string> diagnosticIds) =>
        new(diagnosticIds.ToImmutableDictionary(
            diagnosticId => diagnosticId,
            diagnosticId => diagnosticId == "HAW106"
                ? HawthorneRuleConfiguration.FormattingDefault
                : HawthorneRuleConfiguration.Default,
            StringComparer.Ordinal), Hawthorne105Configuration.Default, 4, 10, 15, true, 12);
}

internal sealed class Hawthorne105Configuration
{
    internal static Hawthorne105Configuration Default { get; } = new(30, 50);

    internal Hawthorne105Configuration(int maximumExecutableStatements, int maximumPhysicalLines)
    {
        MaximumExecutableStatements = maximumExecutableStatements;
        MaximumPhysicalLines = maximumPhysicalLines;
    }

    internal int MaximumExecutableStatements { get; }
    internal int MaximumPhysicalLines { get; }
}

internal sealed class HawthorneRuleConfiguration
{
    internal HawthorneRuleConfiguration(bool isEnabled, DiagnosticSeverity severity)
    {
        IsEnabled = isEnabled;
        Severity = severity;
    }

    internal bool IsEnabled { get; }

    internal DiagnosticSeverity Severity { get; }

    internal static HawthorneRuleConfiguration Default { get; } = new(true, DiagnosticSeverity.Warning);

    internal static HawthorneRuleConfiguration FormattingDefault { get; } = new(false, DiagnosticSeverity.Warning);
}

internal sealed class HawthorneConfigurationLoadResult
{
    private HawthorneConfigurationLoadResult(HawthorneConfiguration? configuration, string? errorMessage, AdditionalText? source)
    {
        Configuration = configuration;
        ErrorMessage = errorMessage;
        Source = source;
    }

    internal HawthorneConfiguration? Configuration { get; }

    internal string? ErrorMessage { get; }

    internal AdditionalText? Source { get; }

    internal bool IsValid => Configuration is not null;

    internal static HawthorneConfigurationLoadResult Valid(HawthorneConfiguration configuration, AdditionalText? source = null) => new(configuration, null, source);

    internal static HawthorneConfigurationLoadResult Invalid(string errorMessage, AdditionalText? source = null) => new(null, errorMessage, source);
}
