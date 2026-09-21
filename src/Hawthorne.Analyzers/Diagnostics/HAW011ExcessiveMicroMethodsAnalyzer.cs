using Hawthorne.Analyzers.Analysis.Metrics;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW011ExcessiveMicroMethodsAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSymbolAction(
            c => Analyze((INamedTypeSymbol)c.Symbol, c, configuration),
            SymbolKind.NamedType);

    private static void Analyze(
        INamedTypeSymbol type,
        SymbolAnalysisContext context,
        HawthorneConfiguration configuration)
    {
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct) ||
            !type.Locations.Any(location => location.IsInSource))
        {
            return;
        }

        var methods = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(IsEligiblePrivateMethod)
            .ToArray();
        if (methods.Length < configuration.ExcessiveMicroMethods.MinimumMethodCount)
        {
            return;
        }

        var tinyMethodCount = methods.Count(method => IsTinyMethod(
            method,
            configuration.ExcessiveMicroMethods.TinyMethodStatementLimit));
        var tinyMethodRatio = (double)tinyMethodCount / methods.Length;
        if (tinyMethodRatio <= configuration.ExcessiveMicroMethods.MaxTinyMethodRatio)
        {
            return;
        }

        var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(
            HawthorneDiagnosticDescriptors.HAW011,
            type.Locations.First(location => location.IsInSource),
            configuration,
            type.Name,
            tinyMethodRatio,
            configuration.ExcessiveMicroMethods.MaxTinyMethodRatio);
        if (diagnostic is not null)
        {
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsEligiblePrivateMethod(IMethodSymbol method) =>
        method.MethodKind == MethodKind.Ordinary &&
        method.DeclaredAccessibility == Accessibility.Private &&
        !method.IsOverride &&
        method.DeclaringSyntaxReferences.Any(reference =>
            reference.GetSyntax() is MethodDeclarationSyntax { Body: not null } or MethodDeclarationSyntax { ExpressionBody: not null });

    private static bool IsTinyMethod(IMethodSymbol method, int statementLimit) =>
        method.DeclaringSyntaxReferences.All(reference =>
            reference.GetSyntax() is MethodDeclarationSyntax declaration &&
            MethodLengthCalculator.CountExecutableStatements(declaration) <= statementLimit);
}
