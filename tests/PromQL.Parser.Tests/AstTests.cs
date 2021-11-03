using System;
using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;

namespace PromQL.Parser.Tests
{
    [TestFixture]
    public class AstTests
    {
        [Test]
        public void StringLiteral_ToPromQl() => new StringLiteral('\'', "hello").ToPromQl().Should().Be("'hello'");
        
        [Test]
        [TestCase(1.0, "1")]
        [TestCase(-999.999, "-999.999")]
        public void NumberLiteral_ToPromQl(double input, string expected) => new NumberLiteral(input).ToPromQl().Should().Be(expected);
        
        [Test]
        [TestCase("7.00:00:00", "7d")] // don't support converting to weeks for now
        [TestCase("5.00:00:00", "5d")]
        [TestCase("1.20:00:00", "1d20h")]
        [TestCase("0.16:17:18.019", "16h17m18s19ms")]
        public void Duration_ToPromQl(string input, string expected) => new Duration(TimeSpan.Parse(input)).ToPromQl().Should().Be(expected);

        [Test]
        public void Aggregate_ToPromQl()
        {
            new AggregateExpr("sum",
                new VectorSelector(new MetricIdentifier("test_expr")),
                null, 
                ImmutableArray<string>.Empty, 
                false
            ).ToPromQl().Should().Be("sum(test_expr)");
        }
        
        [Test]
        public void Aggregate_WithLabels_ToPromQl()
        {
            new AggregateExpr("sum",
                new VectorSelector(new MetricIdentifier("test_expr")),
                null, 
                new [] { "one" }.ToImmutableArray(),
                false
            ).ToPromQl().Should().Be("sum by (one) (test_expr)");
        }

        [Test]
        public void Aggregate_WithParam_ToPromQl()
        {
            new AggregateExpr("quantile",
                new VectorSelector(new MetricIdentifier("test_expr")),
                new NumberLiteral(1),
                new[] {"label1", "label2"}.ToImmutableArray(),
                true
            ).ToPromQl().Should().Be("quantile without (label1, label2) (1, test_expr)");
        }
        
        [Test]
        public void VectorMatching_Default_ToPromQl() => new VectorMatching(false)
            .ToPromQl().Should().Be("");

        [Test]
        public void VectorMatching_Bool_ToPromQl() => new VectorMatching(true)
            .ToPromQl().Should().Be("bool");

        [Test]
        public void VectorMatching_Ignoring_ToPromQl()
        {
            new VectorMatching(
                Operators.VectorMatchCardinality.OneToOne,
                new[] {"one", "two"}.ToImmutableArray(),
                false,
                ImmutableArray<string>.Empty,
                false
            ).ToPromQl().Should().Be("ignoring (one, two)");
        }
        
        [Test]
        public void VectorMatching_On_ToPromQl()
        {
            new VectorMatching(
                Operators.VectorMatchCardinality.OneToOne,
                new[] {"one", "two"}.ToImmutableArray(),
                true,
                ImmutableArray<string>.Empty,
                false
            ).ToPromQl().Should().Be("on (one, two)");
        }
        
        [Test]
        public void VectorMatching_OnNoLabels_ToPromQl()
        {
            new VectorMatching(
                Operators.VectorMatchCardinality.OneToOne,
                ImmutableArray<string>.Empty,
                true,
                ImmutableArray<string>.Empty,
                false
            ).ToPromQl().Should().Be("on ()");
        }
        
        [Test]
        public void VectorMatching_GroupRight_NoLabels_ToPromQl()
        {
            new VectorMatching(
                Operators.VectorMatchCardinality.OneToMany,
                ImmutableArray<string>.Empty,
                false,
                ImmutableArray<string>.Empty,
                false
            ).ToPromQl().Should().Be("group_right");
        }
        
        [Test]
        public void VectorMatching_GroupLeft_Labels_ToPromQl()
        {
            new VectorMatching(
                Operators.VectorMatchCardinality.ManyToOne,
                ImmutableArray<string>.Empty,
                false,
                new[] {"one", "two"}.ToImmutableArray(),
                false
            ).ToPromQl().Should().Be("group_left (one, two)");
        }

        // This expression doesn't have to be valid PromQL to be a useful test
        [Test]
        public void Complex_ToPromQl() =>
            new BinaryExpr(
                new ParenExpression(
                    new SubqueryExpr(
                        new MatrixSelector(
                            new VectorSelector(
                                new MetricIdentifier("another_metric"), new LabelMatchers(new []
                                {
                                    new LabelMatcher("one", Operators.LabelMatch.Equal, new StringLiteral('\'', "test")),
                                    new LabelMatcher("two", Operators.LabelMatch.NotEqual, new StringLiteral('\'', "test2"))
                                }.ToImmutableArray())
                            ),
                            new Duration(TimeSpan.FromHours(1))
                        ),
                        new Duration(TimeSpan.FromDays(1)),
                        new Duration(TimeSpan.FromMinutes(5))
                    )
                ),
                new UnaryExpr(
                    Operators.Unary.Sub,
                    new FunctionCall("sum", new Expr[]
                    {
                        new OffsetExpr(new VectorSelector(new MetricIdentifier("this_is_a_metric")), new Duration(TimeSpan.FromMinutes(5)))
                    }.ToImmutableArray())
                ),
                Operators.Binary.Add,
                null
            ).ToPromQl().Should().Be("(another_metric{one='test', two!='test2'}[1h][1d:5m]) + -sum(this_is_a_metric offset 5m)");
    }
}