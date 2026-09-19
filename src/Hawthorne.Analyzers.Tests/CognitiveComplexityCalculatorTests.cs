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
}
