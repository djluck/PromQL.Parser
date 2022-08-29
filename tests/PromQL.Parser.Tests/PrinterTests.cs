using System;
using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using PromQL.Parser.Ast;

namespace PromQL.Parser.Tests
{
    [TestFixture]
    public class PrettyPrinterTests
    {
        private Printer _printer = new Printer();
        
        [Test]
        public void StringLiteral_SingleQuote() => _printer.ToPromQl(new StringLiteral('\'', "hello")).Should().Be("'hello'");
        
        [Test]
        public void StringLiteral_DoubleQuote() => _printer.ToPromQl(new StringLiteral('"', "hello")).Should().Be("\"hello\"");
        
        [Test]
        public void StringLiteral_RawString() => _printer.ToPromQl(new StringLiteral('`', "hello")).Should().Be("`hello`");
        
        [Test]
        public void StringLiteral_SingleQuoteEscaped() => _printer.ToPromQl(new StringLiteral('\'', "\a\b\f\n\r\t\v\\\'")).Should().Be(@"'\a\b\f\n\r\t\v\\\''");
        
        [Test]
        public void StringLiteral_DoubleQuoteEscaped() => _printer.ToPromQl(new StringLiteral('"', "\a\b\f\n\r\t\v\\\"")).Should().Be(@"""\a\b\f\n\r\t\v\\\""""");
        
        [Test]
        public void StringLiteral_RawStringNotEscaped() => _printer.ToPromQl(new StringLiteral('`', @"\a\b\f\n\r\t\v")).Should().Be(@"`\a\b\f\n\r\t\v`");
        
        [Test]
        public void StringLiteral_RawStringNewline() => _printer.ToPromQl(new StringLiteral('`', @"A\n string")).Should().Be(@"`A\n string`");
        
        [Test]
        [TestCase(1.0, "1")]
        [TestCase(-999.999, "-999.999")]
        public void NumberLiteral_ToPromQl(double input, string expected) => _printer.ToPromQl(new NumberLiteral(input)).Should().Be(expected);

        [Test]
        public void NumberLiteral_Inf_ToPromQl()
        {
            _printer.ToPromQl(new NumberLiteral(double.PositiveInfinity)).Should().Be("Inf");
            _printer.ToPromQl(new NumberLiteral(double.NegativeInfinity)).Should().Be("-Inf");
        }
        
        [Test]
        [TestCase("7.00:00:00", "7d")] // don't support converting to weeks for now
        [TestCase("5.00:00:00", "5d")]
        [TestCase("1.20:00:00", "1d20h")]
        [TestCase("0.16:17:18.019", "16h17m18s19ms")]
        public void Duration_ToPromQl(string input, string expected) => _printer.ToPromQl(new Duration(TimeSpan.Parse(input))).Should().Be(expected);

        [Test]
        public void Aggregate_ToPromQl()
        {
            _printer.ToPromQl(new AggregateExpr(Operators.Aggregates["sum"],
                new VectorSelector(new MetricIdentifier("test_expr")),
                null, 
                ImmutableArray<string>.Empty, 
                false
            )).Should().Be("sum(test_expr)");
        }
        
        [Test]
        public void Aggregate_WithLabels_ToPromQl()
        {
            _printer.ToPromQl(new AggregateExpr(Operators.Aggregates["sum"],
                new VectorSelector(new MetricIdentifier("test_expr")),
                null, 
                new [] { "one" }.ToImmutableArray(),
                false
            )).Should().Be("sum by (one) (test_expr)");
        }

        [Test]
        public void Aggregate_WithParam_ToPromQl()
        {
            _printer.ToPromQl(new AggregateExpr(Operators.Aggregates["quantile"],
                new VectorSelector(new MetricIdentifier("test_expr")),
                new NumberLiteral(1),
                new[] {"label1", "label2"}.ToImmutableArray(),
                true
            )).Should().Be("quantile without (label1, label2) (1, test_expr)");
        }
        
        [Test]
        public void VectorMatching_Default_ToPromQl() => _printer.ToPromQl(new VectorMatching(false)).Should().Be("");

        [Test]
        public void VectorMatching_Bool_ToPromQl() => _printer.ToPromQl(new VectorMatching(true)
            ).Should().Be("bool");

        [Test]
        public void VectorMatching_Ignoring_ToPromQl()
        {
            _printer.ToPromQl(new VectorMatching(
                Operators.VectorMatchCardinality.OneToOne,
                new[] {"one", "two"}.ToImmutableArray(),
                false,
                ImmutableArray<string>.Empty,
                false
            )).Should().Be("ignoring (one, two)");
        }
        
        [Test]
        public void VectorMatching_On_ToPromQl()
        {
            _printer.ToPromQl(new VectorMatching(
                Operators.VectorMatchCardinality.OneToOne,
                new[] {"one", "two"}.ToImmutableArray(),
                true,
                ImmutableArray<string>.Empty,
                false
            )).Should().Be("on (one, two)");
        }
        
        [Test]
        public void VectorMatching_OnNoLabels_ToPromQl()
        {
            _printer.ToPromQl(new VectorMatching(
                Operators.VectorMatchCardinality.OneToOne,
                ImmutableArray<string>.Empty,
                true,
                ImmutableArray<string>.Empty,
                false
            )).Should().Be("on ()");
        }
        
        [Test]
        public void VectorMatching_GroupRight_NoLabels_ToPromQl()
        {
            _printer.ToPromQl(new VectorMatching(
                Operators.VectorMatchCardinality.OneToMany,
                ImmutableArray<string>.Empty,
                false,
                ImmutableArray<string>.Empty,
                false
            )).Should().Be("group_right ()");
        }
        
        [Test]
        public void VectorMatching_GroupLeft_Labels_ToPromQl()
        {
            _printer.ToPromQl(new VectorMatching(
                Operators.VectorMatchCardinality.ManyToOne,
                ImmutableArray<string>.Empty,
                false,
                new[] {"one", "two"}.ToImmutableArray(),
                false
            )).Should().Be("group_left (one, two)");
        }

        [Test]
        public void Offset_Negative()
        {
            _printer.ToPromQl(new OffsetExpr(new VectorSelector(new MetricIdentifier("foo")),
                new Duration(TimeSpan.FromHours(-5))))
                .Should().Be("foo offset -5h");
        }

        // This expression doesn't have to be valid PromQL to be a useful test
        [Test]
        public void Complex_ToPromQl() =>
            _printer.ToPromQl(new BinaryExpr(
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
                    new FunctionCall(Functions.Map["vector"], new Expr[]
                    {
                        new OffsetExpr(new VectorSelector(new MetricIdentifier("this_is_a_metric")), new Duration(TimeSpan.FromMinutes(5)))
                    }.ToImmutableArray())
                ),
                Operators.Binary.Add,
                null
            )).Should().Be("(another_metric{one='test', two!='test2'}[1h][1d:5m]) + -vector(this_is_a_metric offset 5m)");
    }
}