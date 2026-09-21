using Hawthorne.Analyzers.Analysis.Complexity;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class CognitiveComplexityCalculatorTests
{
    [Fact]
    public void Calculate_PenalizesNestedControlFlow()
    {
        var method = CSharpSyntaxTree.ParseText("class C { void M() { if (true) { for (;;) { while (true) { } } } } }")
            .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();

        Assert.Equal(6, CognitiveComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_WhenMethodIsExpressionBodied_ScoresNestedConditionals()
    {
        var method = CSharpSyntaxTree.ParseText("class C { int M(int a, int b) => a > 1 ? (b > 2 ? (a + b > 3 ? 1 : 2) : 3) : 4; }")
            .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();

        Assert.Equal(6, CognitiveComplexityCalculator.Calculate(method));
    }
}
