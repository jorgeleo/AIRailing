using System.Collections.Immutable;
using System.Text.Json;
using Hawthorne.Analyzers.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Hawthorne.Analyzers.Configuration;

internal static class HawthorneConfigurationLoader
{
    internal static HawthorneConfigurationLoadResult Load(ImmutableArray<AdditionalText> additionalFiles)
    {
        var configurationFiles = additionalFiles
            .Where(file => string.Equals(PathNormalizer.GetFileName(file.Path), HawthorneConfiguration.FileName, StringComparison.Ordinal))
            .ToArray();

        if (configurationFiles.Length == 0)
        {
            return HawthorneConfigurationLoadResult.Valid(CreateDefaults());
        }

        if (configurationFiles.Length > 1)
        {
            return HawthorneConfigurationLoadResult.Invalid("Only one hawthorne.json may be supplied to a compilation.", configurationFiles[0]);
        }

        var text = configurationFiles[0].GetText()?.ToString();
        if (text is null || string.IsNullOrWhiteSpace(text))
        {
            return HawthorneConfigurationLoadResult.Invalid("hawthorne.json must contain a JSON object.", configurationFiles[0]);
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return HawthorneConfigurationLoadResult.Invalid("hawthorne.json must contain a JSON object.", configurationFiles[0]);
            }

            if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || version.GetInt32() != 1)
            {
                return HawthorneConfigurationLoadResult.Invalid("hawthorne.json.version must be the integer 1.", configurationFiles[0]);
            }

            if (root.TryGetProperty("exceptions", out _))
            {
                return HawthorneConfigurationLoadResult.Invalid(
                    "hawthorne.json.exceptions is no longer supported; use SuppressMessageAttribute with a non-empty Justification.",
                    configurationFiles[0]);
            }

            var configuration = CreateDefaults();
            if (!root.TryGetProperty("rules", out var rules))
            {
                return HawthorneConfigurationLoadResult.Valid(configuration, configurationFiles[0]);
            }

            if (rules.ValueKind != JsonValueKind.Object)
            {
                return HawthorneConfigurationLoadResult.Invalid("hawthorne.json.rules must be a JSON object.", configurationFiles[0]);
            }

            foreach (var rule in rules.EnumerateObject())
            {
                if (rule.Name is "_comment" or "_potentialFix")
                {
                    continue;
                }

                if (!configuration.Rules.ContainsKey(rule.Name))
                {
                    return HawthorneConfigurationLoadResult.Invalid($"hawthorne.json.rules contains unknown rule '{rule.Name}'.", configurationFiles[0]);
                }

                if (rule.Value.ValueKind != JsonValueKind.Object)
                {
                    return HawthorneConfigurationLoadResult.Invalid($"hawthorne.json.rules.{rule.Name} must be a JSON object.", configurationFiles[0]);
                }

                var defaultRule = configuration.GetRule(rule.Name);
                var enabled = GetEnabled(rule.Name, rule.Value, defaultRule.IsEnabled, out var enabledError);
                if (enabledError is not null)
                {
                    return HawthorneConfigurationLoadResult.Invalid(enabledError, configurationFiles[0]);
                }

                var severity = GetSeverity(
                    rule.Name,
                    rule.Value,
                    defaultRule.Severity,
                    out var isSeverityConfigured,
                    out var severityError);
                if (severityError is not null)
                {
                    return HawthorneConfigurationLoadResult.Invalid(severityError, configurationFiles[0]);
                }

                configuration = configuration.WithRule(rule.Name, new HawthorneRuleConfiguration(
                    enabled,
                    severity,
                    isSeverityConfigured));
                if (rule.Name == "HAW005")
                {
                    var wrapper = GetWrapper(rule.Name, rule.Value, configuration.Wrapper, out var wrapperError);
                    if (wrapperError is not null)
                    {
                        return HawthorneConfigurationLoadResult.Invalid(wrapperError, configurationFiles[0]);
                    }

                    configuration = configuration.WithWrapper(wrapper);
                }
                if (rule.Name == "HAW006")
                {
                    var prematureGeneralization = GetPrematureGeneralization(
                        rule.Name,
                        rule.Value,
                        configuration.PrematureGeneralization,
                        out var prematureGeneralizationError);
                    if (prematureGeneralizationError is not null)
                    {
                        return HawthorneConfigurationLoadResult.Invalid(prematureGeneralizationError, configurationFiles[0]);
                    }

                    configuration = configuration.WithPrematureGeneralization(prematureGeneralization);
                }
                if (rule.Name == "HAW007")
                {
                    var dependencies = GetConstructorDependencies(
                        rule.Name,
                        rule.Value,
                        configuration.ConstructorDependencies,
                        out var dependencyError);
                    if (dependencyError is not null)
                    {
                        return HawthorneConfigurationLoadResult.Invalid(dependencyError, configurationFiles[0]);
                    }

                    configuration = configuration.WithConstructorDependencies(dependencies);
                }
                if (rule.Name == "HAW008")
                {
                    var booleanControlFlow = GetBooleanControlFlow(
                        rule.Name,
                        rule.Value,
                        configuration.BooleanControlFlow,
                        out var booleanControlFlowError);
                    if (booleanControlFlowError is not null)
                    {
                        return HawthorneConfigurationLoadResult.Invalid(booleanControlFlowError, configurationFiles[0]);
                    }

                    configuration = configuration.WithBooleanControlFlow(booleanControlFlow);
                }
                if (rule.Name == "HAW010")
                {
                    var oneMethodServices = GetOneMethodServices(
                        rule.Name,
                        rule.Value,
                        configuration.OneMethodServices,
                        out var oneMethodServiceError);
                    if (oneMethodServiceError is not null)
                    {
                        return HawthorneConfigurationLoadResult.Invalid(oneMethodServiceError, configurationFiles[0]);
                    }

                    configuration = configuration.WithOneMethodServices(oneMethodServices);
                }
                if (rule.Name == "HAW011")
                {
                    var excessiveMicroMethods = GetExcessiveMicroMethods(
                        rule.Name,
                        rule.Value,
                        configuration.ExcessiveMicroMethods,
                        out var excessiveMicroMethodsError);
                    if (excessiveMicroMethodsError is not null)
                    {
                        return HawthorneConfigurationLoadResult.Invalid(excessiveMicroMethodsError, configurationFiles[0]);
                    }

                    configuration = configuration.WithExcessiveMicroMethods(excessiveMicroMethods);
                }
                if (rule.Name == "HAW013")
                {
                    var exceptionLaundering = GetExceptionLaundering(
                        rule.Name,
                        rule.Value,
                        configuration.ExceptionLaundering,
                        out var exceptionLaunderingError);
                    if (exceptionLaunderingError is not null)
                    {
                        return HawthorneConfigurationLoadResult.Invalid(exceptionLaunderingError, configurationFiles[0]);
                    }

                    configuration = configuration.WithExceptionLaundering(exceptionLaundering);
                }
                if (rule.Name == "HAW014")
                {
                    var defensiveNullChecking = GetDefensiveNullChecking(
                        rule.Name,
                        rule.Value,
                        configuration.DefensiveNullChecking,
                        out var defensiveNullCheckingError);
                    if (defensiveNullCheckingError is not null)
                    {
                        return HawthorneConfigurationLoadResult.Invalid(defensiveNullCheckingError, configurationFiles[0]);
                    }

                    configuration = configuration.WithDefensiveNullChecking(defensiveNullChecking);
                }
                if (rule.Name == "HAW015")
                {
                    var deadConfiguration = GetDeadConfiguration(
                        rule.Name,
                        rule.Value,
                        configuration.DeadConfiguration,
                        out var deadConfigurationError);
                    if (deadConfigurationError is not null)
                    {
                        return HawthorneConfigurationLoadResult.Invalid(deadConfigurationError, configurationFiles[0]);
                    }

                    configuration = configuration.WithDeadConfiguration(deadConfiguration);
                }
                if (rule.Name == "HAW016")
                {
                    var fakeAsync = GetFakeAsync(
                        rule.Name,
                        rule.Value,
                        configuration.FakeAsync,
                        out var fakeAsyncError);
                    if (fakeAsyncError is not null)
                    {
                        return HawthorneConfigurationLoadResult.Invalid(fakeAsyncError, configurationFiles[0]);
                    }

                    configuration = configuration.WithFakeAsync(fakeAsync);
                }
                if (rule.Name == "HAW017")
                {
                    var materialization = GetUnnecessaryLinqMaterialization(
                        rule.Name,
                        rule.Value,
                        configuration.UnnecessaryLinqMaterialization,
                        out var materializationError);
                    if (materializationError is not null)
                    {
                        return HawthorneConfigurationLoadResult.Invalid(materializationError, configurationFiles[0]);
                    }

                    configuration = configuration.WithUnnecessaryLinqMaterialization(materialization);
                }
                if (rule.Name == "HAW018")
                {
                    var repeatedEnumeration = GetRepeatedEnumeration(
                        rule.Name,
                        rule.Value,
                        configuration.RepeatedEnumeration,
                        out var repeatedEnumerationError);
                    if (repeatedEnumerationError is not null)
                    {
                        return HawthorneConfigurationLoadResult.Invalid(repeatedEnumerationError, configurationFiles[0]);
                    }

                    configuration = configuration.WithRepeatedEnumeration(repeatedEnumeration);
                }
                if (rule.Name == "HAW021")
                {
                    var excessiveTryCatch = GetExcessiveTryCatch(
                        rule.Name,
                        rule.Value,
                        configuration.ExcessiveTryCatch,
                        out var excessiveTryCatchError);
                    if (excessiveTryCatchError is not null)
                    {
                        return HawthorneConfigurationLoadResult.Invalid(excessiveTryCatchError, configurationFiles[0]);
                    }

                    configuration = configuration.WithExcessiveTryCatch(excessiveTryCatch);
                }
                if (rule.Name == "HAW024")
                {
                    var cancellationTokens = GetCancellationTokens(
                        rule.Name,
                        rule.Value,
                        configuration.CancellationTokens,
                        out var cancellationTokenError);
                    if (cancellationTokenError is not null)
                    {
                        return HawthorneConfigurationLoadResult.Invalid(cancellationTokenError, configurationFiles[0]);
                    }

                    configuration = configuration.WithCancellationTokens(cancellationTokens);
                }
                if (rule.Name == "HAW020")
                {
                    var repository = GetRepositoryLayer(rule.Name, rule.Value, configuration.RepositoryLayer, out var error);
                    if (error is not null) return HawthorneConfigurationLoadResult.Invalid(error, configurationFiles[0]);
                    configuration = configuration.WithRepositoryLayer(repository);
                }
                if (rule.Name == "HAW023")
                {
                    var extensionPoints = GetDeadExtensionPoints(rule.Name, rule.Value, configuration.DeadExtensionPoints, out var error);
                    if (error is not null) return HawthorneConfigurationLoadResult.Invalid(error, configurationFiles[0]);
                    configuration = configuration.WithDeadExtensionPoints(extensionPoints);
                }
                if (rule.Name == "HAW025")
                {
                    var flow = GetConfigurationFlow(rule.Name, rule.Value, configuration.ConfigurationFlow, out var error);
                    if (error is not null) return HawthorneConfigurationLoadResult.Invalid(error, configurationFiles[0]);
                    configuration = configuration.WithConfigurationFlow(flow);
                }
                if (rule.Name == "HAW029")
                {
                    var logging = GetLoggingNoise(rule.Name, rule.Value, configuration.LoggingNoise, out var error);
                    if (error is not null) return HawthorneConfigurationLoadResult.Invalid(error, configurationFiles[0]);
                    configuration = configuration.WithLoggingNoise(logging);
                }
                if (rule.Name == "HAW030")
                {
                    var copyPaste = GetCopyPaste(rule.Name, rule.Value, configuration.CopyPaste, out var error);
                    if (error is not null) return HawthorneConfigurationLoadResult.Invalid(error, configurationFiles[0]);
                    configuration = configuration.WithCopyPaste(copyPaste);
                }
                if (rule.Name == "HAW100")
                {
                    var density = GetAbstractionDensity(rule.Name, rule.Value, configuration.AbstractionDensity, out var error);
                    if (error is not null) return HawthorneConfigurationLoadResult.Invalid(error, configurationFiles[0]);
                    configuration = configuration.WithAbstractionDensity(density);
                }
                if (rule.Name == "HAW105")
                {
                    var methodLength = GetMethodLength(rule.Name, rule.Value, configuration.MethodLength, out var methodLengthError);
                    if (methodLengthError is not null)
                    {
                        return HawthorneConfigurationLoadResult.Invalid(methodLengthError, configurationFiles[0]);
                    }

                    configuration = configuration.WithMethodLength(methodLength);
                }
                if (rule.Name == "HAW103")
                {
                    var maximum = GetPositiveInteger(rule.Name, rule.Value, "maximum", configuration.MaximumNestingDepth, out var error);
                    if (error is not null) return HawthorneConfigurationLoadResult.Invalid(error, configurationFiles[0]);
                    configuration = configuration.WithMaximumNestingDepth(maximum);
                }
                if (rule.Name == "HAW101")
                {
                    var maximum = GetPositiveInteger(rule.Name, rule.Value, "maximum", configuration.MaximumCyclomaticComplexity, out var error);
                    if (error is not null) return HawthorneConfigurationLoadResult.Invalid(error, configurationFiles[0]);
                    configuration = configuration.WithMaximumCyclomaticComplexity(maximum);
                }
                if (rule.Name == "HAW102")
                {
                    var maximum = GetPositiveInteger(rule.Name, rule.Value, "maximum", configuration.MaximumCognitiveComplexity, out var error);
                    if (error is not null) return HawthorneConfigurationLoadResult.Invalid(error, configurationFiles[0]);
                    configuration = configuration.WithMaximumCognitiveComplexity(maximum);
                }
                if (rule.Name == "HAW004" && rule.Value.TryGetProperty("requireMutableState", out var mutableState))
                {
                    if (mutableState.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                        return HawthorneConfigurationLoadResult.Invalid("hawthorne.json.rules.HAW004.requireMutableState must be a boolean.", configurationFiles[0]);
                    configuration = configuration.WithRequireMutableSingletonState(mutableState.GetBoolean());
                }
                if (rule.Name == "HAW104")
                {
                    var maximum = GetPositiveInteger(rule.Name, rule.Value, "maximum", configuration.MaximumClassCoupling, out var error);
                    if (error is not null) return HawthorneConfigurationLoadResult.Invalid(error, configurationFiles[0]);
                    configuration = configuration.WithMaximumClassCoupling(maximum);
                }
            }

            return HawthorneConfigurationLoadResult.Valid(configuration, configurationFiles[0]);
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            return HawthorneConfigurationLoadResult.Invalid($"hawthorne.json is not valid JSON: {exception.Message}", configurationFiles[0]);
        }
    }

    private static HawthorneConfiguration CreateDefaults() =>
        HawthorneConfiguration.CreateDefaults(HawthorneDiagnosticDescriptors.All.Select(descriptor => descriptor.Id));

    private static bool GetEnabled(string ruleId, JsonElement rule, bool defaultValue, out string? errorMessage)
    {
        if (!rule.TryGetProperty("enabled", out var enabled))
        {
            errorMessage = null;
            return defaultValue;
        }

        if (enabled.ValueKind == JsonValueKind.True || enabled.ValueKind == JsonValueKind.False)
        {
            errorMessage = null;
            return enabled.GetBoolean();
        }

        errorMessage = $"hawthorne.json.rules.{ruleId}.enabled must be a boolean.";
        return default;
    }

    private static DiagnosticSeverity GetSeverity(
        string ruleId,
        JsonElement rule,
        DiagnosticSeverity defaultValue,
        out bool isConfigured,
        out string? errorMessage)
    {
        if (!rule.TryGetProperty("severity", out var severity))
        {
            isConfigured = false;
            errorMessage = null;
            return defaultValue;
        }

        if (severity.ValueKind != JsonValueKind.String)
        {
            isConfigured = false;
            errorMessage = $"hawthorne.json.rules.{ruleId}.severity must be error, warning, info, or hidden.";
            return default;
        }

        isConfigured = true;
        errorMessage = null;
        return severity.GetString() switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            "info" => DiagnosticSeverity.Info,
            "hidden" => DiagnosticSeverity.Hidden,
            _ => InvalidSeverity(ruleId, out isConfigured, out errorMessage),
        };
    }

    private static DiagnosticSeverity InvalidSeverity(string ruleId, out bool isConfigured, out string? errorMessage)
    {
        isConfigured = false;
        errorMessage = $"hawthorne.json.rules.{ruleId}.severity must be error, warning, info, or hidden.";
        return default;
    }

    private static Hawthorne105Configuration GetMethodLength(string ruleId, JsonElement rule, Hawthorne105Configuration defaults, out string? errorMessage)
    {
        var statements = GetPositiveInteger(ruleId, rule, "maximumExecutableStatements", defaults.MaximumExecutableStatements, out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var lines = GetPositiveInteger(ruleId, rule, "maximumPhysicalLines", defaults.MaximumPhysicalLines, out errorMessage);
        return errorMessage is null ? new Hawthorne105Configuration(statements, lines) : defaults;
    }

    private static Hawthorne005Configuration GetWrapper(
        string ruleId,
        JsonElement rule,
        Hawthorne005Configuration defaults,
        out string? errorMessage)
    {
        var methods = GetPositiveInteger(
            ruleId,
            rule,
            "minimumForwardingMethods",
            defaults.MinimumForwardingMethods,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var ratio = GetUnitIntervalDouble(
            ruleId,
            rule,
            "minimumForwardingRatio",
            defaults.MinimumForwardingRatio,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var requiresRoleSuffix = GetBoolean(
            ruleId,
            rule,
            "requireRoleSuffix",
            defaults.RequireRoleSuffix,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var suffixes = GetNonEmptyStringArray(
            ruleId,
            rule,
            "roleSuffixes",
            defaults.RoleSuffixes,
            out errorMessage);
        return errorMessage is null
            ? new Hawthorne005Configuration(methods, ratio, requiresRoleSuffix, suffixes)
            : defaults;
    }

    private static Hawthorne006Configuration GetPrematureGeneralization(
        string ruleId,
        JsonElement rule,
        Hawthorne006Configuration defaults,
        out string? errorMessage)
    {
        var maximumSharedStatements = GetPositiveInteger(
            ruleId,
            rule,
            "maximumSharedExecutableStatements",
            defaults.MaximumSharedExecutableStatements,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var minimumDerivedTypes = GetPositiveInteger(
            ruleId,
            rule,
            "minimumDerivedTypes",
            defaults.MinimumDerivedTypes,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var analyzeSingleClosedGenericUse = GetBoolean(
            ruleId,
            rule,
            "analyzeSingleClosedGenericUse",
            defaults.AnalyzeSingleClosedGenericUse,
            out errorMessage);
        return errorMessage is null
            ? new Hawthorne006Configuration(
                maximumSharedStatements,
                minimumDerivedTypes,
                analyzeSingleClosedGenericUse)
            : defaults;
    }

    private static Hawthorne007Configuration GetConstructorDependencies(
        string ruleId,
        JsonElement rule,
        Hawthorne007Configuration defaults,
        out string? errorMessage)
    {
        var maximum = GetPositiveInteger(
            ruleId,
            rule,
            "maximumDependencies",
            defaults.MaximumDependencies,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var excludeOptionWrappers = GetBoolean(
            ruleId,
            rule,
            "excludeOptionWrappers",
            defaults.ExcludeOptionWrappers,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var suffixes = GetNonEmptyStringArray(
            ruleId,
            rule,
            "configurationTypeSuffixes",
            defaults.ConfigurationTypeSuffixes,
            out errorMessage);
        return errorMessage is null
            ? new Hawthorne007Configuration(maximum, excludeOptionWrappers, suffixes)
            : defaults;
    }

    private static Hawthorne008Configuration GetBooleanControlFlow(
        string ruleId,
        JsonElement rule,
        Hawthorne008Configuration defaults,
        out string? errorMessage)
    {
        var warningCount = GetPositiveInteger(
            ruleId,
            rule,
            "warningParameterCount",
            defaults.WarningParameterCount,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var errorCount = GetPositiveInteger(
            ruleId,
            rule,
            "errorParameterCount",
            defaults.ErrorParameterCount,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        if (errorCount < warningCount)
        {
            errorMessage = $"hawthorne.json.rules.{ruleId}.errorParameterCount must be greater than or equal to warningParameterCount.";
            return defaults;
        }

        var requiresDirectUse = GetBoolean(
            ruleId,
            rule,
            "requireDirectControlFlowUse",
            defaults.RequireDirectControlFlowUse,
            out errorMessage);
        return errorMessage is null
            ? new Hawthorne008Configuration(warningCount, errorCount, requiresDirectUse)
            : defaults;
    }

    private static Hawthorne016Configuration GetFakeAsync(
        string ruleId,
        JsonElement rule,
        Hawthorne016Configuration defaults,
        out string? errorMessage)
    {
        var ignoreContractMethods = GetBoolean(
            ruleId,
            rule,
            "ignoreContractMethods",
            defaults.IgnoreContractMethods,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var reportTrivialTaskRun = GetBoolean(
            ruleId,
            rule,
            "reportTrivialTaskRun",
            defaults.ReportTrivialTaskRun,
            out errorMessage);
        return errorMessage is null
            ? new Hawthorne016Configuration(ignoreContractMethods, reportTrivialTaskRun)
            : defaults;
    }

    private static Hawthorne017Configuration GetUnnecessaryLinqMaterialization(
        string ruleId,
        JsonElement rule,
        Hawthorne017Configuration defaults,
        out string? errorMessage)
    {
        var analyzeToList = GetBoolean(
            ruleId,
            rule,
            "analyzeToList",
            defaults.AnalyzeToList,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var analyzeToArray = GetBoolean(
            ruleId,
            rule,
            "analyzeToArray",
            defaults.AnalyzeToArray,
            out errorMessage);
        return errorMessage is null
            ? new Hawthorne017Configuration(analyzeToList, analyzeToArray)
            : defaults;
    }

    private static Hawthorne021Configuration GetExcessiveTryCatch(
        string ruleId,
        JsonElement rule,
        Hawthorne021Configuration defaults,
        out string? errorMessage)
    {
        var minimumMethodCount = GetPositiveInteger(
            ruleId,
            rule,
            "minimumMethodCount",
            defaults.MinimumMethodCount,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var maximumTryBlocksPerMethod = GetUnitIntervalDouble(
            ruleId,
            rule,
            "maximumTryBlocksPerMethod",
            defaults.MaximumTryBlocksPerMethod,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var reportCatchAllDefaultReturn = GetBoolean(
            ruleId,
            rule,
            "reportCatchAllDefaultReturn",
            defaults.ReportCatchAllDefaultReturn,
            out errorMessage);
        return errorMessage is null
            ? new Hawthorne021Configuration(
                minimumMethodCount,
                maximumTryBlocksPerMethod,
                reportCatchAllDefaultReturn)
            : defaults;
    }

    private static Hawthorne018Configuration GetRepeatedEnumeration(
        string ruleId,
        JsonElement rule,
        Hawthorne018Configuration defaults,
        out string? errorMessage)
    {
        var minimumEnumerations = GetPositiveInteger(
            ruleId,
            rule,
            "minimumEnumerations",
            defaults.MinimumEnumerations,
            out errorMessage);
        return errorMessage is null ? new Hawthorne018Configuration(minimumEnumerations) : defaults;
    }

    private static Hawthorne013Configuration GetExceptionLaundering(
        string ruleId,
        JsonElement rule,
        Hawthorne013Configuration defaults,
        out string? errorMessage)
    {
        var genericExceptionTypes = GetNonEmptyStringArray(
            ruleId,
            rule,
            "genericExceptionTypes",
            defaults.GenericExceptionTypes,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var reportLogAndRethrow = GetBoolean(
            ruleId,
            rule,
            "reportLogAndRethrow",
            defaults.ReportLogAndRethrow,
            out errorMessage);
        return errorMessage is null
            ? new Hawthorne013Configuration(genericExceptionTypes, reportLogAndRethrow)
            : defaults;
    }

    private static Hawthorne015Configuration GetDeadConfiguration(
        string ruleId,
        JsonElement rule,
        Hawthorne015Configuration defaults,
        out string? errorMessage)
    {
        var includeInternalProperties = GetBoolean(
            ruleId,
            rule,
            "includeInternalProperties",
            defaults.IncludeInternalProperties,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var suffixes = GetNonEmptyStringArray(
            ruleId,
            rule,
            "configurationTypeSuffixes",
            defaults.ConfigurationTypeSuffixes,
            out errorMessage);
        return errorMessage is null
            ? new Hawthorne015Configuration(includeInternalProperties, suffixes)
            : defaults;
    }

    private static Hawthorne014Configuration GetDefensiveNullChecking(
        string ruleId,
        JsonElement rule,
        Hawthorne014Configuration defaults,
        out string? errorMessage)
    {
        var includeInternalMethods = GetBoolean(
            ruleId,
            rule,
            "includeInternalMethods",
            defaults.IncludeInternalMethods,
            out errorMessage);
        return errorMessage is null
            ? new Hawthorne014Configuration(includeInternalMethods)
            : defaults;
    }

    private static Hawthorne010Configuration GetOneMethodServices(
        string ruleId,
        JsonElement rule,
        Hawthorne010Configuration defaults,
        out string? errorMessage)
    {
        var suffixes = GetNonEmptyStringArray(
            ruleId,
            rule,
            "serviceSuffixes",
            defaults.ServiceSuffixes,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var physicalLines = GetPositiveInteger(
            ruleId,
            rule,
            "maximumPhysicalLines",
            defaults.MaximumPhysicalLines,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var cyclomaticComplexity = GetPositiveInteger(
            ruleId,
            rule,
            "maximumCyclomaticComplexity",
            defaults.MaximumCyclomaticComplexity,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var sourceCallers = GetPositiveInteger(
            ruleId,
            rule,
            "maximumSourceCallers",
            defaults.MaximumSourceCallers,
            out errorMessage);
        return errorMessage is null
            ? new Hawthorne010Configuration(suffixes, physicalLines, cyclomaticComplexity, sourceCallers)
            : defaults;
    }

    private static Hawthorne011Configuration GetExcessiveMicroMethods(
        string ruleId,
        JsonElement rule,
        Hawthorne011Configuration defaults,
        out string? errorMessage)
    {
        var maximumRatio = GetUnitIntervalDouble(
            ruleId,
            rule,
            "maxTinyMethodRatio",
            defaults.MaxTinyMethodRatio,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var tinyMethodStatementLimit = GetPositiveInteger(
            ruleId,
            rule,
            "tinyMethodStatementLimit",
            defaults.TinyMethodStatementLimit,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var minimumMethodCount = GetPositiveInteger(
            ruleId,
            rule,
            "minimumMethodCount",
            defaults.MinimumMethodCount,
            out errorMessage);
        return errorMessage is null
            ? new Hawthorne011Configuration(maximumRatio, tinyMethodStatementLimit, minimumMethodCount)
            : defaults;
    }

    private static Hawthorne024Configuration GetCancellationTokens(
        string ruleId,
        JsonElement rule,
        Hawthorne024Configuration defaults,
        out string? errorMessage)
    {
        var reportMissingForwarding = GetBoolean(
            ruleId,
            rule,
            "reportMissingForwarding",
            defaults.ReportMissingForwarding,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var treatNoneAsMissingForwarding = GetBoolean(
            ruleId,
            rule,
            "treatNoneAsMissingForwarding",
            defaults.TreatNoneAsMissingForwarding,
            out errorMessage);
        if (errorMessage is not null)
        {
            return defaults;
        }

        var ignoreContractMethods = GetBoolean(
            ruleId,
            rule,
            "ignoreContractMethods",
            defaults.IgnoreContractMethods,
            out errorMessage);
        return errorMessage is null
            ? new Hawthorne024Configuration(
                reportMissingForwarding,
                treatNoneAsMissingForwarding,
                ignoreContractMethods)
            : defaults;
    }

    private static Hawthorne020Configuration GetRepositoryLayer(string ruleId, JsonElement rule, Hawthorne020Configuration defaults, out string? error)
    {
        var methods = GetPositiveInteger(ruleId, rule, "minimumForwardingMethods", defaults.MinimumForwardingMethods, out error);
        if (error is not null) return defaults;
        var ratio = GetUnitIntervalDouble(ruleId, rule, "minimumForwardingRatio", defaults.MinimumForwardingRatio, out error);
        if (error is not null) return defaults;
        var suffixes = GetNonEmptyStringArray(ruleId, rule, "repositorySuffixes", defaults.RepositorySuffixes, out error);
        if (error is not null) return defaults;
        var required = GetBoolean(ruleId, rule, "requireRepositorySuffix", defaults.RequireRepositorySuffix, out error);
        return error is null ? new Hawthorne020Configuration(methods, ratio, suffixes, required) : defaults;
    }

    private static Hawthorne023Configuration GetDeadExtensionPoints(string ruleId, JsonElement rule, Hawthorne023Configuration defaults, out string? error)
    {
        var privateMembers = GetBoolean(ruleId, rule, "includePrivateMembers", defaults.IncludePrivateMembers, out error);
        if (error is not null) return defaults;
        var external = GetBoolean(ruleId, rule, "includeExternallyAccessibleMembers", defaults.IncludeExternallyAccessibleMembers, out error);
        if (error is not null) return defaults;
        var events = GetBoolean(ruleId, rule, "analyzeEvents", defaults.AnalyzeEvents, out error);
        if (error is not null) return defaults;
        var callbacks = GetBoolean(ruleId, rule, "analyzeCallbacks", defaults.AnalyzeCallbacks, out error);
        if (error is not null) return defaults;
        var hooks = GetBoolean(ruleId, rule, "analyzeVirtualHooks", defaults.AnalyzeVirtualHooks, out error);
        return error is null ? new Hawthorne023Configuration(privateMembers, external, events, callbacks, hooks) : defaults;
    }

    private static Hawthorne025Configuration GetConfigurationFlow(string ruleId, JsonElement rule, Hawthorne025Configuration defaults, out string? error)
    {
        var hops = GetPositiveInteger(ruleId, rule, "minimumForwardingHops", defaults.MinimumForwardingHops, out error);
        if (error is not null) return defaults;
        var suffixes = GetNonEmptyStringArray(ruleId, rule, "configurationTypeSuffixes", defaults.ConfigurationTypeSuffixes, out error);
        return error is null ? new Hawthorne025Configuration(hops, suffixes) : defaults;
    }

    private static Hawthorne029Configuration GetLoggingNoise(string ruleId, JsonElement rule, Hawthorne029Configuration defaults, out string? error)
    {
        var methods = GetPositiveInteger(ruleId, rule, "minimumMethodCount", defaults.MinimumMethodCount, out error);
        if (error is not null) return defaults;
        var ratio = GetUnitIntervalDouble(ruleId, rule, "maximumLifecycleLogRatio", defaults.MaximumLifecycleLogRatio, out error);
        if (error is not null) return defaults;
        var terms = GetNonEmptyStringArray(ruleId, rule, "lifecycleTerms", defaults.LifecycleTerms, out error);
        if (error is not null) return defaults;
        var loggerNames = GetNonEmptyStringArray(ruleId, rule, "loggerTypeNames", defaults.LoggerTypeNames, out error);
        return error is null ? new Hawthorne029Configuration(methods, ratio, terms, loggerNames) : defaults;
    }

    private static Hawthorne030Configuration GetCopyPaste(string ruleId, JsonElement rule, Hawthorne030Configuration defaults, out string? error)
    {
        var methods = GetPositiveInteger(ruleId, rule, "minimumMethods", defaults.MinimumMethods, out error);
        if (error is not null) return defaults;
        var statements = GetPositiveInteger(ruleId, rule, "minimumStatements", defaults.MinimumStatements, out error);
        if (error is not null) return defaults;
        var similarity = GetUnitIntervalDouble(ruleId, rule, "minimumSimilarity", defaults.MinimumSimilarity, out error);
        return error is null ? new Hawthorne030Configuration(methods, statements, similarity) : defaults;
    }

    private static Hawthorne100Configuration GetAbstractionDensity(string ruleId, JsonElement rule, Hawthorne100Configuration defaults, out string? error)
    {
        double? maximum = defaults.MaximumDensity;
        if (rule.TryGetProperty("maximumDensity", out var value))
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var parsed) || parsed <= 0)
            {
                error = $"hawthorne.json.rules.{ruleId}.maximumDensity must be a number greater than zero.";
                return defaults;
            }
            maximum = parsed;
        }
        var minimumBehavioral = GetPositiveInteger(ruleId, rule, "minimumBehavioralTypes", defaults.MinimumBehavioralTypes, out error);
        if (error is not null) return defaults;
        var suffixes = GetNonEmptyStringArray(ruleId, rule, "abstractionRoleSuffixes", defaults.AbstractionRoleSuffixes, out error);
        return error is null ? new Hawthorne100Configuration(maximum, minimumBehavioral, suffixes) : defaults;
    }

    private static bool GetBoolean(
        string ruleId,
        JsonElement rule,
        string propertyName,
        bool defaultValue,
        out string? errorMessage)
    {
        if (!rule.TryGetProperty(propertyName, out var value))
        {
            errorMessage = null;
            return defaultValue;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            errorMessage = null;
            return value.GetBoolean();
        }

        errorMessage = $"hawthorne.json.rules.{ruleId}.{propertyName} must be a boolean.";
        return default;
    }

    private static double GetUnitIntervalDouble(
        string ruleId,
        JsonElement rule,
        string propertyName,
        double defaultValue,
        out string? errorMessage)
    {
        if (!rule.TryGetProperty(propertyName, out var value))
        {
            errorMessage = null;
            return defaultValue;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var parsed) && parsed > 0 && parsed <= 1)
        {
            errorMessage = null;
            return parsed;
        }

        errorMessage = $"hawthorne.json.rules.{ruleId}.{propertyName} must be a number greater than zero and at most one.";
        return default;
    }

    private static ImmutableArray<string> GetNonEmptyStringArray(
        string ruleId,
        JsonElement rule,
        string propertyName,
        ImmutableArray<string> defaultValue,
        out string? errorMessage)
    {
        if (!rule.TryGetProperty(propertyName, out var value))
        {
            errorMessage = null;
            return defaultValue;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            errorMessage = $"hawthorne.json.rules.{ruleId}.{propertyName} must be a non-empty array of non-blank strings.";
            return defaultValue;
        }

        var values = ImmutableArray.CreateBuilder<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                errorMessage = $"hawthorne.json.rules.{ruleId}.{propertyName} must be a non-empty array of non-blank strings.";
                return defaultValue;
            }

            values.Add(item.GetString()!);
        }

        if (values.Count == 0)
        {
            errorMessage = $"hawthorne.json.rules.{ruleId}.{propertyName} must be a non-empty array of non-blank strings.";
            return defaultValue;
        }

        errorMessage = null;
        return values.ToImmutable();
    }

    private static int GetPositiveInteger(string ruleId, JsonElement rule, string propertyName, int defaultValue, out string? errorMessage)
    {
        if (!rule.TryGetProperty(propertyName, out var value))
        {
            errorMessage = null;
            return defaultValue;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed) && parsed > 0)
        {
            errorMessage = null;
            return parsed;
        }

        errorMessage = $"hawthorne.json.rules.{ruleId}.{propertyName} must be an integer greater than zero.";
        return defaultValue;
    }
}
