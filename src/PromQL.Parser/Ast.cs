using System;
using System.Collections.Immutable;
using ExhaustiveMatching;
using Superpower.Model;

namespace PromQL.Parser.Ast
{
    /// <summary>
    /// Base of all PromQL syntactic components.
    /// </summary>
    public interface IPromQlNode
    {
        void Accept(IVisitor visitor);
        TextSpan? Span { get; }
    }

    /// <summary>
    /// Root of all valid PromQL expressions.
    /// </summary>
    [Closed(
        typeof(AggregateExpr),
        typeof(BinaryExpr),
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
    public interface Expr : IPromQlNode
    {
        ValueType Type { get; }
    }

    /// <summary>
    /// Represents an aggregation operation on a Vector.
    /// </summary>
    /// <param name="OperatorName">The used aggregation operation.</param>
    /// <param name="Expr">The Vector expression over which is aggregated.</param>
    /// <param name="Param">Parameter used by some aggregators.</param>
    /// <param name="GroupingLabels">The labels by which to group the Vector.</param>
    /// <param name="Without"> Whether to drop the given labels rather than keep them.</param>
    public record AggregateExpr(string OperatorName, Expr Expr, Expr? Param,
        ImmutableArray<string> GroupingLabels, bool Without, TextSpan? Span = null) : Expr
    {
        public AggregateExpr(string operatorName, Expr expr)
            : this (operatorName, expr, null, ImmutableArray<string>.Empty, false)
        {
        }

        public AggregateExpr(string operatorName, Expr expr, Expr param, bool without = false, params string[] groupingLabels)
            : this (operatorName, expr, param, groupingLabels.ToImmutableArray(), without)
        {
        }
        
        public string OperatorName { get; set;} = OperatorName;
        public Expr Expr { get; set;} = Expr;
        public Expr? Param { get; set;} = Param;
        public ImmutableArray<string> GroupingLabels { get; set;} = GroupingLabels;
        public bool Without { get; set;} = Without;

        public ValueType Type => ValueType.Vector;
        
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
        VectorMatching? VectorMatching = null, TextSpan? Span = null) : Expr
    {
        public Expr LeftHandSide { get; set; } = LeftHandSide;
        public Expr RightHandSide { get; set; } = RightHandSide;
        public Operators.Binary Operator { get; set; } = Operator;
        public VectorMatching? VectorMatching { get; set; } = VectorMatching;
        public void Accept(IVisitor visitor) => visitor.Visit(this);

        public ValueType Type
        {
            get
            {
                if (RightHandSide.Type == ValueType.Scalar && LeftHandSide.Type == ValueType.Scalar)
                    return ValueType.Scalar;

                return ValueType.Vector;
            }
        }
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
        bool On, ImmutableArray<string> Include, bool ReturnBool, TextSpan? Span = null) : IPromQlNode
    {
        public static Operators.VectorMatchCardinality DefaultMatchCardinality { get; } = Operators.VectorMatchCardinality.OneToOne;

        public VectorMatching() : this(DefaultMatchCardinality, ImmutableArray<string>.Empty, false,
            ImmutableArray<string>.Empty, false)
        {
        }

        public VectorMatching(bool returnBool) : this (DefaultMatchCardinality, ImmutableArray<string>.Empty, false, ImmutableArray<string>.Empty, returnBool  )
        {
        }

        public Operators.VectorMatchCardinality MatchCardinality { get; set; } = MatchCardinality;
        public bool On { get; set; } = On;
        public ImmutableArray<string> Include { get; set; } = Include;
        public bool ReturnBool { get; set; } = ReturnBool;

        public void Accept(IVisitor visitor) => visitor.Visit(this);
    };

    /// <summary>
    /// A function call.
    /// </summary>
    /// <param name="Function">The function that was called.</param>
    /// <param name="Args">Arguments used in the call.</param>
    public record FunctionCall(Function Function, ImmutableArray<Expr> Args, TextSpan? Span = null) : Expr
    {
        public FunctionCall(Function function, params Expr[] args) 
            : this (function, args.ToImmutableArray())
        {
        }

        public Function Function { get; set; } = Function;
        public ImmutableArray<Expr> Args { get; set; } = Args;

        public ValueType Type => Function.ReturnType;

        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }

    public record ParenExpression(Expr Expr, TextSpan? Span = null) : Expr
    {
        public Expr Expr { get; set; } = Expr;
        public void Accept(IVisitor visitor) => visitor.Visit(this);
        public ValueType Type => Expr.Type;
    }

    public record OffsetExpr(Expr Expr, Duration Duration, TextSpan? Span = null) : Expr
    {
        public Expr Expr { get; set; } = Expr;
        public Duration Duration { get; set; } = Duration;
        public void Accept(IVisitor visitor) => visitor.Visit(this);
        public ValueType Type => Expr.Type;
    }

    public record MatrixSelector(VectorSelector Vector, Duration Duration, TextSpan? Span = null) : Expr
    {
        public VectorSelector Vector { get; set; } =Vector;
        public Duration Duration { get; set; } = Duration;
        public void Accept(IVisitor visitor) => visitor.Visit(this);
        public ValueType Type => ValueType.Matrix;
    }

    public record UnaryExpr(Operators.Unary Operator, Expr Expr, TextSpan? Span = null) : Expr
    {
        public Operators.Unary Operator { get; set; } = Operator;
        public Expr Expr { get; set; } = Expr;
        
        public void Accept(IVisitor visitor) => visitor.Visit(this);
        public ValueType Type => Expr.Type;
    }
    
    public record VectorSelector : Expr
    {
        public VectorSelector(MetricIdentifier metricIdentifier, TextSpan? span = null)
        {
            MetricIdentifier = metricIdentifier;
            Span = span;
        }

        public VectorSelector(LabelMatchers labelMatchers, TextSpan? span = null)
        {
            LabelMatchers = labelMatchers;
            Span = span;
        }
        
        public VectorSelector(MetricIdentifier metricIdentifier, LabelMatchers labelMatchers, TextSpan? span = null)
        {
            
            MetricIdentifier = metricIdentifier;
            LabelMatchers = labelMatchers;
            Span = span;
        }
        
        public MetricIdentifier? MetricIdentifier { get; set; }
        public LabelMatchers? LabelMatchers { get; set; }
        public TextSpan? Span { get; }
        public ValueType Type => ValueType.Vector;
        
        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }

    public record LabelMatchers(ImmutableArray<LabelMatcher> Matchers, TextSpan? Span = null) : IPromQlNode
    {
        protected virtual bool PrintMembers(System.Text.StringBuilder builder)
        {
            builder.Append($"{nameof(Matchers)} = ");
            Matchers.PrintArray(builder);

            return true;
        }

        public ImmutableArray<LabelMatcher> Matchers { get; set; } = Matchers;

        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }

    public record LabelMatcher(string LabelName, Operators.LabelMatch Operator, StringLiteral Value, TextSpan? Span = null) : IPromQlNode
    {
        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }

    public record MetricIdentifier(string Value, TextSpan? Span = null) : IPromQlNode
    {
        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }

    public record NumberLiteral(double Value, TextSpan? Span = null) : Expr
    {
        public void Accept(IVisitor visitor) => visitor.Visit(this);
        public ValueType Type => ValueType.Scalar;
    }

    public record Duration(TimeSpan Value, TextSpan? Span = null) : IPromQlNode 
    {
        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }

    public record StringLiteral(char Quote, string Value, TextSpan? Span = null) : Expr
    {
        public void Accept(IVisitor visitor) => visitor.Visit(this);
        public ValueType Type => ValueType.String;
    }

    public record SubqueryExpr(Expr Expr, Duration Range, Duration? Step = null, TextSpan? Span = null) : Expr
    {
        public Expr Expr { get; set; } = Expr;
        public Duration Range { get; set; } = Range;
        public Duration? Step { get; set; } = Step;
        public ValueType Type => ValueType.Matrix;
        
        public void Accept(IVisitor visitor) => visitor.Visit(this);
    }
    
    internal static class Extensions
    {
        internal static void PrintArray<T>(this ImmutableArray<T> arr, System.Text.StringBuilder sb)
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
