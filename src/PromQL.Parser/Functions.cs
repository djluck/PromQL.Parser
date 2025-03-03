using System.Collections.Immutable;

namespace PromQL.Parser
{
    public static class Functions
    {
        /// <summary>
        /// The set of all recognized PromQL functions.
        /// </summary>
        /// <remarks>
        /// Primarily taken from https://github.com/prometheus/prometheus/blob/main/web/ui/module/codemirror-promql/src/grammar/promql.grammar#L121-L188.
        /// More authoritative source would be https://github.com/prometheus/prometheus/blob/38fd48e6b54b5413b5281efbeeb989a44845be34/promql/parser/functions.go.
        /// </remarks>
        public static ImmutableDictionary<string, Function> Map { get; set; } = new[]
        {
            new Function("absent", ValueType.Vector, ValueType.Vector),
            new Function("absent_over_time", ValueType.Vector, ValueType.Matrix),
            new Function("abs", ValueType.Vector, ValueType.Vector),
            new Function("acos", ValueType.Vector, ValueType.Vector),
            new Function("acosh", ValueType.Vector, ValueType.Vector),
            new Function("asin", ValueType.Vector, ValueType.Vector),
            new Function("asinh", ValueType.Vector, ValueType.Vector),
            new Function("atan", ValueType.Vector, ValueType.Vector),
            new Function("atanh", ValueType.Vector, ValueType.Vector),
            new Function("avg_over_time", ValueType.Vector, ValueType.Matrix),
            new Function("ceil", ValueType.Vector, ValueType.Vector),
            new Function("changes", ValueType.Vector, ValueType.Matrix),
            new Function("clamp", ValueType.Vector, ValueType.Vector, ValueType.Scalar, ValueType.Scalar),
            new Function("clamp_max", ValueType.Vector, ValueType.Vector, ValueType.Scalar),
            new Function("clamp_min", ValueType.Vector, ValueType.Vector, ValueType.Scalar),
            new Function("cos", ValueType.Vector, ValueType.Vector),
            new Function("cosh", ValueType.Vector, ValueType.Vector),
            new Function("count_over_time", ValueType.Vector, ValueType.Matrix),
            new Function("days_in_month", ValueType.Vector, varadicModifier: 1 , ValueType.Vector),
            new Function("day_of_month", ValueType.Vector, varadicModifier: 1, ValueType.Vector),
            new Function("day_of_week", ValueType.Vector, varadicModifier: 1, ValueType.Vector),
            new Function("day_of_year", ValueType.Vector, varadicModifier: 1, ValueType.Vector),
            new Function("deg", ValueType.Vector, ValueType.Vector),
            new Function("delta", ValueType.Vector, ValueType.Matrix),
            new Function("deriv", ValueType.Vector, ValueType.Matrix),
            new Function("exp", ValueType.Vector, ValueType.Vector),
            new Function("floor", ValueType.Vector, ValueType.Vector),
            new Function("histogram_avg", ValueType.Vector, ValueType.Vector),
            new Function("histogram_count", ValueType.Vector, ValueType.Vector),
            new Function("histogram_sum", ValueType.Vector, ValueType.Vector),
            new Function("histogram_stddev", ValueType.Vector, ValueType.Vector),
            new Function("histogram_stdvar", ValueType.Vector, ValueType.Vector),
            new Function("histogram_fraction", ValueType.Vector, ValueType.Scalar, ValueType.Scalar, ValueType.Vector),
            new Function("histogram_quantile", ValueType.Vector, ValueType.Scalar, ValueType.Vector),
            new Function("double_exponential_smoothing", ValueType.Vector, ValueType.Matrix, ValueType.Scalar, ValueType.Scalar),
            new Function("hour", ValueType.Vector, varadicModifier: 1, ValueType.Vector),
            new Function("idelta", ValueType.Vector, ValueType.Matrix),
            new Function("info", ValueType.Vector, varadicModifier: 1, ValueType.Vector, ValueType.Vector),
            new Function("increase", ValueType.Vector, ValueType.Matrix),
            new Function("irate", ValueType.Vector, ValueType.Matrix),
            new Function("label_replace", ValueType.Vector, ValueType.Vector, ValueType.String, ValueType.String, ValueType.String, ValueType.String),
            new Function("label_join", ValueType.Vector, varadicModifier: 0, ValueType.Vector, ValueType.String, ValueType.String, ValueType.String),
            new Function("last_over_time", ValueType.Vector, ValueType.Matrix),
            new Function("ln", ValueType.Vector, ValueType.Vector),
            new Function("log_10", ValueType.Vector, ValueType.Vector),
            new Function("log_2", ValueType.Vector, ValueType.Vector),
            new Function("mad_over_time", ValueType.Vector, ValueType.Matrix),
            new Function("max_over_time", ValueType.Vector, ValueType.Matrix),
            new Function("min_over_time", ValueType.Vector, ValueType.Matrix),
            new Function("minute", ValueType.Vector, varadicModifier: 1, ValueType.Vector),
            new Function("month", ValueType.Vector, varadicModifier: 1, ValueType.Vector),
            new Function("pi", ValueType.Scalar),
            new Function("predict_linear", ValueType.Scalar, ValueType.Matrix, ValueType.Scalar),
            new Function("present_over_time", ValueType.Vector, ValueType.Matrix),
            new Function("quantile_over_time", ValueType.Vector, ValueType.Scalar, ValueType.Matrix),
            new Function("rad", ValueType.Vector, ValueType.Vector),
            new Function("rate", ValueType.Vector, ValueType.Matrix),
            new Function("resets", ValueType.Vector, ValueType.Matrix),
            new Function("round", ValueType.Vector, varadicModifier: 1, ValueType.Vector, ValueType.Scalar),
            new Function("scalar", ValueType.Scalar, ValueType.Vector),
            new Function("sgn", ValueType.Vector, ValueType.Vector),
            new Function("sin", ValueType.Vector, ValueType.Vector),
            new Function("sinh", ValueType.Vector, ValueType.Vector),
            new Function("sort", ValueType.Vector, ValueType.Vector),
            new Function("sort_desc", ValueType.Vector, ValueType.Vector),
            new Function("sort_by_label", ValueType.Vector, varadicModifier: 0, ValueType.Vector, ValueType.String),
            new Function("sort_by_label_desc", ValueType.Vector, varadicModifier: 0, ValueType.Vector, ValueType.String),
            new Function("sqrt", ValueType.Vector, ValueType.Vector),
            new Function("stddev_over_time", ValueType.Vector, ValueType.Matrix),
            new Function("stdvar_over_time", ValueType.Vector, ValueType.Matrix),
            new Function("sum_over_time", ValueType.Vector, ValueType.Matrix),
            new Function("tan", ValueType.Vector, ValueType.Vector),
            new Function("tanh", ValueType.Vector, ValueType.Vector),
            new Function("timestamp", ValueType.Vector, ValueType.Vector),
            new Function("time", ValueType.Scalar),
            new Function("vector", ValueType.Vector, ValueType.Scalar),
            new Function("year", ValueType.Vector, varadicModifier: 1, ValueType.Vector)
        }.ToImmutableDictionary(k => k.Name);
    }

    public record Function(string Name, ValueType ReturnType, ImmutableArray<ValueType> ArgTypes, int? VariadicModifier = null)
    {
        public Function(string name, ValueType returnType, params ValueType[] argTypes) 
            : this(name, returnType, argTypes.ToImmutableArray(), null) { }   
        
        public Function(string name, ValueType returnType, int varadicModifier, params ValueType[] argTypes) 
            : this(name, returnType, argTypes.ToImmutableArray(), varadicModifier) { }

        public bool IsVariadic => VariadicModifier != null;
        public int MinArgCount => ArgTypes.Length - (VariadicModifier ?? 0);
    }
}
