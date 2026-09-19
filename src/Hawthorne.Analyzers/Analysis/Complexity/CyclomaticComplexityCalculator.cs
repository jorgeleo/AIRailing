using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;

namespace Hawthorne.Analyzers.Analysis.Complexity;

internal static class CyclomaticComplexityCalculator
{
    internal static int Calculate(MethodDeclarationSyntax method)
    {
        var visitor = new Visitor();
        visitor.Visit(method.Body ?? (SyntaxNode?)method.ExpressionBody);
        return visitor.Complexity;
    }

    private sealed class Visitor : CSharpSyntaxWalker
    {
        internal int Complexity { get; private set; } = 1;
        public override void VisitIfStatement(IfStatementSyntax node) => AddAndVisit(node);
        public override void VisitForStatement(ForStatementSyntax node) => AddAndVisit(node);
        public override void VisitForEachStatement(ForEachStatementSyntax node) => AddAndVisit(node);
        public override void VisitWhileStatement(WhileStatementSyntax node) => AddAndVisit(node);
        public override void VisitDoStatement(DoStatementSyntax node) => AddAndVisit(node);
        public override void VisitCatchClause(CatchClauseSyntax node) => AddAndVisit(node);
        public override void VisitConditionalExpression(ConditionalExpressionSyntax node) => AddAndVisit(node);
        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.LogicalAndExpression) || node.IsKind(SyntaxKind.LogicalOrExpression) || node.IsKind(SyntaxKind.CoalesceExpression)) Complexity++;
            base.VisitBinaryExpression(node);
        }
        public override void VisitSwitchSection(SwitchSectionSyntax node)
        {
            Complexity += node.Labels.Count(label => label is CaseSwitchLabelSyntax or CasePatternSwitchLabelSyntax);
            base.VisitSwitchSection(node);
        }
        private void AddAndVisit(SyntaxNode node) { Complexity++; base.DefaultVisit(node); }
    }
}
