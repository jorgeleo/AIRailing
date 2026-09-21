using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Hawthorne.Analyzers.Analysis.Complexity;

internal static class CognitiveComplexityCalculator
{
    internal static int Calculate(MethodDeclarationSyntax method)
    {
        var visitor = new Visitor();
        visitor.Visit(method.Body ?? (SyntaxNode?)method.ExpressionBody);
        return visitor.Score;
    }

    private sealed class Visitor : CSharpSyntaxWalker
    {
        private int nesting;
        internal int Score { get; private set; }
        public override void VisitIfStatement(IfStatementSyntax node) => AddAndVisit(node);
        public override void VisitForStatement(ForStatementSyntax node) => AddAndVisit(node);
        public override void VisitForEachStatement(ForEachStatementSyntax node) => AddAndVisit(node);
        public override void VisitWhileStatement(WhileStatementSyntax node) => AddAndVisit(node);
        public override void VisitDoStatement(DoStatementSyntax node) => AddAndVisit(node);
        public override void VisitCatchClause(CatchClauseSyntax node) => AddAndVisit(node);
        public override void VisitConditionalExpression(ConditionalExpressionSyntax node) => AddAndVisit(node);
        public override void VisitSwitchStatement(SwitchStatementSyntax node)
        {
            Score++;
            nesting++;
            base.DefaultVisit(node);
            nesting--;
        }
        private void AddAndVisit(SyntaxNode node)
        {
            Score += 1 + nesting;
            nesting++;
            base.DefaultVisit(node);
            nesting--;
        }
    }
}
