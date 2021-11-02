using System;
using System.Collections.Immutable;
using ExhaustiveMatching;

namespace PromQL.Parser
{
    public static class Operators
    {
        /// <remarks>
        /// Taken from https://github.com/prometheus/prometheus/blob/4414351576ac27754d9eec71c271171d5c020677/pkg/labels/matcher.go#L24
        /// </remarks>
        public enum LabelMatch
        {
            Equal,
            NotEqual,
            Regexp,
            NotRegexp
        }
        
        /// <summary>
        /// Defines the set of all valid aggregator operators (e.g. sum, avg, etc.)
        /// </summary>
        public static ImmutableHashSet<string> Aggregates = new []
        {
            "sum",
            "avg",
            "count",
            "min",
            "max",
            "group",
            "stddev",
            "stdvar",
            "topk",
            "bottomk",
            "count_values",
            "quantile",
        }.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// Describes the cardinality relationship of two Vectors in a binary operation.
        /// </summary>
        public enum VectorMatchCardinality
        {
            OneToOne,
            ManyToOne,
            OneToMany
        }
        
        public enum Binary
        {
            Pow,
            Mul,
            Div,
            Mod,
            Atan2,
            Add,
            Sub,
            Eql,
            Gte,
            Gtr,
            Lte,
            Lss,
            Neq,
            And,
            Unless,
            Or
        }

        public enum Unary
        {
            /// <summary>
            /// Aka plus ('+')
            /// </summary>
            Add,
            /// <summary>
            /// Aka minus ('-')
            /// </summary>
            Sub
        }
    }
    
    public static class Extensions
    {
        public static string ToPromQl(this Operators.Binary op) => op switch
        {
            Operators.Binary.Add => "+",
            Operators.Binary.Pow => "^",
            Operators.Binary.Mul => "*",
            Operators.Binary.Div => "/",
            Operators.Binary.Mod => "%",
            Operators.Binary.Atan2 => "atan2",
            Operators.Binary.Sub => "-",
            Operators.Binary.Eql => "==",
            Operators.Binary.Gte => ">=",
            Operators.Binary.Gtr => ">",
            Operators.Binary.Lte => "<=",
            Operators.Binary.Lss => "<",
            Operators.Binary.Neq => "!=",
            Operators.Binary.And => "and",
            Operators.Binary.Unless => "unless",
            Operators.Binary.Or => "or",
            _ => throw ExhaustiveMatch.Failed(op)
        };
        
        public static string ToPromQl(this Operators.Unary op) => op switch
        {
            Operators.Unary.Add => "+",
            Operators.Unary.Sub => "-",
            _ => throw ExhaustiveMatch.Failed(op)
        };
        
        public static string ToPromQl(this Operators.LabelMatch op) => op switch
        {
            Operators.LabelMatch.Equal => "=",
            Operators.LabelMatch.NotEqual => "!=",
            Operators.LabelMatch.Regexp => "=~",
            Operators.LabelMatch.NotRegexp => "!~",
            _ => throw ExhaustiveMatch.Failed(op)
        };
        
        public static string ToPromQl(this Operators.VectorMatchCardinality op) => op switch
        {
            Operators.VectorMatchCardinality.OneToOne => "",
            Operators.VectorMatchCardinality.OneToMany => "group_right",
            Operators.VectorMatchCardinality.ManyToOne => "group_left",
            _ => throw ExhaustiveMatch.Failed(op)
        };
    }
    
}
