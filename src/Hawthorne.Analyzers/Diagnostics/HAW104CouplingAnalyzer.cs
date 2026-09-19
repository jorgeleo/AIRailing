using Hawthorne.Analyzers.Analysis.Coupling;
using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW104CouplingAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSymbolAction(c => Analyze((INamedTypeSymbol)c.Symbol, c, configuration), SymbolKind.NamedType);

    private static void Analyze(INamedTypeSymbol type, SymbolAnalysisContext context, HawthorneConfiguration configuration)
    {
        if (type.TypeKind != TypeKind.Class) return;
        var dependencies = new CouplingInventory();
        dependencies.Add(type.BaseType, type, CouplingSource.ApiSurface);
        foreach (var item in type.Interfaces) dependencies.Add(item, type, CouplingSource.ApiSurface);
        foreach (var member in type.GetMembers())
        {
            if (member is IFieldSymbol field) dependencies.Add(field.Type, type, CouplingSource.StateOrDependency);
            if (member is IPropertySymbol property)
                dependencies.Add(property.Type, type, IsApiSurface(property) ? CouplingSource.ApiSurface : CouplingSource.StateOrDependency);
            if (member is IMethodSymbol method)
            {
                var source = method.MethodKind == MethodKind.Constructor
                    ? CouplingSource.StateOrDependency
                    : IsApiSurface(method) ? CouplingSource.ApiSurface : CouplingSource.Implementation;
                dependencies.Add(method.ReturnType, type, source);
                foreach (var parameter in method.Parameters) dependencies.Add(parameter.Type, type, source);
            }
        }

        foreach (var declaration in type.DeclaringSyntaxReferences)
        {
            var model = context.Compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (var node in declaration.GetSyntax().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>())
            {
                if (!SymbolEqualityComparer.Default.Equals(model.GetDeclaredSymbol(node)?.ContainingType, type)) continue;
                var operation = model.GetOperation(node);
                if (operation is not null) CollectOperations(operation, type, dependencies);
            }
        }

        if (dependencies.Count > configuration.MaximumClassCoupling)
        {
            var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW104,
                type.Locations.FirstOrDefault() ?? Location.None, configuration, type.Name, dependencies.Count, configuration.MaximumClassCoupling,
                dependencies.CountBySource(CouplingSource.ApiSurface),
                dependencies.CountBySource(CouplingSource.StateOrDependency),
                dependencies.CountBySource(CouplingSource.Implementation),
                dependencies.FormatExamples());
            if (diagnostic is not null) context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsApiSurface(ISymbol member) =>
        member.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal;

    private static void CollectOperations(IOperation operation, INamedTypeSymbol owner, CouplingInventory dependencies)
    {
        if (operation is IObjectCreationOperation creation) dependencies.Add(creation.Type, owner, CouplingSource.Implementation);
        if (operation is IInvocationOperation invocation) dependencies.Add(invocation.TargetMethod.ContainingType, owner, CouplingSource.Implementation);
        if (operation is IVariableDeclaratorOperation local) dependencies.Add(local.Symbol.Type, owner, CouplingSource.Implementation);
        foreach (var child in operation.ChildOperations) CollectOperations(child, owner, dependencies);
    }
}
