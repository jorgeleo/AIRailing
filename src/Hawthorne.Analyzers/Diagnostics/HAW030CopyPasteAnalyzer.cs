using System.Collections.Immutable;
using Hawthorne.Analyzers.Analysis.Architecture;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW030CopyPasteAnalyzer
{
    private sealed class Candidate
    {
        internal Candidate(IMethodSymbol symbol, MethodDeclarationSyntax declaration, IReadOnlyDictionary<int, int> shape, int statements) { Symbol = symbol; Declaration = declaration; Shape = shape; Statements = statements; }
        internal IMethodSymbol Symbol { get; }
        internal MethodDeclarationSyntax Declaration { get; }
        internal IReadOnlyDictionary<int, int> Shape { get; }
        internal int Statements { get; }
    }

    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration)
    {
        context.RegisterSymbolAction(_ => { }, SymbolKind.NamedType);
        context.RegisterCompilationEndAction(c => Analyze(c, configuration));
    }

    private static void Analyze(CompilationAnalysisContext context, HawthorneConfiguration configuration)
    {
        var options = configuration.CopyPaste;
        var candidates = new List<Candidate>();
        foreach (var tree in context.Compilation.SyntaxTrees)
        {
            var model = context.Compilation.GetSemanticModel(tree);
            foreach (var method in tree.GetRoot(context.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (method.Body is null || method.Body.Statements.Count < options.MinimumStatements || model.GetDeclaredSymbol(method, context.CancellationToken) is not IMethodSymbol symbol || symbol.IsOverride || ForwardingMethodClassifier.IsRequiredContractMethod(symbol)) continue;
                candidates.Add(new Candidate(symbol, method, BuildShape(method), method.Body.Statements.Count));
            }
        }
        var used = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        foreach (var candidate in candidates.OrderBy(c => c.Declaration.SyntaxTree.FilePath, StringComparer.Ordinal).ThenBy(c => c.Declaration.SpanStart))
        {
            if (used.Contains(candidate.Symbol)) continue;
            var cluster = candidates.Where(other => !SymbolEqualityComparer.Default.Equals(other.Symbol, candidate.Symbol) && Similarity(candidate.Shape, other.Shape) >= options.MinimumSimilarity).ToList();
            if (cluster.Count + 1 < options.MinimumMethods) continue;
            var all = new[] { candidate }.Concat(cluster).OrderBy(c => c.Declaration.SyntaxTree.FilePath, StringComparer.Ordinal).ThenBy(c => c.Declaration.SpanStart).ToArray();
            foreach (var member in all) used.Add(member.Symbol);
            var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnosticWithEvidence(HawthorneDiagnosticDescriptors.HAW030, all[0].Declaration.Identifier.GetLocation(), configuration, all.Skip(1).Select(m => m.Declaration.Identifier.GetLocation()).ToImmutableArray(), null, all[0].Symbol.Name, all.Length);
            if (diagnostic is not null) context.ReportDiagnostic(diagnostic);
        }
    }

    private static IReadOnlyDictionary<int, int> BuildShape(MethodDeclarationSyntax method) =>
        method.Body!.DescendantNodesAndSelf().Where(n => n is not IdentifierNameSyntax && !n.IsKind(SyntaxKind.NumericLiteralExpression) && !n.IsKind(SyntaxKind.StringLiteralExpression)).GroupBy(n => n.RawKind).ToDictionary(g => g.Key, g => g.Count());

    private static double Similarity(IReadOnlyDictionary<int, int> left, IReadOnlyDictionary<int, int> right)
    {
        var intersection = left.Keys.Union(right.Keys).Sum(key => Math.Min(left.TryGetValue(key, out var l) ? l : 0, right.TryGetValue(key, out var r) ? r : 0));
        var union = left.Keys.Union(right.Keys).Sum(key => Math.Max(left.TryGetValue(key, out var l) ? l : 0, right.TryGetValue(key, out var r) ? r : 0));
        return union == 0 ? 0 : intersection / (double)union;
    }
}
