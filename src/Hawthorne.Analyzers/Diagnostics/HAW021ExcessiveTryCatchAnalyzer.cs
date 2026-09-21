using Hawthorne.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hawthorne.Analyzers.Diagnostics;

internal static class HAW021ExcessiveTryCatchAnalyzer
{
    internal static void Register(CompilationStartAnalysisContext context, HawthorneConfiguration configuration)
    {
        context.RegisterSyntaxNodeAction(
            c => AnalyzeCatchAllDefaultReturn((CatchClauseSyntax)c.Node, c, configuration),
            SyntaxKind.CatchClause);
        context.RegisterSymbolAction(
            c => AnalyzeTypeDensity((INamedTypeSymbol)c.Symbol, c, configuration),
            SymbolKind.NamedType);
    }

    private static void AnalyzeCatchAllDefaultReturn(
        CatchClauseSyntax catchClause,
        SyntaxNodeAnalysisContext context,
        HawthorneConfiguration configuration)
    {
        if (!configuration.ExcessiveTryCatch.ReportCatchAllDefaultReturn ||
            catchClause.Filter is not null ||
            catchClause.Declaration?.Type is not { } catchTypeSyntax ||
            !SymbolEqualityComparer.Default.Equals(
                context.SemanticModel.GetTypeInfo(catchTypeSyntax).Type,
                context.SemanticModel.Compilation.GetTypeByMetadataName("System.Exception")) ||
            catchClause.Block.Statements.Count != 1 ||
            catchClause.Block.Statements[0] is not ReturnStatementSyntax { Expression: { } expression } ||
            !TryGetEnclosingMethod(catchClause, context.SemanticModel, out var method) ||
            !IsEquivalentDefaultValue(expression, method.ReturnType, context.SemanticModel))
        {
            return;
        }

        context.ReportHawthorneDiagnostic(
            HawthorneDiagnosticDescriptors.HAW021,
            catchClause.GetLocation(),
            configuration,
            $"catch (Exception) in method '{method.Name}' returns a default value without recovery. Propagate the failure or perform meaningful recovery.");
    }

    private static void AnalyzeTypeDensity(
        INamedTypeSymbol type,
        SymbolAnalysisContext context,
        HawthorneConfiguration configuration)
    {
        if (!type.Locations.Any(location => location.IsInSource) ||
            type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return;
        }

        var methods = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(IsEligibleMethod)
            .ToArray();
        if (methods.Length < configuration.ExcessiveTryCatch.MinimumMethodCount)
        {
            return;
        }

        var tryBlockCount = methods.Sum(CountTryBlocks);
        var density = (double)tryBlockCount / methods.Length;
        if (density <= configuration.ExcessiveTryCatch.MaximumTryBlocksPerMethod)
        {
            return;
        }

        var diagnostic = DiagnosticReportingExtensions.CreateHawthorneDiagnostic(
            HawthorneDiagnosticDescriptors.HAW021,
            type.Locations.First(location => location.IsInSource),
            configuration,
            $"type '{type.Name}' has {density:F2} try blocks per eligible method; maximum allowed is {configuration.ExcessiveTryCatch.MaximumTryBlocksPerMethod:F2}. Handle failures where recovery is possible and remove redundant catch blocks.");
        if (diagnostic is not null)
        {
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsEligibleMethod(IMethodSymbol method) =>
        method.MethodKind == MethodKind.Ordinary &&
        !method.IsAbstract &&
        method.DeclaringSyntaxReferences.Any(reference =>
            reference.GetSyntax() is MethodDeclarationSyntax { Body: not null } or MethodDeclarationSyntax { ExpressionBody: not null });

    private static int CountTryBlocks(IMethodSymbol method) => method.DeclaringSyntaxReferences.Sum(reference =>
    {
        if (reference.GetSyntax() is not MethodDeclarationSyntax declaration)
        {
            return 0;
        }

        return declaration.DescendantNodes()
            .OfType<TryStatementSyntax>()
            .Count(tryStatement => BelongsToMethod(tryStatement, declaration));
    });

    private static bool BelongsToMethod(TryStatementSyntax tryStatement, MethodDeclarationSyntax declaration)
    {
        for (SyntaxNode? ancestor = tryStatement.Parent; ancestor is not null && ancestor != declaration; ancestor = ancestor.Parent)
        {
            if (ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetEnclosingMethod(
        CatchClauseSyntax catchClause,
        SemanticModel semanticModel,
        out IMethodSymbol method)
    {
        method = null!;
        var declaration = catchClause.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (declaration is null || semanticModel.GetDeclaredSymbol(declaration) is not IMethodSymbol declaredMethod)
        {
            return false;
        }

        method = declaredMethod;
        return true;
    }

    private static bool IsEquivalentDefaultValue(
        ExpressionSyntax expression,
        ITypeSymbol returnType,
        SemanticModel semanticModel)
    {
        if (expression is DefaultExpressionSyntax || expression.IsKind(SyntaxKind.DefaultLiteralExpression))
        {
            return true;
        }

        if (expression.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return returnType.IsReferenceType || returnType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        }

        var constant = semanticModel.GetConstantValue(expression);
        if (!constant.HasValue)
        {
            return false;
        }

        if (returnType.TypeKind == TypeKind.Enum)
        {
            return IsZero(constant.Value);
        }

        return returnType.SpecialType switch
        {
            SpecialType.System_Boolean => constant.Value is false,
            SpecialType.System_Char => constant.Value is char character && character == '\0',
            SpecialType.System_SByte or SpecialType.System_Byte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 or
            SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int64 or SpecialType.System_UInt64 or
            SpecialType.System_Single or SpecialType.System_Double or
            SpecialType.System_Decimal => IsZero(constant.Value),
            _ => false,
        };
    }

    private static bool IsZero(object? value)
    {
        try
        {
            return value is not null && Convert.ToDecimal(value) == decimal.Zero;
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }
}
