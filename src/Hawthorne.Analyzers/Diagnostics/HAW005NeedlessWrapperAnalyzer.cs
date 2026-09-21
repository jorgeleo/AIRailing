using Hawthorne.Analyzers.Analysis.Architecture;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW005NeedlessWrapperAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSyntaxNodeAction(
            c => Analyze((ClassDeclarationSyntax)c.Node, c, configuration),
            SyntaxKind.ClassDeclaration);

    internal static bool TryGetViolation(
        ClassDeclarationSyntax type,
        SemanticModel semanticModel,
        Hawthorne005Configuration configuration,
        out HAW005Violation? violation)
    {
        violation = null;
        if (configuration.RequireRoleSuffix && !configuration.HasConfiguredRoleSuffix(type.Identifier.ValueText))
        {
            return false;
        }

        var eligibleMethods = type.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(method => IsEligiblePublicMethod(method, semanticModel))
            .ToArray();
        if (eligibleMethods.Length == 0)
        {
            return false;
        }

        var forwardedByDependency = new Dictionary<ISymbol, int>(SymbolEqualityComparer.Default);
        foreach (var method in eligibleMethods)
        {
            if (!ForwardingMethodClassifier.TryGetForwardedDependency(method, semanticModel, out var dependency) ||
                dependency is null)
            {
                continue;
            }

            forwardedByDependency[dependency] = forwardedByDependency.TryGetValue(dependency, out var count)
                ? count + 1
                : 1;
        }

        var dominant = forwardedByDependency
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key.Name, StringComparer.Ordinal)
            .FirstOrDefault();
        var ratio = dominant.Value / (double)eligibleMethods.Length;
        if (dominant.Key is null || dominant.Value < configuration.MinimumForwardingMethods ||
            ratio < configuration.MinimumForwardingRatio)
        {
            return false;
        }

        violation = new HAW005Violation(dominant.Key, ratio);
        return true;
    }

    private static void Analyze(
        ClassDeclarationSyntax type,
        SyntaxNodeAnalysisContext context,
        HawthorneConfiguration configuration)
    {
        if (!TryGetViolation(type, context.SemanticModel, configuration.Wrapper, out var violation) || violation is null)
        {
            return;
        }

        context.ReportHawthorneDiagnostic(
            HawthorneDiagnosticDescriptors.HAW005,
            type.Identifier.GetLocation(),
            configuration,
            type.Identifier.ValueText,
            violation.ForwardingRatio,
            violation.Dependency.Name);
    }

    private static bool IsEligiblePublicMethod(MethodDeclarationSyntax method, SemanticModel semanticModel) =>
        semanticModel.GetDeclaredSymbol(method) is { DeclaredAccessibility: Accessibility.Public, MethodKind: MethodKind.Ordinary };
}

internal sealed class HAW005Violation
{
    internal HAW005Violation(ISymbol dependency, double forwardingRatio)
    {
        Dependency = dependency;
        ForwardingRatio = forwardingRatio;
    }

    internal ISymbol Dependency { get; }
    internal double ForwardingRatio { get; }
}
