using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW002TrivialFactoryAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration) =>
        context.RegisterSyntaxNodeAction(c => Analyze((ClassDeclarationSyntax)c.Node, c, configuration), SyntaxKind.ClassDeclaration);

    private static void Analyze(ClassDeclarationSyntax factory, SyntaxNodeAnalysisContext context, HawthorneConfiguration configuration)
    {
        if (!factory.Identifier.ValueText.EndsWith("Factory", StringComparison.Ordinal)) return;
        foreach (var method in factory.Members.OfType<MethodDeclarationSyntax>())
        {
            var creation = GetSingleCreation(method);
            if (creation is null) continue;
            var type = context.SemanticModel.GetTypeInfo(creation).Type;
            if (type is null) continue;
            context.ReportHawthorneDiagnostic(HawthorneDiagnosticDescriptors.HAW002, method.Identifier.GetLocation(), configuration,
                factory.Identifier.ValueText, type.Name);
        }
    }

    private static ExpressionSyntax? GetSingleCreation(MethodDeclarationSyntax method)
    {
        var expression = method.ExpressionBody?.Expression ??
            (method.Body?.Statements.Count == 1 && method.Body.Statements[0] is ReturnStatementSyntax returned ? returned.Expression : null);
        // BaseObjectCreationExpressionSyntax covers both `new T()` and target-typed `new()` (ImplicitObjectCreationExpressionSyntax in Roslyn 4.14+).
        return expression is BaseObjectCreationExpressionSyntax { Initializer: null } ? expression : null;
    }
}
