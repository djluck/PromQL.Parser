using System;
using System.Collections.Generic;
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
        /// More authoritative source would be https://github.com/prometheus/prometheus/blob/7471208b5c8ff6b65b644adedf7eb964da3d50ae/promql/parser/functions.go.
        /// </remarks>
        public static ImmutableHashSet<string> Names = new[]
        {
            "absent_over_time",
            "absent",
            "abs",
            "acos",
            "acosh",
            "asin",
            "asinh",
            "atan",
            "atanh",
            "avg_over_time",
            "ceil",
            "changes",
            "clamp",
            "clamp_max",
            "clamp_min",
            "cos",
            "cosh",
            "count_over_time",
            "days_in_month",
            "day_of_month",
            "day_of_week",
            "deg",
            "delta",
            "deriv",
            "exp",
            "floor",
            "histogram_quantile",
            "holt_winters",
            "hour",
            "idelta",
            "increase",
            "irate",
            "label_replace",
            "label_join",
            "last_over_time",
            "ln",
            "log_10",
            "log_2",
            "max_over_time",
            "min_over_time",
            "minute",
            "month",
            "pi",
            "predict_linear",
            "present_over_time",
            "quantile_over_time",
            "rad",
            "rate",
            "resets",
            "round",
            "scalar",
            "sgn",
            "sin",
            "sinh",
            "sort",
            "sort_desc",
            "sqrt",
            "stddev_over_time",
            "stdvar_over_time",
            "sum_over_time",
            "tan",
            "tanh",
            "timestamp",
            "time",
            "vector",
            "year"
        }.ToImmutableHashSet();
    }
}
