using System.Collections;
using System.Collections.Generic;
using PromQL.Parser.Ast;

namespace PromQL.Parser
{
    /// <summary>
    /// A depth-first visitor that can find all descendant <see cref="Expr"/> nodes from a given <see cref="Expr"/>.
    /// </summary>
    public class DepthFirstExpressionVisitor : IVisitor
    {
        private List<Expr> _expressions = new();

        void IVisitor.Visit(StringLiteral expr) => _expressions.Add(expr);

        void IVisitor.Visit(SubqueryExpr sq)
        {
            _expressions.Add(sq);
            sq.Expr.Accept(this);
        }

        void IVisitor.Visit(Duration d) { }

        void IVisitor.Visit(NumberLiteral n) => _expressions.Add(n);

        void IVisitor.Visit(MetricIdentifier mi) { }

        void IVisitor.Visit(LabelMatcher expr) { }

        void IVisitor.Visit(UnaryExpr unary)
        {
            _expressions.Add(unary);
            unary.Expr.Accept(this);
        }

        void IVisitor.Visit(MatrixSelector ms)
        {
            _expressions.Add(ms);
            // No need to visit vector selector, it's accessible from matrix selector
        }

        void IVisitor.Visit(OffsetExpr offset)
        {
            _expressions.Add(offset);
            offset.Expr.Accept(this);
        }

        void IVisitor.Visit(ParenExpression paren)
        {
            _expressions.Add(paren);
            paren.Expr.Accept(this);
        }

        void IVisitor.Visit(FunctionCall fnCall)
        {
            _expressions.Add(fnCall);
            foreach (var a in fnCall.Args)
                a.Accept(this);
        }

        void IVisitor.Visit(VectorMatching vm) { }

        void IVisitor.Visit(BinaryExpr expr)
        {
            _expressions.Add(expr);
            expr.LeftHandSide.Accept(this);
            expr.RightHandSide.Accept(this);
        }

        void IVisitor.Visit(AggregateExpr expr)
        {
            _expressions.Add(expr);
            expr.Param?.Accept(this);
            expr.Expr.Accept(this);
        }

        void IVisitor.Visit(VectorSelector vs) => _expressions.Add(vs);

        void IVisitor.Visit(LabelMatchers lms) { }

        public IEnumerable<Expr> GetExpressions(Expr expr)
        {
            _expressions.Clear();
            expr.Accept(this);
            return _expressions;
        }
    }
}