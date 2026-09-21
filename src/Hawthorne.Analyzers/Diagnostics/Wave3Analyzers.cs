using System.Collections.Immutable;
using System.Globalization;
using Hawthorne.Analyzers.Analysis.Architecture;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW020RepositoryLayerAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSymbolAction(c => Analyze((INamedTypeSymbol)c.Symbol, c, configuration), SymbolKind.NamedType);

    private static void Analyze(INamedTypeSymbol type, SymbolAnalysisContext context, HawthorneConfiguration configuration)
    {
        var options = configuration.RepositoryLayer;
        if (!type.Locations.Any(l => l.IsInSource) || type.TypeKind != TypeKind.Class ||
            (options.RequireRepositorySuffix && !options.HasRepositorySuffix(type.Name)))
        {
            return;
        }

        var eligible = type.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && m.DeclaredAccessibility == Accessibility.Public && !m.IsOverride && m.DeclaringSyntaxReferences.Length > 0)
            .ToArray();
        if (eligible.Length == 0) return;

        var forwarded = new Dictionary<ISymbol, int>(SymbolEqualityComparer.Default);
        foreach (var method in eligible)
        {
            if (method.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken) is not MethodDeclarationSyntax declaration ||
                !ForwardingMethodClassifier.TryGetForwardedDependency(declaration, context.Compilation.GetSemanticModel(declaration.SyntaxTree), out var dependency) ||
                dependency is null || !IsEntityFrameworkDependency(dependency))
            {
                continue;
            }

            forwarded[dependency] = forwarded.TryGetValue(dependency, out var count) ? count + 1 : 1;
        }

        var dominant = forwarded.OrderByDescending(p => p.Value).ThenBy(p => p.Key.Name, StringComparer.Ordinal).FirstOrDefault();
        var ratio = dominant.Value / (double)eligible.Length;
        if (dominant.Key is null || dominant.Value < options.MinimumForwardingMethods || ratio < options.MinimumForwardingRatio) return;

        var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(
            HawthorneDiagnosticDescriptors.HAW020,
            type.Locations.First(l => l.IsInSource),
            configuration,
            type.Name,
            ratio,
            dominant.Key.Name);
        if (diagnostic is not null) context.ReportDiagnostic(diagnostic);
    }

    private static bool IsEntityFrameworkDependency(ISymbol dependency)
    {
        var type = dependency switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            IParameterSymbol parameter => parameter.Type,
            _ => null,
        };
        return TypeRoleClassifier.IsEntityFrameworkType(type);
    }
}

internal static class HAW023DeadExtensionPointAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration)
    {
        context.RegisterSymbolAction(_ => { }, SymbolKind.NamedType);
        context.RegisterCompilationEndAction(c => Analyze(c, configuration));
    }

    private static void Analyze(CompilationAnalysisContext context, HawthorneConfiguration configuration)
    {
        var options = configuration.DeadExtensionPoints;
        if (!options.IncludePrivateMembers && !options.IncludeExternallyAccessibleMembers) return;

        var candidates = new List<(ISymbol Symbol, Location Location)>();
        foreach (var tree in context.Compilation.SyntaxTrees)
        {
            var model = context.Compilation.GetSemanticModel(tree);
            foreach (var eventDeclaration in tree.GetRoot(context.CancellationToken).DescendantNodes().OfType<EventDeclarationSyntax>())
            {
                if (!options.AnalyzeEvents || model.GetDeclaredSymbol(eventDeclaration, context.CancellationToken) is not IEventSymbol symbol || !IsCandidate(symbol, options)) continue;
                candidates.Add((symbol, eventDeclaration.Identifier.GetLocation()));
            }
            foreach (var field in tree.GetRoot(context.CancellationToken).DescendantNodes().OfType<EventFieldDeclarationSyntax>())
            {
                if (!options.AnalyzeEvents) continue;
                foreach (var variable in field.Declaration.Variables)
                {
                    if (model.GetDeclaredSymbol(variable, context.CancellationToken) is IEventSymbol symbol && IsCandidate(symbol, options))
                        candidates.Add((symbol, variable.Identifier.GetLocation()));
                }
            }
            foreach (var property in tree.GetRoot(context.CancellationToken).DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                if (!options.AnalyzeCallbacks || model.GetDeclaredSymbol(property, context.CancellationToken) is not IPropertySymbol symbol || !IsCandidate(symbol, options) || symbol.Type.TypeKind != TypeKind.Delegate) continue;
                candidates.Add((symbol, property.Identifier.GetLocation()));
            }
            foreach (var method in tree.GetRoot(context.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (!options.AnalyzeVirtualHooks || model.GetDeclaredSymbol(method, context.CancellationToken) is not IMethodSymbol symbol || !symbol.IsVirtual || !IsCandidate(symbol, options)) continue;
                if (symbol.DeclaredAccessibility == Accessibility.Protected && !symbol.ContainingType.IsSealed && HasSourceDerivedType(symbol.ContainingType, context.Compilation)) continue;
                candidates.Add((symbol, method.Identifier.GetLocation()));
            }
        }

        var used = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var tree in context.Compilation.SyntaxTrees)
        {
            var model = context.Compilation.GetSemanticModel(tree);
            foreach (var identifier in tree.GetRoot(context.CancellationToken).DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var symbol = model.GetSymbolInfo(identifier, context.CancellationToken).Symbol;
                if (symbol is not null) used.Add(symbol);
            }
        }

        foreach (var candidate in candidates.GroupBy(c => c.Symbol, SymbolEqualityComparer.Default).Select(group => group.First()))
        {
            if (used.Contains(candidate.Symbol)) continue;
            var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW023, candidate.Location, configuration, candidate.Symbol.Name);
            if (diagnostic is not null) context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsCandidate(ISymbol symbol, Hawthorne023Configuration options) =>
        options.IncludeExternallyAccessibleMembers ||
        (options.IncludePrivateMembers && (symbol.DeclaredAccessibility == Accessibility.Private ||
            (symbol.DeclaredAccessibility == Accessibility.Protected && symbol.ContainingType?.IsSealed == true)));

    private static bool HasSourceDerivedType(INamedTypeSymbol type, Compilation compilation) =>
        compilation.Assembly.GlobalNamespace.GetNamespaceTypes().Any(candidate => candidate.BaseType is not null && SymbolEqualityComparer.Default.Equals(candidate.BaseType, type));
}

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

internal static class HAW029LoggingNoiseAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSymbolAction(c => Analyze((INamedTypeSymbol)c.Symbol, c, configuration), SymbolKind.NamedType);

    private static void Analyze(INamedTypeSymbol type, SymbolAnalysisContext context, HawthorneConfiguration configuration)
    {
        var options = configuration.LoggingNoise;
        if (!type.Locations.Any(l => l.IsInSource)) return;
        var methods = type.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary && !m.IsAbstract && m.DeclaringSyntaxReferences.Length > 0).ToArray();
        if (methods.Length < options.MinimumMethodCount) return;
        var lifecycle = methods.Count(m => HasLifecycleLog(m, context.Compilation, options, context.CancellationToken));
        var ratio = lifecycle / (double)methods.Length;
        if (ratio < options.MaximumLifecycleLogRatio) return;
        var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW029, type.Locations.First(l => l.IsInSource), configuration, type.Name, ratio);
        if (diagnostic is not null) context.ReportDiagnostic(diagnostic);
    }

    private static bool HasLifecycleLog(IMethodSymbol method, Compilation compilation, Hawthorne029Configuration options, CancellationToken cancellationToken)
    {
        var declaration = (MethodDeclarationSyntax)method.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken);
        var model = compilation.GetSemanticModel(declaration.SyntaxTree);
        foreach (var invocation in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol target || !LoggingClassifier.IsLifecycleMethod(target)) continue;
            if (invocation.Expression is not MemberAccessExpressionSyntax access || !LoggingClassifier.IsLoggerType(model.GetTypeInfo(access.Expression, cancellationToken).Type, options.LoggerTypeNames)) continue;
            if (invocation.ArgumentList.Arguments.Count == 0 || model.GetConstantValue(invocation.ArgumentList.Arguments[0].Expression, cancellationToken) is not { HasValue: true, Value: string template } ||
                !LoggingClassifier.IsLifecycleTemplate(template, options.LifecycleTerms)) continue;
            if (invocation.ArgumentList.Arguments.Skip(1).Any(argument => IsException(model.GetTypeInfo(argument.Expression, cancellationToken).Type, compilation))) continue;
            return true;
        }
        return false;
    }

    private static bool IsException(ITypeSymbol? type, Compilation compilation)
    {
        var exception = compilation.GetTypeByMetadataName("System.Exception");
        return type is not null && exception is not null && (SymbolEqualityComparer.Default.Equals(type, exception) || type.InheritsFrom(exception));
    }
}

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

internal static class HAW100AbstractionDensityAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration)
    {
        context.RegisterSymbolAction(_ => { }, SymbolKind.NamedType);
        context.RegisterCompilationEndAction(c => Analyze(c, configuration));
    }

    private static void Analyze(CompilationAnalysisContext context, HawthorneConfiguration configuration)
    {
        var options = configuration.AbstractionDensity;
        var abstractions = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var behavioral = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        Location? first = null;
        foreach (var tree in context.Compilation.SyntaxTrees)
        {
            var model = context.Compilation.GetSemanticModel(tree);
            foreach (var declaration in tree.GetRoot(context.CancellationToken).DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol type || !type.Locations.Any(l => l.IsInSource)) continue;
                first ??= declaration.Identifier.GetLocation();
                if (type.TypeKind == TypeKind.Interface || type.IsAbstract || options.AbstractionRoleSuffixes.Any(suffix => type.Name.EndsWith(suffix, StringComparison.Ordinal))) abstractions.Add(type);
                if (type.TypeKind == TypeKind.Class && !type.IsAbstract && type.GetMembers().OfType<IMethodSymbol>().Any(m => m.MethodKind == MethodKind.Ordinary && !m.IsAbstract && m.DeclaringSyntaxReferences.Length > 0)) behavioral.Add(type);
            }
        }
        if (first is null || behavioral.Count < options.MinimumBehavioralTypes) return;
        var density = abstractions.Count / (double)behavioral.Count;
        if (options.MaximumDensity is double maximum && density <= maximum) return;
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add("abstractionCount", abstractions.Count.ToString(CultureInfo.InvariantCulture))
            .Add("behavioralCount", behavioral.Count.ToString(CultureInfo.InvariantCulture))
            .Add("density", density.ToString("F2", CultureInfo.InvariantCulture));
        var withEvidence = DiagnosticReportingExtensions.CreateHawthorneDiagnosticWithEvidence(HawthorneDiagnosticDescriptors.HAW100, first, configuration, ImmutableArray<Location>.Empty, properties, density, abstractions.Count, behavioral.Count);
        if (withEvidence is not null) context.ReportDiagnostic(withEvidence);
    }
}

internal static class Wave3SymbolExtensions
{
    internal static IEnumerable<INamedTypeSymbol> GetNamespaceTypes(this INamespaceSymbol namespaceSymbol)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers()) yield return type;
        foreach (var child in namespaceSymbol.GetNamespaceMembers())
            foreach (var type in child.GetNamespaceTypes()) yield return type;
    }

    internal static bool InheritsFrom(this ITypeSymbol type, ITypeSymbol baseType)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current, baseType)) return true;
        return false;
    }
}
