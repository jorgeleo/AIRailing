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
}
