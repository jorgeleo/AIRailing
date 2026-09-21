using System.Collections.Concurrent;
using Hawthorne.Analyzers.Analysis.Architecture;
using Hawthorne.Analyzers.Analysis.Complexity;
using Hawthorne.Analyzers.Analysis.Metrics;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW010OneMethodServiceAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration)
    {
        var callIndex = new SourceCallIndex();
        var sourceTypes = new ConcurrentBag<INamedTypeSymbol>();
        context.RegisterOperationAction(
            c => callIndex.Record(((IInvocationOperation)c.Operation).TargetMethod, c.ContainingSymbol),
            OperationKind.Invocation);
        context.RegisterSymbolAction(c =>
        {
            var type = (INamedTypeSymbol)c.Symbol;
            if (type.TypeKind == TypeKind.Class && type.Locations.Any(location => location.IsInSource))
            {
                sourceTypes.Add(type);
            }
        }, SymbolKind.NamedType);
        context.RegisterCompilationEndAction(c => Analyze(sourceTypes, callIndex, c, configuration));
    }

    private static void Analyze(
        ConcurrentBag<INamedTypeSymbol> sourceTypes,
        SourceCallIndex callIndex,
        CompilationAnalysisContext context,
        HawthorneConfiguration configuration)
    {
        foreach (var type in sourceTypes)
        {
            if (!IsCandidate(type, configuration.OneMethodServices, out var method))
            {
                continue;
            }

            var callers = callIndex.GetSourceCallerCount(method);
            if (callers > configuration.OneMethodServices.MaximumSourceCallers)
            {
                continue;
            }

            var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(
                HawthorneDiagnosticDescriptors.HAW010,
                type.Locations.First(location => location.IsInSource),
                configuration,
                type.Name,
                callers);
            if (diagnostic is not null)
            {
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool IsCandidate(
        INamedTypeSymbol type,
        Hawthorne010Configuration configuration,
        out IMethodSymbol method)
    {
        method = null!;
        if (type.IsAbstract || type.AllInterfaces.Length > 0 || !configuration.HasServiceSuffix(type.Name))
        {
            return false;
        }

        var methods = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(member =>
                member.MethodKind == MethodKind.Ordinary &&
                member.DeclaredAccessibility == Accessibility.Public &&
                !member.IsOverride)
            .ToArray();
        if (methods.Length != 1 || methods[0].DeclaringSyntaxReferences.Length != 1)
        {
            return false;
        }

        if (methods[0].DeclaringSyntaxReferences[0].GetSyntax() is not MethodDeclarationSyntax declaration)
        {
            return false;
        }

        if (MethodLengthCalculator.CountPhysicalLines(declaration) > configuration.MaximumPhysicalLines ||
            CyclomaticComplexityCalculator.Calculate(declaration) > configuration.MaximumCyclomaticComplexity)
        {
            return false;
        }

        method = methods[0];
        return true;
    }
}
