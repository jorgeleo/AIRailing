using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Hawthorne.Analyzers.Configuration;

internal sealed class HawthorneConfiguration
{
    internal const string FileName = "hawthorne.json";

    private HawthorneConfiguration(
        ImmutableDictionary<string, HawthorneRuleConfiguration> rules,
        ImmutableArray<HawthorneException> exceptions,
        string? projectDirectory,
        Hawthorne105Configuration methodLength,
        int maximumNestingDepth,
        int maximumCyclomaticComplexity,
        int maximumCognitiveComplexity,
        bool requireMutableSingletonState)
    {
        Rules = rules;
        Exceptions = exceptions;
        ProjectDirectory = projectDirectory;
        MethodLength = methodLength;
        MaximumNestingDepth = maximumNestingDepth;
        MaximumCyclomaticComplexity = maximumCyclomaticComplexity;
        MaximumCognitiveComplexity = maximumCognitiveComplexity;
        RequireMutableSingletonState = requireMutableSingletonState;
    }

    internal ImmutableDictionary<string, HawthorneRuleConfiguration> Rules { get; }

    internal ImmutableArray<HawthorneException> Exceptions { get; }

    internal string? ProjectDirectory { get; }

    internal Hawthorne105Configuration MethodLength { get; }
    internal int MaximumNestingDepth { get; }
    internal int MaximumCyclomaticComplexity { get; }
    internal int MaximumCognitiveComplexity { get; }
    internal bool RequireMutableSingletonState { get; }

    internal HawthorneRuleConfiguration GetRule(string diagnosticId) => Rules[diagnosticId];

    internal HawthorneConfiguration WithRule(string diagnosticId, HawthorneRuleConfiguration rule) =>
        new(Rules.SetItem(diagnosticId, rule), Exceptions, ProjectDirectory, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState);

    internal HawthorneConfiguration WithExceptions(ImmutableArray<HawthorneException> exceptions) =>
        new(Rules, exceptions, ProjectDirectory, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState);

    internal HawthorneConfiguration WithMethodLength(Hawthorne105Configuration methodLength) =>
        new(Rules, Exceptions, ProjectDirectory, methodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState);
    internal HawthorneConfiguration WithMaximumNestingDepth(int maximum) =>
        new(Rules, Exceptions, ProjectDirectory, MethodLength, maximum, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState);
    internal HawthorneConfiguration WithMaximumCyclomaticComplexity(int maximum) =>
        new(Rules, Exceptions, ProjectDirectory, MethodLength, MaximumNestingDepth, maximum, MaximumCognitiveComplexity, RequireMutableSingletonState);
    internal HawthorneConfiguration WithMaximumCognitiveComplexity(int maximum) =>
        new(Rules, Exceptions, ProjectDirectory, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, maximum, RequireMutableSingletonState);
    internal HawthorneConfiguration WithRequireMutableSingletonState(bool value) =>
        new(Rules, Exceptions, ProjectDirectory, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, value);

    internal static HawthorneConfiguration CreateDefaults(IEnumerable<string> diagnosticIds, string? projectDirectory = null) =>
        new(diagnosticIds.ToImmutableDictionary(
            diagnosticId => diagnosticId,
            _ => HawthorneRuleConfiguration.Default,
            StringComparer.Ordinal), ImmutableArray<HawthorneException>.Empty, projectDirectory, Hawthorne105Configuration.Default, 4, 10, 15, true);
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

internal sealed class HawthorneException
{
    internal HawthorneException(string file, ImmutableHashSet<string> rules, string reason)
    {
        File = file;
        Rules = rules;
        Reason = reason;
    }

    internal string File { get; }

    internal ImmutableHashSet<string> Rules { get; }

    internal string Reason { get; }
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
