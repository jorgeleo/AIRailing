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

                var severity = GetSeverity(rule.Name, rule.Value, defaultRule.Severity, out var severityError);
                if (severityError is not null)
                {
                    return HawthorneConfigurationLoadResult.Invalid(severityError, configurationFiles[0]);
                }

                configuration = configuration.WithRule(rule.Name, new HawthorneRuleConfiguration(enabled, severity));
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

    private static DiagnosticSeverity GetSeverity(string ruleId, JsonElement rule, DiagnosticSeverity defaultValue, out string? errorMessage)
    {
        if (!rule.TryGetProperty("severity", out var severity))
        {
            errorMessage = null;
            return defaultValue;
        }

        if (severity.ValueKind != JsonValueKind.String)
        {
            errorMessage = $"hawthorne.json.rules.{ruleId}.severity must be error, warning, info, or hidden.";
            return default;
        }

        errorMessage = null;
        return severity.GetString() switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            "info" => DiagnosticSeverity.Info,
            "hidden" => DiagnosticSeverity.Hidden,
            _ => InvalidSeverity(ruleId, out errorMessage),
        };
    }

    private static DiagnosticSeverity InvalidSeverity(string ruleId, out string? errorMessage)
    {
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
