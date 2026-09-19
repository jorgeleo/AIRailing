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
    {
        var rule = configuration.GetRule(descriptor.Id);
        if (!rule.IsEnabled ||
            (location.SourceTree is not null && new HawthorneExceptionEvaluator(configuration).IsExcepted(descriptor.Id, location.SourceTree)))
        {
            return null;
        }

        return Diagnostic.Create(
            descriptor,
            location,
            rule.Severity,
            additionalLocations: null,
            properties: null,
            messageArgs: messageArguments);
    }
}
