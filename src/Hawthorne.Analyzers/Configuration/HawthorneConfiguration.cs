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
        bool requireMutableSingletonState,
        int maximumClassCoupling,
        HawthorneNextRuleConfiguration nextRules)
    {
        Rules = rules;
        MethodLength = methodLength;
        MaximumNestingDepth = maximumNestingDepth;
        MaximumCyclomaticComplexity = maximumCyclomaticComplexity;
        MaximumCognitiveComplexity = maximumCognitiveComplexity;
        RequireMutableSingletonState = requireMutableSingletonState;
        MaximumClassCoupling = maximumClassCoupling;
        NextRules = nextRules;
    }

    internal ImmutableDictionary<string, HawthorneRuleConfiguration> Rules { get; }

    internal Hawthorne105Configuration MethodLength { get; }
    internal int MaximumNestingDepth { get; }
    internal int MaximumCyclomaticComplexity { get; }
    internal int MaximumCognitiveComplexity { get; }
    internal bool RequireMutableSingletonState { get; }
    internal int MaximumClassCoupling { get; }
    private HawthorneNextRuleConfiguration NextRules { get; }

    internal Hawthorne005Configuration Wrapper => NextRules.Wrapper;
    internal Hawthorne007Configuration ConstructorDependencies => NextRules.ConstructorDependencies;
    internal Hawthorne008Configuration BooleanControlFlow => NextRules.BooleanControlFlow;

    internal HawthorneRuleConfiguration GetRule(string diagnosticId) => Rules[diagnosticId];

    internal HawthorneConfiguration WithRule(string diagnosticId, HawthorneRuleConfiguration rule) =>
        new(Rules.SetItem(diagnosticId, rule), MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules);

    internal HawthorneConfiguration WithMethodLength(Hawthorne105Configuration methodLength) =>
        new(Rules, methodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules);
    internal HawthorneConfiguration WithMaximumNestingDepth(int maximum) =>
        new(Rules, MethodLength, maximum, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules);
    internal HawthorneConfiguration WithMaximumCyclomaticComplexity(int maximum) =>
        new(Rules, MethodLength, MaximumNestingDepth, maximum, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules);
    internal HawthorneConfiguration WithMaximumCognitiveComplexity(int maximum) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, maximum, RequireMutableSingletonState, MaximumClassCoupling, NextRules);
    internal HawthorneConfiguration WithRequireMutableSingletonState(bool value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, value, MaximumClassCoupling, NextRules);
    internal HawthorneConfiguration WithMaximumClassCoupling(int value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, value, NextRules);
    internal HawthorneConfiguration WithWrapper(Hawthorne005Configuration value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules.WithWrapper(value));
    internal HawthorneConfiguration WithConstructorDependencies(Hawthorne007Configuration value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules.WithConstructorDependencies(value));
    internal HawthorneConfiguration WithBooleanControlFlow(Hawthorne008Configuration value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules.WithBooleanControlFlow(value));

    internal static HawthorneConfiguration CreateDefaults(IEnumerable<string> diagnosticIds) =>
        new(diagnosticIds.ToImmutableDictionary(
            diagnosticId => diagnosticId,
            GetDefaultRuleConfiguration,
            StringComparer.Ordinal), Hawthorne105Configuration.Default, 4, 10, 15, true, 12, HawthorneNextRuleConfiguration.Default);

    private static HawthorneRuleConfiguration GetDefaultRuleConfiguration(string diagnosticId) => diagnosticId switch
    {
        "HAW106" => HawthorneRuleConfiguration.FormattingDefault,
        "HAW015" or "HAW020" or "HAW023" or "HAW025" or "HAW029" or "HAW030" => HawthorneRuleConfiguration.OptInDefault,
        "HAW100" => HawthorneRuleConfiguration.HealthDefault,
        _ => HawthorneRuleConfiguration.Default,
    };
}

internal sealed class HawthorneNextRuleConfiguration
{
    internal static HawthorneNextRuleConfiguration Default { get; } = new(
        Hawthorne005Configuration.Default,
        Hawthorne007Configuration.Default,
        Hawthorne008Configuration.Default);

    internal HawthorneNextRuleConfiguration(
        Hawthorne005Configuration wrapper,
        Hawthorne007Configuration constructorDependencies,
        Hawthorne008Configuration booleanControlFlow)
    {
        Wrapper = wrapper;
        ConstructorDependencies = constructorDependencies;
        BooleanControlFlow = booleanControlFlow;
    }

    internal Hawthorne005Configuration Wrapper { get; }
    internal Hawthorne007Configuration ConstructorDependencies { get; }
    internal Hawthorne008Configuration BooleanControlFlow { get; }

    internal HawthorneNextRuleConfiguration WithWrapper(Hawthorne005Configuration value) =>
        new(value, ConstructorDependencies, BooleanControlFlow);

    internal HawthorneNextRuleConfiguration WithConstructorDependencies(Hawthorne007Configuration value) =>
        new(Wrapper, value, BooleanControlFlow);

    internal HawthorneNextRuleConfiguration WithBooleanControlFlow(Hawthorne008Configuration value) =>
        new(Wrapper, ConstructorDependencies, value);
}

internal sealed class Hawthorne005Configuration
{
    internal static Hawthorne005Configuration Default { get; } = new(
        minimumForwardingMethods: 3,
        minimumForwardingRatio: 0.80,
        requireRoleSuffix: true,
        roleSuffixes: ImmutableArray.Create("Wrapper", "Adapter", "Decorator", "Facade", "Client"));

    internal Hawthorne005Configuration(
        int minimumForwardingMethods,
        double minimumForwardingRatio,
        bool requireRoleSuffix,
        ImmutableArray<string> roleSuffixes)
    {
        MinimumForwardingMethods = minimumForwardingMethods;
        MinimumForwardingRatio = minimumForwardingRatio;
        RequireRoleSuffix = requireRoleSuffix;
        RoleSuffixes = roleSuffixes;
    }

    internal int MinimumForwardingMethods { get; }
    internal double MinimumForwardingRatio { get; }
    internal bool RequireRoleSuffix { get; }
    internal ImmutableArray<string> RoleSuffixes { get; }

    internal bool HasConfiguredRoleSuffix(string typeName) =>
        RoleSuffixes.Any(suffix => typeName.EndsWith(suffix, StringComparison.Ordinal));
}

internal sealed class Hawthorne007Configuration
{
    internal static Hawthorne007Configuration Default { get; } = new(
        maximumDependencies: 7,
        excludeOptionWrappers: true,
        configurationTypeSuffixes: ImmutableArray.Create("Options", "Settings", "Configuration"));

    internal Hawthorne007Configuration(
        int maximumDependencies,
        bool excludeOptionWrappers,
        ImmutableArray<string> configurationTypeSuffixes)
    {
        MaximumDependencies = maximumDependencies;
        ExcludeOptionWrappers = excludeOptionWrappers;
        ConfigurationTypeSuffixes = configurationTypeSuffixes;
    }

    internal int MaximumDependencies { get; }
    internal bool ExcludeOptionWrappers { get; }
    internal ImmutableArray<string> ConfigurationTypeSuffixes { get; }

    internal bool IsConfigurationType(string typeName) =>
        ConfigurationTypeSuffixes.Any(suffix => typeName.EndsWith(suffix, StringComparison.Ordinal));
}

internal sealed class Hawthorne008Configuration
{
    internal static Hawthorne008Configuration Default { get; } = new(
        warningParameterCount: 2,
        errorParameterCount: 3,
        requireDirectControlFlowUse: true);

    internal Hawthorne008Configuration(
        int warningParameterCount,
        int errorParameterCount,
        bool requireDirectControlFlowUse)
    {
        WarningParameterCount = warningParameterCount;
        ErrorParameterCount = errorParameterCount;
        RequireDirectControlFlowUse = requireDirectControlFlowUse;
    }

    internal int WarningParameterCount { get; }
    internal int ErrorParameterCount { get; }
    internal bool RequireDirectControlFlowUse { get; }
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
    internal HawthorneRuleConfiguration(bool isEnabled, DiagnosticSeverity severity, bool isSeverityConfigured = false)
    {
        IsEnabled = isEnabled;
        Severity = severity;
        IsSeverityConfigured = isSeverityConfigured;
    }

    internal bool IsEnabled { get; }

    internal DiagnosticSeverity Severity { get; }
    internal bool IsSeverityConfigured { get; }

    internal static HawthorneRuleConfiguration Default { get; } = new(true, DiagnosticSeverity.Warning);

    internal static HawthorneRuleConfiguration FormattingDefault { get; } = new(false, DiagnosticSeverity.Warning);

    internal static HawthorneRuleConfiguration OptInDefault { get; } = new(false, DiagnosticSeverity.Warning);

    internal static HawthorneRuleConfiguration HealthDefault { get; } = new(false, DiagnosticSeverity.Info);
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
