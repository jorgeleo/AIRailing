using Hawthorne.Analyzers.Analysis.Complexity;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class CyclomaticComplexityCalculatorTests
{
    [Fact]
    public void Calculate_CountsBranchesAndShortCircuitOperators()
    {
        var method = CSharpSyntaxTree.ParseText("class C { void M(bool a, bool b) { if (a && b) { } else if (a ?? b) { } } }")
            .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        Assert.Equal(5, CyclomaticComplexityCalculator.Calculate(method));
    }
}
