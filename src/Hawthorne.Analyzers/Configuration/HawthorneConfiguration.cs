using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Hawthorne.Analyzers.Configuration;

internal sealed class HawthorneConfiguration
{
    internal const string FileName = "hawthorne.json";

    private HawthorneConfiguration(ImmutableDictionary<string, HawthorneRuleConfiguration> rules)
    {
        Rules = rules;
    }

    internal ImmutableDictionary<string, HawthorneRuleConfiguration> Rules { get; }

    internal HawthorneRuleConfiguration GetRule(string diagnosticId) => Rules[diagnosticId];

    internal HawthorneConfiguration WithRule(string diagnosticId, HawthorneRuleConfiguration rule) =>
        new(Rules.SetItem(diagnosticId, rule));

    internal static HawthorneConfiguration CreateDefaults(IEnumerable<string> diagnosticIds) =>
        new(diagnosticIds.ToImmutableDictionary(
            diagnosticId => diagnosticId,
            _ => HawthorneRuleConfiguration.Default,
            StringComparer.Ordinal));
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
    private HawthorneConfigurationLoadResult(HawthorneConfiguration? configuration, string? errorMessage)
    {
        Configuration = configuration;
        ErrorMessage = errorMessage;
    }

    internal HawthorneConfiguration? Configuration { get; }

    internal string? ErrorMessage { get; }

    internal bool IsValid => Configuration is not null;

    internal static HawthorneConfigurationLoadResult Valid(HawthorneConfiguration configuration) => new(configuration, null);

    internal static HawthorneConfigurationLoadResult Invalid(string errorMessage) => new(null, errorMessage);
}
