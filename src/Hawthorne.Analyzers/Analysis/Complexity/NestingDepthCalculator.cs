using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;

namespace Hawthorne.Analyzers.Analysis.Complexity;

internal static class NestingDepthCalculator
{
    internal static int Calculate(MethodDeclarationSyntax method)
    {
        var visitor = new Visitor();
        visitor.Visit(method.Body);
        return visitor.MaximumDepth;
    }

    private sealed class Visitor : CSharpSyntaxWalker
    {
        private int currentDepth;
        internal int MaximumDepth { get; private set; }

        public override void VisitIfStatement(IfStatementSyntax node) => VisitNested(node);
        public override void VisitForStatement(ForStatementSyntax node) => VisitNested(node);
        public override void VisitForEachStatement(ForEachStatementSyntax node) => VisitNested(node);
        public override void VisitWhileStatement(WhileStatementSyntax node) => VisitNested(node);
        public override void VisitDoStatement(DoStatementSyntax node) => VisitNested(node);
        public override void VisitSwitchStatement(SwitchStatementSyntax node) => VisitNested(node);
        public override void VisitTryStatement(TryStatementSyntax node) => VisitNested(node);
        public override void VisitCatchClause(CatchClauseSyntax node) => VisitNested(node);
        public override void VisitLockStatement(LockStatementSyntax node) => VisitNested(node);
        public override void VisitUsingStatement(UsingStatementSyntax node) => VisitNested(node);

        private void VisitNested(SyntaxNode node)
        {
            currentDepth++;
            MaximumDepth = Math.Max(MaximumDepth, currentDepth);
            base.DefaultVisit(node);
            currentDepth--;
        }
    }
}
