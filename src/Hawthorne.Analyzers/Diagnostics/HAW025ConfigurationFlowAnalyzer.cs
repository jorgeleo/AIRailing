using System.Collections.Immutable;
using System.Globalization;
using Hawthorne.Analyzers.Analysis.Architecture;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW025ConfigurationFlowAnalyzer
{
    private sealed class Edge
    {
        internal Edge(IParameterSymbol source, IParameterSymbol target, Location location) { Source = source; Target = target; Location = location; }
        internal IParameterSymbol Source { get; }
        internal IParameterSymbol Target { get; }
        internal Location Location { get; }
    }

    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration)
    {
        context.RegisterSymbolAction(_ => { }, SymbolKind.NamedType);
        context.RegisterCompilationEndAction(c => Analyze(c, configuration));
    }

    private static void Analyze(CompilationAnalysisContext context, HawthorneConfiguration configuration)
    {
        var options = configuration.ConfigurationFlow;
        var edges = new Dictionary<IParameterSymbol, Edge>(SymbolEqualityComparer.Default);
        var consumed = new HashSet<IParameterSymbol>(SymbolEqualityComparer.Default);
        foreach (var tree in context.Compilation.SyntaxTrees)
        {
            var model = context.Compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot(context.CancellationToken).DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol target || target.Parameters.Length == 0) continue;
                var caller = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                if (caller is null || model.GetDeclaredSymbol(caller, context.CancellationToken) is not IMethodSymbol) continue;
                for (var i = 0; i < Math.Min(target.Parameters.Length, invocation.ArgumentList.Arguments.Count); i++)
                {
                    if (invocation.ArgumentList.Arguments[i].Expression is not IdentifierNameSyntax identifier ||
                        model.GetSymbolInfo(identifier, context.CancellationToken).Symbol is not IParameterSymbol source ||
                        !ConfigurationValueClassifier.IsConfigurationType(source.Type, options.ConfigurationTypeSuffixes) || !SymbolEqualityComparer.Default.Equals(source.Type, target.Parameters[i].Type)) continue;
                    edges[source] = new Edge(source, target.Parameters[i], invocation.GetLocation());
                }
            }
            foreach (var memberAccess in tree.GetRoot(context.CancellationToken).DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                if (memberAccess.Expression is IdentifierNameSyntax identifier &&
                    model.GetSymbolInfo(identifier, context.CancellationToken).Symbol is IParameterSymbol parameter &&
                    ConfigurationValueClassifier.IsConfigurationType(parameter.Type, options.ConfigurationTypeSuffixes))
                {
                    consumed.Add(parameter);
                }
            }
        }

        var incoming = new HashSet<IParameterSymbol>(edges.Values.Select(e => e.Target), SymbolEqualityComparer.Default);
        foreach (var edge in edges.Values.Where(e => !incoming.Contains(e.Source)).OrderBy(e => e.Location.SourceTree?.FilePath, StringComparer.Ordinal).ThenBy(e => e.Location.SourceSpan.Start))
        {
            var chain = new List<Edge> { edge };
            var current = edge.Target;
            while (edges.TryGetValue(current, out var next) && !chain.Any(e => SymbolEqualityComparer.Default.Equals(e.Source, next.Source)))
            {
                chain.Add(next);
                current = next.Target;
            }
            if (chain.Count < options.MinimumForwardingHops || chain.Any(e => consumed.Contains(e.Source) || consumed.Contains(e.Target))) continue;
            var properties = ImmutableDictionary<string, string?>.Empty.Add("hops", chain.Count.ToString(CultureInfo.InvariantCulture));
            var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnosticWithEvidence(HawthorneDiagnosticDescriptors.HAW025, edge.Location, configuration, chain.Skip(1).Select(e => e.Location).ToImmutableArray(), properties, edge.Source.Name, chain.Count);
            if (diagnostic is not null)
                context.ReportDiagnostic(diagnostic);
        }
    }
}
