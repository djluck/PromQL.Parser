using System;
using System.Collections.Immutable;
using System.Text;
using ExhaustiveMatching;

namespace PromQL.Parser.Ast
{
    /// <summary>
    /// Base of all PromQL syntactic components.
    /// </summary>
    public interface IPromQlNode
    {
        void Accept(IVisitor visitor);
    }

    /// <summary>
    /// Root of all valid PromQL expressions.
    /// </summary>
    [Closed(
        typeof(AggregateExpr),
        typeof(BinaryExpr),
        typeof(Duration),
        typeof(FunctionCall),
        typeof(MatrixSelector),
        typeof(NumberLiteral),
        typeof(OffsetExpr),
        typeof(ParenExpression),
        typeof(StringLiteral),
        typeof(SubqueryExpr),
        typeof(UnaryExpr),
        typeof(VectorSelector)
    )]
    public interface Expr : IPromQlNode {}

    /// <summary>
    /// Represents an aggregation operation on a Vector.
    /// </summary>
    /// <param name="OperatorName">The used aggregation operation.</param>
    /// <param name="Expr">The Vector expression over which is aggregated.</param>
    /// <param name="Param">Parameter used by some aggregators.</param>
    /// <param name="GroupingLabels">The labels by which to group the Vector.</param>
    /// <param name="Without"> Whether to drop the given labels rather than keep them.</param>
    public record AggregateExpr(string OperatorName, Expr Expr, Expr? Param,
        ImmutableArray<string> GroupingLabels, bool Without) : Expr
    {
        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }

    /// <summary>
    /// Represents a binary expression between two child expressions.
    /// </summary>
    /// <param name="LeftHandSide">The left-hand operand of the expression</param>
    /// <param name="RightHandSide">The right-hand operand of the expression</param>
    /// <param name="Operator">The operation of the expression</param>
    /// <param name="VectorMatching">The matching behavior for the operation to be applied if both operands are Vectors.</param>
    public record BinaryExpr(Expr LeftHandSide, Expr RightHandSide, Operators.Binary Operator,
        VectorMatching? VectorMatching) : Expr
    {
        public void Accept(IVisitor visitor) => visitor.Visit(this);
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
        public static Operators.VectorMatchCardinality DefaultMatchCardinality { get; } = Operators.VectorMatchCardinality.OneToOne;

        public VectorMatching() : this(DefaultMatchCardinality, ImmutableArray<string>.Empty, false,
            ImmutableArray<string>.Empty, false)
        {
        }

        public VectorMatching(bool returnBool) : this (DefaultMatchCardinality, ImmutableArray<string>.Empty, false, ImmutableArray<string>.Empty, returnBool  )
        {
        }
        
        public void Accept(IVisitor visitor) => visitor.Visit(this);
    };

    /// <summary>
    /// A function call.
    /// </summary>
    /// <param name="Identifier">The function that was called.</param>
    /// <param name="Args">Arguments used in the call.</param>
    public record FunctionCall(string Identifier, ImmutableArray<Expr> Args) : Expr
    {
        public void Accept(IVisitor visitor) => visitor.Visit(this);

        protected virtual bool PrintMembers(StringBuilder builder)
        {
            builder.AppendLine($"{nameof(Identifier)} = {Identifier}, ");
            builder.Append($"{nameof(Args)} = ");
            Args.PrintArray(builder);
            
            return true;
        }
    }

    public record ParenExpression(Expr Expr) : Expr
    {
        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }

    public record OffsetExpr(Expr Expr, Duration Duration) : Expr
    {
        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }

    public record MatrixSelector(VectorSelector Vector, Duration Duration) : Expr
    {
        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }

    public record UnaryExpr(Operators.Unary Operator, Expr Expr) : Expr
    {
        public void Accept(IVisitor visitor) => visitor.Visit(this);
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
        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }

    public record LabelMatchers(ImmutableArray<LabelMatcher> Matchers) : IPromQlNode
    {
        protected virtual bool PrintMembers(StringBuilder builder)
        {
            builder.Append($"{nameof(Matchers)} = ");
            Matchers.PrintArray(builder);

            return true;
        }

        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }

    public record LabelMatcher(string LabelName, Operators.LabelMatch Operator, StringLiteral Value) : IPromQlNode
    {
        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }

    public record MetricIdentifier(string Value) : IPromQlNode
    {
        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }

    public record NumberLiteral(double Value) : Expr
    {
        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }

    public record Duration(TimeSpan Value) : Expr
    {
        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }

    public record StringLiteral(char Quote, string Value) : Expr
    {
        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }

    public record SubqueryExpr(Expr Expr, Duration Range, Duration? Step) : Expr
    {
        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }
    
    internal static class Extensions
    {
        internal static void PrintArray<T>(this ImmutableArray<T> arr, StringBuilder sb)
            where T : notnull
        {
            sb.Append("[ ");
            for (int i = 0; i < arr.Length; i++)
            {
                sb.Append(arr[i].ToString());
                if (i < arr.Length - 1)
                    sb.Append(", ");
            }

            sb.Append(" ]");
        } 
    }
}
