using System;
using System.Text;
using PromQL.Parser.Ast;

namespace PromQL.Parser
{
    public class Printer : IVisitor
    {
        private StringBuilder _sb = new ();

        public virtual void Visit(StringLiteral s)
        {
            // Raw strings are denoted by ` and should not have their values escaped
            if (s.Quote == '`')
            {
                _sb.Append($"{s.Quote}{s.Value}{s.Quote}");
                return;
            }
            
            // TODO this both duplicates knowledge in the Parser and is inefficient, can do better
            var escaped = s.Value
                .Replace("\\", "\\\\")
                .Replace($"{s.Quote}", $"\\{s.Quote}")
                .Replace("\a", "\\a")
                .Replace("\b", "\\b")
                .Replace("\f", "\\f")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t")
                .Replace("\v", "\\v");
            
            _sb.Append($"{s.Quote}{escaped}{s.Quote}");
        }

        public virtual void Visit(SubqueryExpr sq)
        {
            sq.Expr.Accept(this);
            _sb.Append("[");
            sq.Range.Accept(this);
            _sb.Append(":");
            sq.Step?.Accept(this);
            _sb.Append("]");
        }

        public virtual void Visit(Duration d)
        {
            if (d.Value.Days > 0)
                _sb.Append($"{d.Value.Days}d");
            if (d.Value.Hours > 0)
                _sb.Append($"{d.Value.Hours}h");
            if (d.Value.Minutes > 0)
                _sb.Append($"{d.Value.Minutes}m");
            if (d.Value.Seconds > 0)
                _sb.Append($"{d.Value.Seconds}s");
            if (d.Value.Milliseconds > 0)
                _sb.Append($"{d.Value.Milliseconds}ms");
        }

        public virtual void Visit(NumberLiteral n) => _sb.Append(n.Value switch
        {
            double.PositiveInfinity => "Inf",
            double.NegativeInfinity => "-Inf",
            _ => n.Value.ToString()
        });

        public virtual void Visit(MetricIdentifier mi) => _sb.Append(mi.Value);

        public virtual void Visit(LabelMatcher lm)
        {
            _sb.Append($"{lm.LabelName}{lm.Operator.ToPromQl()}");
            lm.Value.Accept(this);
        }

        public virtual void Visit(LabelMatchers lms)
        {
            bool first = true;
            _sb.Append("{");
            foreach (var lm in lms.Matchers)
            {
                if (!first)
                    _sb.Append(", ");
                
                lm.Accept(this);
                first = false;
            }
            _sb.Append("}");
        }

        public virtual void Visit(VectorSelector vs)
        {
            vs.MetricIdentifier?.Accept(this);
            vs.LabelMatchers?.Accept(this);
        }

        public virtual void Visit(UnaryExpr unary)
        {
            _sb.Append(unary.Operator.ToPromQl());
            unary.Expr.Accept(this);
        }

        public virtual void Visit(MatrixSelector ms)
        {
            ms.Vector.Accept(this);
            _sb.Append("[");
            ms.Duration.Accept(this);
            _sb.Append("]");
        }

        public virtual void Visit(OffsetExpr offset)
        {
            offset.Expr.Accept(this);
            _sb.Append(" offset ");

            Duration d = offset.Duration;

            if (d.Value < TimeSpan.Zero)
            {
                // Negative durations cannot be printed by the duration visitor. Convert to positive and emit sign here.
                d = d with { Value = new TimeSpan(Math.Abs(d.Value.Ticks))};
                _sb.Append("-");
            }
            
            d.Accept(this);
        }

        public virtual void Visit(ParenExpression paren)
        {
            _sb.Append("(");
            paren.Expr.Accept(this);
            _sb.Append(")");
        }

        public virtual void Visit(FunctionCall fnCall)
        {
            _sb.Append($"{fnCall.Function.Name}(");

            bool isFirst = true;
            foreach (var arg in fnCall.Args)
            {
                if (!isFirst)
                    _sb.Append(", ");
                
                arg.Accept(this);
                isFirst = false;
            }
            
            _sb.Append(")");
        }

        public virtual void Visit(VectorMatching vm)
        {
            if (vm.ReturnBool)
                _sb.Append("bool");

            if (vm.On || vm.MatchingLabels.Length > 0)
            {
                if (_sb.Length > 0)
                    _sb.Append(" ");
                
                _sb.Append(vm.On ? "on" : "ignoring");
                _sb.Append(" (");
                _sb.Append(string.Join(", ", vm.MatchingLabels));
                _sb.Append(")");
            }

            if (vm.MatchCardinality != VectorMatching.DefaultMatchCardinality)
            {
                if (_sb.Length > 0)
                    _sb.Append(" ");
                
                _sb.Append(vm.MatchCardinality.ToPromQl());
            }

            if (vm.Include.Length > 0)
            {
                if (_sb.Length > 0)
                    _sb.Append(" ");
                
                _sb.Append("(");
                _sb.Append(string.Join(", ", vm.Include));
                _sb.Append(")");
            }
        }

        public virtual void Visit(BinaryExpr expr)
        {
            expr.LeftHandSide.Accept(this);
            
            _sb.Append($" {expr.Operator.ToPromQl()} ");

            var preLen = _sb.Length;
            expr.VectorMatching?.Accept(this);

            if (_sb.Length > preLen)
                _sb.Append(" ");
            
            expr.RightHandSide.Accept(this);
        }

        public virtual void Visit(AggregateExpr expr)
        {
            _sb.Append($"{expr.OperatorName}");

            if (expr.GroupingLabels.Length > 0)
            {
                _sb.Append(" ");
                _sb.Append(expr.Without ? "without" : "by");
                _sb.Append($" ({string.Join(", ", expr.GroupingLabels)}) ");
            }

            _sb.Append("(");
            if (expr.Param != null)
            {
                expr.Param.Accept(this);
                _sb.Append(", ");
            }

            expr.Expr.Accept(this);
            _sb.Append(")");
        }

        protected void Write(string s) => _sb.Append(s);

        public string ToPromQl(IPromQlNode node)
        {
            if (node == null)
                throw new ArgumentNullException();
            
            _sb.Clear();
            node.Accept(this);

            return _sb.ToString();
        }
    }
}