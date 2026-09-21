using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class DiagnosticReportingExtensions
{
    internal static void ReportHawthorneDiagnostic(
        this SyntaxNodeAnalysisContext context,
        DiagnosticDescriptor descriptor,
        Location location,
        HawthorneConfiguration configuration,
        params object[] messageArguments)
    {
        var diagnostic = CreateHawthorneDiagnostic(descriptor, location, configuration, messageArguments);
        if (diagnostic is not null)
        {
            context.ReportDiagnostic(diagnostic);
        }
    }

    internal static Diagnostic? CreateHawthorneDiagnostic(
        DiagnosticDescriptor descriptor,
        Location location,
        HawthorneConfiguration configuration,
        params object[] messageArguments)
        => CreateHawthorneDiagnosticWithDefaultSeverity(
            descriptor,
            location,
            configuration,
            configuration.GetRule(descriptor.Id).Severity,
            messageArguments);

    internal static Diagnostic? CreateHawthorneDiagnosticWithDefaultSeverity(
        DiagnosticDescriptor descriptor,
        Location location,
        HawthorneConfiguration configuration,
        DiagnosticSeverity defaultSeverity,
        params object[] messageArguments)
    {
        var rule = configuration.GetRule(descriptor.Id);
        if (!rule.IsEnabled)
        {
            return null;
        }

        return Diagnostic.Create(
            descriptor,
            location,
            rule.IsSeverityConfigured ? rule.Severity : defaultSeverity,
            additionalLocations: null,
            properties: null,
            messageArgs: messageArguments);
    }

    internal static void ReportHawthorneDiagnostic(
        this SyntaxTreeAnalysisContext context,
        DiagnosticDescriptor descriptor,
        Location location,
        HawthorneConfiguration configuration,
        params object[] messageArguments)
    {
        var diagnostic = CreateHawthorneDiagnostic(descriptor, location, configuration, messageArguments);
        if (diagnostic is not null)
        {
            context.ReportDiagnostic(diagnostic);
        }
    }
}
