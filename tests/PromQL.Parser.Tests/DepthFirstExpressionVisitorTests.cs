using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using PromQL.Parser.Ast;

namespace PromQL.Parser.Tests
{
    [TestFixture]
    public class DepthFirstExpressionVisitorTests
    {
        [Test]
        public void Visit_Basic_Types()
        {
            // Not semantically valid but syntactically valid!
            const string toParse = "'hello' + 1";
            var expr = Parser.ParseExpression(toParse);
            var visitor = new DepthFirstExpressionVisitor();
            visitor.GetExpressions(expr)
                .Select(x => x.GetType())
                .Should()
                .Equal(
                    typeof(BinaryExpr),
                    typeof(StringLiteral),
                    typeof(NumberLiteral)
                );
        }
        
        [Test]
        public void Visit_Complex_Expression()
        {
            const string toParse = "sum(rate(my_vector[1m] offset 5m)) + -(some_metric[1m:])";
            var expr = Parser.ParseExpression(toParse);
            var visitor = new DepthFirstExpressionVisitor();
            visitor.GetExpressions(expr)
                    .Select(x => x.GetType())
                    .Should()
                    .Equal(
                        typeof(BinaryExpr),
                        typeof(AggregateExpr),
                        typeof(FunctionCall),
                        typeof(OffsetExpr),
                        typeof(MatrixSelector),
                        typeof(VectorSelector),
                        typeof(UnaryExpr),
                        typeof(ParenExpression),
                        typeof(SubqueryExpr),
                        typeof(VectorSelector)
                    )
                ;
        }
    }
}