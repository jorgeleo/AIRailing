using Hawthorne.Analyzers.Analysis.Architecture;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW003PassThroughAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSyntaxNodeAction(c => Analyze((ClassDeclarationSyntax)c.Node, c, configuration), SyntaxKind.ClassDeclaration);

    private static void Analyze(ClassDeclarationSyntax type, SyntaxNodeAnalysisContext context, HawthorneConfiguration configuration)
    {
        if (configuration.GetRule("HAW005").IsEnabled &&
            HAW005NeedlessWrapperAnalyzer.TryGetViolation(type, context.SemanticModel, configuration.Wrapper, out _))
        {
            return;
        }

        var methods = type.Members.OfType<MethodDeclarationSyntax>().ToArray();
        var forwarding = methods.Where(method =>
            ForwardingMethodClassifier.TryGetForwardedDependency(method, context.SemanticModel, out _)).ToArray();
        if (forwarding.Length >= 3 && forwarding.Length / (double)methods.Length >= 0.80)
        {
            context.ReportHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW003, type.Identifier.GetLocation(), configuration,
                $"Type '{type.Identifier.ValueText}' forwards {forwarding.Length / (double)methods.Length:P0} of its eligible methods without adding behavior. Remove or collapse the forwarding layer.");
            return;
        }

        foreach (var method in forwarding)
        {
            context.ReportHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW003, method.Identifier.GetLocation(), configuration,
                $"Method '{method.Identifier.ValueText}' only forwards its arguments without adding behavior. Remove the method or add the responsibility that justifies it.");
        }
    }

}
