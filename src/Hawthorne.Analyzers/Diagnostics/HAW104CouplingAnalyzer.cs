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
        var dependencies = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        Add(type.BaseType, type, dependencies);
        foreach (var item in type.Interfaces) Add(item, type, dependencies);
        foreach (var member in type.GetMembers())
        {
            if (member is IFieldSymbol field) Add(field.Type, type, dependencies);
            if (member is IPropertySymbol property) Add(property.Type, type, dependencies);
            if (member is IMethodSymbol method)
            {
                Add(method.ReturnType, type, dependencies);
                foreach (var parameter in method.Parameters) Add(parameter.Type, type, dependencies);
            }
        }

        foreach (var declaration in type.DeclaringSyntaxReferences)
        {
            var model = context.Compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (var node in declaration.GetSyntax().DescendantNodes().Where(node => node is Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax))
            {
                var operation = model.GetOperation(node);
                if (operation is not null) CollectOperations(operation, type, dependencies);
            }
        }

        if (dependencies.Count > configuration.MaximumClassCoupling)
        {
            var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW104,
                type.Locations.FirstOrDefault() ?? Location.None, configuration, type.Name, dependencies.Count, configuration.MaximumClassCoupling);
            if (diagnostic is not null) context.ReportDiagnostic(diagnostic);
        }
    }

    private static void CollectOperations(IOperation operation, INamedTypeSymbol owner, HashSet<INamedTypeSymbol> dependencies)
    {
        if (operation is IObjectCreationOperation creation) Add(creation.Type, owner, dependencies);
        if (operation is IInvocationOperation invocation) Add(invocation.TargetMethod.ContainingType, owner, dependencies);
        if (operation is IVariableDeclaratorOperation local) Add(local.Symbol.Type, owner, dependencies);
        foreach (var child in operation.ChildOperations) CollectOperations(child, owner, dependencies);
    }

    private static void Add(ITypeSymbol? symbol, INamedTypeSymbol owner, HashSet<INamedTypeSymbol> dependencies)
    {
        if (symbol is not INamedTypeSymbol named || SymbolEqualityComparer.Default.Equals(named, owner) || named.SpecialType != SpecialType.None) return;
        if (named.Name is "Task" or "ValueTask" or "List" or "IEnumerable" or "ICollection" or "Dictionary" or "Nullable")
            foreach (var argument in named.TypeArguments) Add(argument, owner, dependencies);
        else dependencies.Add(named);
    }
}
