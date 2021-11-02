using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace PromQL.Parser
{
    /// <summary>
    /// Base of all PromQL syntactic components.
    /// </summary>
    public interface IPromQlNode
    {
        string ToPromQl();
    }
        
    /// <summary>
    /// Root of all valid PromQL expressions.
    /// </summary>
    public interface Expr : IPromQlNode {}


    /// <summary>
    /// Represents an aggregation operation on a Vector.
    /// </summary>
    /// <param name="OperatorName">The used aggregation operation.</param>
    /// <param name="Expr">The Vector expression over which is aggregated.</param>
    /// <param name="Param">Parameter used by some aggregators.</param>
    /// <param name="GroupingLabels">The labels by which to group the Vector.</param>
    /// <param name="Without"> Whether to drop the given labels rather than keep them.</param>
    public record AggregateExpr(string OperatorName, Expr Expr, Expr Param,
        ImmutableArray<string> GroupingLabels, bool Without) : Expr
    {
        public string ToPromQl() => $"{OperatorName} {(Without ? "without" : "by")} ({string.Join(", ", GroupingLabels)}) ({(Param != null ? string.Join(", ", new []{ Param, Expr}.Select(x => x.ToPromQl())) : Expr.ToPromQl())})";
    }

    /// <summary>
    /// Represents a binary expression between two child expressions.
    /// </summary>
    /// <param name="LeftHandSide">The left-hand operand of the expression</param>
    /// <param name="RightHandSide">The right-hand operand of the expression</param>
    /// <param name="Operator">The operation of the expression</param>
    /// <param name="VectorMatching">The matching behavior for the operation to be applied if both operands are Vectors.</param>
    public record BinaryExpr(Expr LeftHandSide, Expr RightHandSide, Operators.Binary Operator,
        VectorMatching VectorMatching) : Expr
    {
        public string ToPromQl() => $"{LeftHandSide.ToPromQl()} {Operator.ToPromQl()} {VectorMatching?.ToPromQl()} {RightHandSide.ToPromQl()}";
    }

    /// <summary>
    /// VectorMatching describes how elements from two Vectors in a binary operation are supposed to be matched.
    /// </summary>
    /// <param name="MatchCardinality">The cardinality of the two Vectors.</param>
    /// <param name="MatchingLabels">Contains the labels which define equality of a pair of elements from the Vectors.</param>
    /// <param name="On">When true, includes the given label names from matching, rather than excluding them.</param>
    /// <param name="Include">Contains additional labels that should be included in the result from the side with the lower cardinality.</param>
    /// <param name="ReturnBool">If a comparison operator, return 0/1 rather than filtering.</param>
    public record VectorMatching(Operators.VectorMatchCardinality MatchCardinality, ImmutableArray<string> MatchingLabels,
        bool On, ImmutableArray<string> Include, bool ReturnBool) : IPromQlNode
    {
        public VectorMatching() : this(Operators.VectorMatchCardinality.OneToOne, ImmutableArray<string>.Empty, false,
            ImmutableArray<string>.Empty, false)
        {
        }

        public VectorMatching(bool returnBool) : this (Operators.VectorMatchCardinality.OneToOne, ImmutableArray<string>.Empty, false, ImmutableArray<string>.Empty, returnBool  )
        {
        }
        
        public string ToPromQl() =>
            $"{(ReturnBool ? "bool" : "")} {(On ? "on" : "ignoring")} ({string.Join(",", MatchingLabels)})  {MatchCardinality.ToPromQl()} ({string.Join(",", Include)})";
    };

    /// <summary>
    /// A function call.
    /// </summary>
    /// <param name="Identifier">The function that was called.</param>
    /// <param name="Args">Arguments used in the call.</param>
    public record FunctionCall(string Identifier, ImmutableArray<Expr> Args) : Expr
    {
        public string ToPromQl() => $"{Identifier}({string.Join(",", Args.Select(x => x.ToPromQl()))})";
    }

    public record ParenExpression(Expr Expr) : Expr
    {
        public string ToPromQl() => $"({Expr.ToPromQl()})";
    }

    public record OffsetExpr(Expr Expr, Duration Duration) : Expr
    {
        public string ToPromQl() => $"{Expr.ToPromQl()} offset {Duration.ToPromQl()}";
    }

    public record MatrixSelector(VectorSelector Vector, Duration Duration) : Expr
    {
        public string ToPromQl() => $"{Vector.ToPromQl()}[{Duration.ToPromQl()}]";
    }

    public record UnaryExpr(Operators.Unary Operator, Expr Expr) : Expr
    {
        public string ToPromQl() => $"{Operator.ToPromQl()}{Expr.ToPromQl()}";
    }
    
    public record VectorSelector : Expr
    {
        public VectorSelector(MetricIdentifier metricIdentifier)
        {
            MetricIdentifier = metricIdentifier;
        }

        public VectorSelector(LabelMatchers labelMatchers)
        {
            LabelMatchers = labelMatchers;
        }
        
        public VectorSelector(MetricIdentifier metricIdentifier, LabelMatchers labelMatchers)
        {
            MetricIdentifier = metricIdentifier;
            LabelMatchers = labelMatchers;
        }
        
        public MetricIdentifier? MetricIdentifier { get; }
        public LabelMatchers? LabelMatchers { get; }
        
        public string ToPromQl() => $"{MetricIdentifier?.ToPromQl() ?? ""}{LabelMatchers?.ToPromQl() ?? ""}";
    }

    public record LabelMatchers(ImmutableArray<LabelMatcher> Matchers) : IPromQlNode
    {
        public string ToPromQl() => $"{{{string.Join(", ", Matchers.Select(x => x.ToPromQl()))}}}";
    }

    public record LabelMatcher(string LabelName, Operators.LabelMatch Operator, StringLiteral Value) : IPromQlNode
    {
        public string ToPromQl() => $"{LabelName}{Operator.ToPromQl()}{Value.ToPromQl()}";
    }

    public record MetricIdentifier(string Value) : IPromQlNode
    {
        public string ToPromQl() => Value;
    }

    public record NumberLiteral(double Value) : Expr
    {
        public string ToPromQl() => Value.ToString();
    }

    public record Duration(TimeSpan Value) : Expr
    {
        // TODO test
        public string ToPromQl()
        {

            var sb = new StringBuilder();
            if (Value.Days > 0)
                sb.Append($"{Value.Days}d");
            if (Value.Hours > 0)
                sb.Append($"{Value.Hours}h");
            if (Value.Minutes > 0)
                sb.Append($"{Value.Minutes}m");
            if (Value.Seconds > 0)
                sb.Append($"{Value.Seconds}s");
            if (Value.Milliseconds > 0)
                sb.Append($"{Value.Milliseconds}ms");
            
            return sb.ToString();
        }
    }

    public record StringLiteral(char Quote, string Value) : Expr
    {
        public string ToPromQl() => $"{Quote}{Value}{Quote}";
    }

    public record SubqueryExpr(Expr Expr, Duration Range, Duration? Step) : Expr
    {
        public string ToPromQl() => $"{Expr.ToPromQl()}[{Range.ToPromQl()}:{Step?.ToPromQl() ?? ""}]";
    }
}
