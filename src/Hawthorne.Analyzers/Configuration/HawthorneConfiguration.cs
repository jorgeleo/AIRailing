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
    internal Hawthorne006Configuration PrematureGeneralization => NextRules.PrematureGeneralization;
    internal Hawthorne007Configuration ConstructorDependencies => NextRules.ConstructorDependencies;
    internal Hawthorne008Configuration BooleanControlFlow => NextRules.BooleanControlFlow;
    internal Hawthorne010Configuration OneMethodServices => NextRules.OneMethodServices;
    internal Hawthorne011Configuration ExcessiveMicroMethods => NextRules.ExcessiveMicroMethods;
    internal Hawthorne013Configuration ExceptionLaundering => NextRules.ExceptionLaundering;
    internal Hawthorne014Configuration DefensiveNullChecking => NextRules.DefensiveNullChecking;
    internal Hawthorne016Configuration FakeAsync => NextRules.FakeAsync;
    internal Hawthorne017Configuration UnnecessaryLinqMaterialization => NextRules.UnnecessaryLinqMaterialization;
    internal Hawthorne018Configuration RepeatedEnumeration => NextRules.RepeatedEnumeration;
    internal Hawthorne021Configuration ExcessiveTryCatch => NextRules.ExcessiveTryCatch;
    internal Hawthorne024Configuration CancellationTokens => NextRules.CancellationTokens;

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
    internal HawthorneConfiguration WithPrematureGeneralization(Hawthorne006Configuration value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules.WithPrematureGeneralization(value));
    internal HawthorneConfiguration WithConstructorDependencies(Hawthorne007Configuration value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules.WithConstructorDependencies(value));
    internal HawthorneConfiguration WithBooleanControlFlow(Hawthorne008Configuration value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules.WithBooleanControlFlow(value));
    internal HawthorneConfiguration WithOneMethodServices(Hawthorne010Configuration value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules.WithOneMethodServices(value));
    internal HawthorneConfiguration WithExcessiveMicroMethods(Hawthorne011Configuration value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules.WithExcessiveMicroMethods(value));
    internal HawthorneConfiguration WithExceptionLaundering(Hawthorne013Configuration value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules.WithExceptionLaundering(value));
    internal HawthorneConfiguration WithDefensiveNullChecking(Hawthorne014Configuration value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules.WithDefensiveNullChecking(value));
    internal HawthorneConfiguration WithFakeAsync(Hawthorne016Configuration value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules.WithFakeAsync(value));
    internal HawthorneConfiguration WithUnnecessaryLinqMaterialization(Hawthorne017Configuration value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules.WithUnnecessaryLinqMaterialization(value));
    internal HawthorneConfiguration WithRepeatedEnumeration(Hawthorne018Configuration value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules.WithRepeatedEnumeration(value));
    internal HawthorneConfiguration WithExcessiveTryCatch(Hawthorne021Configuration value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules.WithExcessiveTryCatch(value));
    internal HawthorneConfiguration WithCancellationTokens(Hawthorne024Configuration value) =>
        new(Rules, MethodLength, MaximumNestingDepth, MaximumCyclomaticComplexity, MaximumCognitiveComplexity, RequireMutableSingletonState, MaximumClassCoupling, NextRules.WithCancellationTokens(value));

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
        Hawthorne006Configuration.Default,
        Hawthorne007Configuration.Default,
        Hawthorne008Configuration.Default,
        Hawthorne010Configuration.Default,
        Hawthorne011Configuration.Default,
        Hawthorne013Configuration.Default,
        Hawthorne014Configuration.Default,
        Hawthorne016Configuration.Default,
        Hawthorne017Configuration.Default,
        Hawthorne021Configuration.Default,
        Hawthorne024Configuration.Default,
        Hawthorne018Configuration.Default);

    internal HawthorneNextRuleConfiguration(
        Hawthorne005Configuration wrapper,
        Hawthorne006Configuration prematureGeneralization,
        Hawthorne007Configuration constructorDependencies,
        Hawthorne008Configuration booleanControlFlow,
        Hawthorne010Configuration oneMethodServices,
        Hawthorne011Configuration excessiveMicroMethods,
        Hawthorne013Configuration exceptionLaundering,
        Hawthorne014Configuration defensiveNullChecking,
        Hawthorne016Configuration fakeAsync,
        Hawthorne017Configuration unnecessaryLinqMaterialization,
        Hawthorne021Configuration excessiveTryCatch,
        Hawthorne024Configuration cancellationTokens,
        Hawthorne018Configuration? repeatedEnumeration = null)
    {
        Wrapper = wrapper;
        PrematureGeneralization = prematureGeneralization;
        ConstructorDependencies = constructorDependencies;
        BooleanControlFlow = booleanControlFlow;
        OneMethodServices = oneMethodServices;
        ExcessiveMicroMethods = excessiveMicroMethods;
        ExceptionLaundering = exceptionLaundering;
        DefensiveNullChecking = defensiveNullChecking;
        FakeAsync = fakeAsync;
        UnnecessaryLinqMaterialization = unnecessaryLinqMaterialization;
        RepeatedEnumeration = repeatedEnumeration ?? Hawthorne018Configuration.Default;
        ExcessiveTryCatch = excessiveTryCatch;
        CancellationTokens = cancellationTokens;
    }

    internal Hawthorne005Configuration Wrapper { get; }
    internal Hawthorne006Configuration PrematureGeneralization { get; }
    internal Hawthorne007Configuration ConstructorDependencies { get; }
    internal Hawthorne008Configuration BooleanControlFlow { get; }
    internal Hawthorne010Configuration OneMethodServices { get; }
    internal Hawthorne011Configuration ExcessiveMicroMethods { get; }
    internal Hawthorne013Configuration ExceptionLaundering { get; }
    internal Hawthorne014Configuration DefensiveNullChecking { get; }
    internal Hawthorne016Configuration FakeAsync { get; }
    internal Hawthorne017Configuration UnnecessaryLinqMaterialization { get; }
    internal Hawthorne018Configuration RepeatedEnumeration { get; }
    internal Hawthorne021Configuration ExcessiveTryCatch { get; }
    internal Hawthorne024Configuration CancellationTokens { get; }

    internal HawthorneNextRuleConfiguration WithWrapper(Hawthorne005Configuration value) =>
        new(value, PrematureGeneralization, ConstructorDependencies, BooleanControlFlow, OneMethodServices, ExcessiveMicroMethods, ExceptionLaundering, DefensiveNullChecking, FakeAsync, UnnecessaryLinqMaterialization, ExcessiveTryCatch, CancellationTokens);

    internal HawthorneNextRuleConfiguration WithPrematureGeneralization(Hawthorne006Configuration value) =>
        new(Wrapper, value, ConstructorDependencies, BooleanControlFlow, OneMethodServices, ExcessiveMicroMethods, ExceptionLaundering, DefensiveNullChecking, FakeAsync, UnnecessaryLinqMaterialization, ExcessiveTryCatch, CancellationTokens);

    internal HawthorneNextRuleConfiguration WithConstructorDependencies(Hawthorne007Configuration value) =>
        new(Wrapper, PrematureGeneralization, value, BooleanControlFlow, OneMethodServices, ExcessiveMicroMethods, ExceptionLaundering, DefensiveNullChecking, FakeAsync, UnnecessaryLinqMaterialization, ExcessiveTryCatch, CancellationTokens);

    internal HawthorneNextRuleConfiguration WithBooleanControlFlow(Hawthorne008Configuration value) =>
        new(Wrapper, PrematureGeneralization, ConstructorDependencies, value, OneMethodServices, ExcessiveMicroMethods, ExceptionLaundering, DefensiveNullChecking, FakeAsync, UnnecessaryLinqMaterialization, ExcessiveTryCatch, CancellationTokens);

    internal HawthorneNextRuleConfiguration WithOneMethodServices(Hawthorne010Configuration value) =>
        new(Wrapper, PrematureGeneralization, ConstructorDependencies, BooleanControlFlow, value, ExcessiveMicroMethods, ExceptionLaundering, DefensiveNullChecking, FakeAsync, UnnecessaryLinqMaterialization, ExcessiveTryCatch, CancellationTokens);

    internal HawthorneNextRuleConfiguration WithExcessiveMicroMethods(Hawthorne011Configuration value) =>
        new(Wrapper, PrematureGeneralization, ConstructorDependencies, BooleanControlFlow, OneMethodServices, value, ExceptionLaundering, DefensiveNullChecking, FakeAsync, UnnecessaryLinqMaterialization, ExcessiveTryCatch, CancellationTokens);

    internal HawthorneNextRuleConfiguration WithExceptionLaundering(Hawthorne013Configuration value) =>
        new(Wrapper, PrematureGeneralization, ConstructorDependencies, BooleanControlFlow, OneMethodServices, ExcessiveMicroMethods, value, DefensiveNullChecking, FakeAsync, UnnecessaryLinqMaterialization, ExcessiveTryCatch, CancellationTokens);

    internal HawthorneNextRuleConfiguration WithDefensiveNullChecking(Hawthorne014Configuration value) =>
        new(Wrapper, PrematureGeneralization, ConstructorDependencies, BooleanControlFlow, OneMethodServices, ExcessiveMicroMethods, ExceptionLaundering, value, FakeAsync, UnnecessaryLinqMaterialization, ExcessiveTryCatch, CancellationTokens);

    internal HawthorneNextRuleConfiguration WithFakeAsync(Hawthorne016Configuration value) =>
        new(Wrapper, PrematureGeneralization, ConstructorDependencies, BooleanControlFlow, OneMethodServices, ExcessiveMicroMethods, ExceptionLaundering, DefensiveNullChecking, value, UnnecessaryLinqMaterialization, ExcessiveTryCatch, CancellationTokens);

    internal HawthorneNextRuleConfiguration WithUnnecessaryLinqMaterialization(Hawthorne017Configuration value) =>
        new(Wrapper, PrematureGeneralization, ConstructorDependencies, BooleanControlFlow, OneMethodServices, ExcessiveMicroMethods, ExceptionLaundering, DefensiveNullChecking, FakeAsync, value, ExcessiveTryCatch, CancellationTokens);

    internal HawthorneNextRuleConfiguration WithRepeatedEnumeration(Hawthorne018Configuration value) =>
        new(Wrapper, PrematureGeneralization, ConstructorDependencies, BooleanControlFlow, OneMethodServices, ExcessiveMicroMethods, ExceptionLaundering, DefensiveNullChecking, FakeAsync, UnnecessaryLinqMaterialization, ExcessiveTryCatch, CancellationTokens, value);

    internal HawthorneNextRuleConfiguration WithExcessiveTryCatch(Hawthorne021Configuration value) =>
        new(Wrapper, PrematureGeneralization, ConstructorDependencies, BooleanControlFlow, OneMethodServices, ExcessiveMicroMethods, ExceptionLaundering, DefensiveNullChecking, FakeAsync, UnnecessaryLinqMaterialization, value, CancellationTokens);

    internal HawthorneNextRuleConfiguration WithCancellationTokens(Hawthorne024Configuration value) =>
        new(Wrapper, PrematureGeneralization, ConstructorDependencies, BooleanControlFlow, OneMethodServices, ExcessiveMicroMethods, ExceptionLaundering, DefensiveNullChecking, FakeAsync, UnnecessaryLinqMaterialization, ExcessiveTryCatch, value);
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

internal sealed class Hawthorne006Configuration
{
    internal static Hawthorne006Configuration Default { get; } = new(
        maximumSharedExecutableStatements: 2,
        minimumDerivedTypes: 1,
        analyzeSingleClosedGenericUse: true);

    internal Hawthorne006Configuration(
        int maximumSharedExecutableStatements,
        int minimumDerivedTypes,
        bool analyzeSingleClosedGenericUse)
    {
        MaximumSharedExecutableStatements = maximumSharedExecutableStatements;
        MinimumDerivedTypes = minimumDerivedTypes;
        AnalyzeSingleClosedGenericUse = analyzeSingleClosedGenericUse;
    }

    internal int MaximumSharedExecutableStatements { get; }
    internal int MinimumDerivedTypes { get; }
    internal bool AnalyzeSingleClosedGenericUse { get; }
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

internal sealed class Hawthorne010Configuration
{
    internal static Hawthorne010Configuration Default { get; } = new(
        serviceSuffixes: ImmutableArray.Create("Service", "Handler", "Processor", "Validator"),
        maximumPhysicalLines: 25,
        maximumCyclomaticComplexity: 2,
        maximumSourceCallers: 1);

    internal Hawthorne010Configuration(
        ImmutableArray<string> serviceSuffixes,
        int maximumPhysicalLines,
        int maximumCyclomaticComplexity,
        int maximumSourceCallers)
    {
        ServiceSuffixes = serviceSuffixes;
        MaximumPhysicalLines = maximumPhysicalLines;
        MaximumCyclomaticComplexity = maximumCyclomaticComplexity;
        MaximumSourceCallers = maximumSourceCallers;
    }

    internal ImmutableArray<string> ServiceSuffixes { get; }
    internal int MaximumPhysicalLines { get; }
    internal int MaximumCyclomaticComplexity { get; }
    internal int MaximumSourceCallers { get; }

    internal bool HasServiceSuffix(string typeName) =>
        ServiceSuffixes.Any(suffix =>
            typeName.Length > suffix.Length && typeName.EndsWith(suffix, StringComparison.Ordinal));
}

internal sealed class Hawthorne011Configuration
{
    internal static Hawthorne011Configuration Default { get; } = new(
        maxTinyMethodRatio: 0.60,
        tinyMethodStatementLimit: 2,
        minimumMethodCount: 8);

    internal Hawthorne011Configuration(
        double maxTinyMethodRatio,
        int tinyMethodStatementLimit,
        int minimumMethodCount)
    {
        MaxTinyMethodRatio = maxTinyMethodRatio;
        TinyMethodStatementLimit = tinyMethodStatementLimit;
        MinimumMethodCount = minimumMethodCount;
    }

    internal double MaxTinyMethodRatio { get; }
    internal int TinyMethodStatementLimit { get; }
    internal int MinimumMethodCount { get; }
}

internal sealed class Hawthorne013Configuration
{
    internal static Hawthorne013Configuration Default { get; } = new(
        genericExceptionTypes: ImmutableArray.Create("System.Exception", "System.ApplicationException"),
        reportLogAndRethrow: false);

    internal Hawthorne013Configuration(
        ImmutableArray<string> genericExceptionTypes,
        bool reportLogAndRethrow)
    {
        GenericExceptionTypes = genericExceptionTypes;
        ReportLogAndRethrow = reportLogAndRethrow;
    }

    internal ImmutableArray<string> GenericExceptionTypes { get; }
    internal bool ReportLogAndRethrow { get; }

    internal bool IsGenericException(ITypeSymbol type) =>
        GenericExceptionTypes.Contains(type.ToDisplayString(), StringComparer.Ordinal);
}

internal sealed class Hawthorne014Configuration
{
    internal static Hawthorne014Configuration Default { get; } = new(includeInternalMethods: false);

    internal Hawthorne014Configuration(bool includeInternalMethods) =>
        IncludeInternalMethods = includeInternalMethods;

    internal bool IncludeInternalMethods { get; }
}

internal sealed class Hawthorne016Configuration
{
    internal static Hawthorne016Configuration Default { get; } = new(
        ignoreContractMethods: true,
        reportTrivialTaskRun: false);

    internal Hawthorne016Configuration(bool ignoreContractMethods, bool reportTrivialTaskRun)
    {
        IgnoreContractMethods = ignoreContractMethods;
        ReportTrivialTaskRun = reportTrivialTaskRun;
    }

    internal bool IgnoreContractMethods { get; }
    internal bool ReportTrivialTaskRun { get; }
}

internal sealed class Hawthorne017Configuration
{
    internal static Hawthorne017Configuration Default { get; } = new(analyzeToList: true, analyzeToArray: true);

    internal Hawthorne017Configuration(bool analyzeToList, bool analyzeToArray)
    {
        AnalyzeToList = analyzeToList;
        AnalyzeToArray = analyzeToArray;
    }

    internal bool AnalyzeToList { get; }
    internal bool AnalyzeToArray { get; }
}

internal sealed class Hawthorne018Configuration
{
    internal static Hawthorne018Configuration Default { get; } = new(minimumEnumerations: 2);

    internal Hawthorne018Configuration(int minimumEnumerations) =>
        MinimumEnumerations = minimumEnumerations;

    internal int MinimumEnumerations { get; }
}

internal sealed class Hawthorne021Configuration
{
    internal static Hawthorne021Configuration Default { get; } = new(
        minimumMethodCount: 5,
        maximumTryBlocksPerMethod: 0.75,
        reportCatchAllDefaultReturn: true);

    internal Hawthorne021Configuration(
        int minimumMethodCount,
        double maximumTryBlocksPerMethod,
        bool reportCatchAllDefaultReturn)
    {
        MinimumMethodCount = minimumMethodCount;
        MaximumTryBlocksPerMethod = maximumTryBlocksPerMethod;
        ReportCatchAllDefaultReturn = reportCatchAllDefaultReturn;
    }

    internal int MinimumMethodCount { get; }
    internal double MaximumTryBlocksPerMethod { get; }
    internal bool ReportCatchAllDefaultReturn { get; }
}

internal sealed class Hawthorne024Configuration
{
    internal static Hawthorne024Configuration Default { get; } = new(
        reportMissingForwarding: true,
        treatNoneAsMissingForwarding: true,
        ignoreContractMethods: true);

    internal Hawthorne024Configuration(
        bool reportMissingForwarding,
        bool treatNoneAsMissingForwarding,
        bool ignoreContractMethods)
    {
        ReportMissingForwarding = reportMissingForwarding;
        TreatNoneAsMissingForwarding = treatNoneAsMissingForwarding;
        IgnoreContractMethods = ignoreContractMethods;
    }

    internal bool ReportMissingForwarding { get; }
    internal bool TreatNoneAsMissingForwarding { get; }
    internal bool IgnoreContractMethods { get; }
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
