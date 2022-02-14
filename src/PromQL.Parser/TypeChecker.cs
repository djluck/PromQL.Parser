using System;
using System.Linq;
using ExhaustiveMatching;
using PromQL.Parser.Ast;
using Superpower.Model;

namespace PromQL.Parser
{
    public static class TypeChecker
    {
        public static ValueType CheckExpression(this Expr expr)
        {
            switch (expr)
            {
                case AggregateExpr aggExpr:
                    // TODO Currently don't check for parameter counts here
                    // The parser does this currently but we might want to extend the logic for user generated ASTs.
                    if (aggExpr.Operator.ParameterType != null && aggExpr.Param != null)
                        ExpectTypes(aggExpr.Param, aggExpr.Operator.ParameterType.Value);
                        
                    ExpectTypes(aggExpr.Expr, ValueType.Vector);
                    
                    return aggExpr.Type;
                
                // TODO
                case BinaryExpr binExpr:
                    return binExpr.Type;
                
                // TODO
                case FunctionCall fnCall:
                    if (fnCall.Function.IsVariadic)
                    {
                        
                    }
                    else
                    {
                        
                    }
                    
                    var expectedArgTypes = fnCall.Function.ArgTypes;
                    
                    // Varadic functions can repeat the last parameter type indefinitely
                    if (fnCall.Function.IsVariadic)
                        expectedArgTypes = expectedArgTypes.AddRange(
                            Enumerable.Repeat(
                                fnCall.Function.ArgTypes.Last(), 
                                fnCall.Args.Length - fnCall.Function.ArgTypes.Length
                            )
                        );

                    foreach (var (arg, expectedType) in fnCall.Args.Zip(expectedArgTypes))
                        ExpectTypes(arg, expectedType);
                    
                    return fnCall.Type;
                
                case SubqueryExpr subqueryExpr:
                    ExpectTypes(subqueryExpr, ValueType.Vector);
                    return subqueryExpr.Type;
                
                case UnaryExpr unaryExpr:
                    ExpectTypes(unaryExpr, ValueType.Scalar, ValueType.Vector);
                    return unaryExpr.Type;
                
                case NumberLiteral _:
                case ParenExpression _:
                case StringLiteral _:
                case VectorSelector _:
                case MatrixSelector _:
                case OffsetExpr _:
                    return expr.Type;

                default:
                    throw ExhaustiveMatch.Failed(expr);
            }
        }

        private static void ExpectTypes(Expr expr, params ValueType[] expected)
        {
            if (!expected.Contains(expr.Type))
                throw new InvalidTypeException(expected, expr.Type, expr.Span);
        }

        public class InvalidTypeException : Exception
        {
            public InvalidTypeException(ValueType[] expected, ValueType provided, TextSpan? span)
                : base($"Unexpected type '{provided}' was provided, expected one of '{string.Join(", ", expected)}': {span}")
            {
            }
        }
    }
}