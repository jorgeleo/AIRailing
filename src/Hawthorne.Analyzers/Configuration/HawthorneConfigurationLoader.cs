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
            .Where(file => string.Equals(Path.GetFileName(file.Path), HawthorneConfiguration.FileName, StringComparison.Ordinal))
            .ToArray();

        if (configurationFiles.Length == 0)
        {
            return HawthorneConfigurationLoadResult.Valid(CreateDefaults());
        }

        if (configurationFiles.Length > 1)
        {
            return HawthorneConfigurationLoadResult.Invalid("Only one hawthorne.json may be supplied to a compilation.");
        }

        var text = configurationFiles[0].GetText()?.ToString();
        if (text is null || string.IsNullOrWhiteSpace(text))
        {
            return HawthorneConfigurationLoadResult.Invalid("hawthorne.json must contain a JSON object.");
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return HawthorneConfigurationLoadResult.Invalid("hawthorne.json must contain a JSON object.");
            }

            if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || version.GetInt32() != 1)
            {
                return HawthorneConfigurationLoadResult.Invalid("hawthorne.json.version must be the integer 1.");
            }

            var configuration = CreateDefaults();
            if (!root.TryGetProperty("rules", out var rules))
            {
                return HawthorneConfigurationLoadResult.Valid(configuration);
            }

            if (rules.ValueKind != JsonValueKind.Object)
            {
                return HawthorneConfigurationLoadResult.Invalid("hawthorne.json.rules must be a JSON object.");
            }

            foreach (var rule in rules.EnumerateObject())
            {
                if (!configuration.Rules.ContainsKey(rule.Name))
                {
                    return HawthorneConfigurationLoadResult.Invalid($"hawthorne.json.rules contains unknown rule '{rule.Name}'.");
                }

                if (rule.Value.ValueKind != JsonValueKind.Object)
                {
                    return HawthorneConfigurationLoadResult.Invalid($"hawthorne.json.rules.{rule.Name} must be a JSON object.");
                }

                var defaultRule = configuration.GetRule(rule.Name);
                var enabled = GetEnabled(rule.Name, rule.Value, defaultRule.IsEnabled, out var enabledError);
                if (enabledError is not null)
                {
                    return HawthorneConfigurationLoadResult.Invalid(enabledError);
                }

                var severity = GetSeverity(rule.Name, rule.Value, defaultRule.Severity, out var severityError);
                if (severityError is not null)
                {
                    return HawthorneConfigurationLoadResult.Invalid(severityError);
                }

                configuration = configuration.WithRule(rule.Name, new HawthorneRuleConfiguration(enabled, severity));
            }

            return HawthorneConfigurationLoadResult.Valid(configuration);
        }
        catch (JsonException exception)
        {
            return HawthorneConfigurationLoadResult.Invalid($"hawthorne.json is not valid JSON: {exception.Message}");
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
}
