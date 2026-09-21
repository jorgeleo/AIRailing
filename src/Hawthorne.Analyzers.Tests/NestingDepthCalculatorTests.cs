using Hawthorne.Analyzers.Analysis.Complexity;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class NestingDepthCalculatorTests
{
    [Fact]
    public void Calculate_CountsSemanticControlFlowButNotOrdinaryBlocks()
    {
        var method = CSharpSyntaxTree.ParseText("""
            class Example { void M() { { if (true) { for (;;) { while (true) { } } } } } }
            """).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();

        Assert.Equal(3, NestingDepthCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_WhenMethodIsExpressionBodied_CountsNestedLambdaControlFlow()
    {
        var method = CSharpSyntaxTree.ParseText("""
            using System.Collections.Generic;
            class C { void M(List<int> values) => values.ForEach(value => { if (value > 0) { if (value > 1) { } } }); }
            """).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        Assert.Equal(2, NestingDepthCalculator.Calculate(method));
    }
}
