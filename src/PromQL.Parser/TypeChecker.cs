using System;
using System.Linq;
using ExhaustiveMatching;
using PromQL.Parser.Ast;
using Superpower.Model;

namespace PromQL.Parser
{
    public static class TypeChecker
    {
        public static ValueType CheckType(this Expr expr)
        {
            switch (expr)
            {
                case AggregateExpr aggExpr:
                    // TODO Currently don't check for parameter counts here
                    // The parser does this currently but we might want to extend the logic for user generated ASTs.
                    if (aggExpr.Operator.ParameterType != null)
                        ExpectTypes(aggExpr.Param!, aggExpr.Operator.ParameterType.Value);
                        
                    ExpectTypes(aggExpr.Expr, ValueType.Vector);
                    
                    return aggExpr.Type;
                
                case BinaryExpr binExpr:
                    var lhsType = ExpectTypes(binExpr.LeftHandSide, ValueType.Scalar, ValueType.Vector);
                    var rhsType = ExpectTypes(binExpr.RightHandSide, ValueType.Scalar, ValueType.Vector);

                    if (Operators.BinaryComparisonOperators.Contains(binExpr.Operator) && !(binExpr.VectorMatching?.ReturnBool ?? false)
                        && lhsType == ValueType.Scalar && rhsType == ValueType.Scalar)
                    {
                        throw new InvalidTypeException("comparisons between scalars must use bool modifier", binExpr.Span);
                    }
                    

                    // TODO https://github.com/prometheus/prometheus/blob/7471208b5c8ff6b65b644adedf7eb964da3d50ae/promql/parser/parse.go#L526-L534
                    
                    if ((lhsType == ValueType.Scalar || rhsType == ValueType.Scalar) && Operators.BinarySetOperators.Contains(binExpr.Operator))
                        throw new InvalidTypeException($"set operator {binExpr.Operator} not allowed in binary scalar expression", binExpr.Span);
                    
                    return binExpr.Type;
                
                case FunctionCall fnCall:
                    var expectedArgTypes = fnCall.Function.ArgTypes;
                    
                    // Varadic functions can repeat the last parameter type indefinitely
                    if (fnCall.Function.IsVariadic)
                        expectedArgTypes = expectedArgTypes.AddRange(
                            Enumerable.Repeat(
                                fnCall.Function.ArgTypes.Last(), 
                                fnCall.Args.Length - fnCall.Function.MinArgCount
                            )
                        );

                    foreach (var (arg, expectedType) in fnCall.Args.Zip(expectedArgTypes))
                        ExpectTypes(arg, expectedType);
                    
                    return fnCall.Type;
                
                case SubqueryExpr subqueryExpr:
                    ExpectTypes(subqueryExpr.Expr, ValueType.Vector);
                    return subqueryExpr.Type;
                
                case UnaryExpr unaryExpr:
                    ExpectTypes(unaryExpr.Expr, ValueType.Scalar, ValueType.Vector);
                    return unaryExpr.Type;

                case OffsetExpr offsetExpr:
                    return offsetExpr.Expr.CheckType();
                
                case ParenExpression parenExpr:
                    return parenExpr.Expr.CheckType();
                
                case NumberLiteral _:
                case StringLiteral _:
                case MatrixSelector _:
                case VectorSelector _:
                    return expr.Type;

                default:
                    throw ExhaustiveMatch.Failed(expr);
            }
        }

        private static ValueType ExpectTypes(Expr expr, params ValueType[] expected)
        {
            var type = expr.CheckType();
            if (!expected.Contains(type))
                throw new InvalidTypeException(expected, expr.Type, expr.Span);

            return type;
        }
        
        public class InvalidTypeException : Exception
        {
            public InvalidTypeException(ValueType[] expected, ValueType provided, TextSpan? span)
                : this($"Unexpected type '{AsHumanReadable(provided)}' was provided, expected {(expected.Length == 1 ? AsHumanReadable(expected[0]): $"one of {string.Join(", ", expected.Select(e => $"'{AsHumanReadable(e)}'"))}")}", span)
            {
            }
            
            public InvalidTypeException(string message, TextSpan? span)
                : base($"{message}{(span.HasValue ? $": {span.Value.Position.ToString()}" : "")}")
            {
            }

            private static string AsHumanReadable(ValueType vt) => vt switch
            {
                ValueType.Scalar => "scalar",
                ValueType.Matrix => "range vector",
                ValueType.Vector => "instant vector",
                ValueType.String => "string",
                ValueType.None => "none",
                _ => throw ExhaustiveMatch.Failed(vt)
            };
        }
    }
}