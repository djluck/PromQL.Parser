using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using PromQL.Parser.Ast;
using Superpower;
using Superpower.Display;
using Superpower.Model;

namespace PromQL.Parser.Tests
{
    [TestFixture]
    public class ParserTests
    {
        [SetUp]
        public void SetUp()
        {
            AssertionOptions.AssertEquivalencyUsing(opts => 
                opts.IncludingAllRuntimeProperties()
                    .Excluding(p => p.Name == "TextSpan")
            );
        }
        
        [Test]
        public void StringLiteralDoubleQuote() => Parse(Parser.StringLiteral, "\"A string\"")
            .Should().Be(new StringLiteral('"', "A string"));
        
        [Test]
        public void StringLiteralDoubleQuoteEscaped() => Parse(Parser.StringLiteral, @"""\a\b\f\n\r\t\v\\\""""")
            .Should().Be(new StringLiteral('"', "\a\b\f\n\r\t\v\\\""));
        
        [Test]
        public void StringLiteralDoubleQuoteNewline() => 
            Assert.Throws<ParseException>(() => Parse(Parser.StringLiteral, "\"\n\""));
        
        [Test]
        public void StringLiteralSingleQuote() => Parse(Parser.StringLiteral, "'A string'")
            .Should().Be(new StringLiteral('\'', "A string"));

        [Test]
        public void StringLiteralSingleQuoteEscaped() => Parse(Parser.StringLiteral, @"'\a\b\f\n\r\t\v\\\''")
            .Should().Be(new StringLiteral('\'', "\a\b\f\n\r\t\v\\\'"));
        
        [Test]
        public void StringLiteralSingleQuoteNewline() => 
            Assert.Throws<ParseException>(() => Parse(Parser.StringLiteral, "'\n'"));
        
        [Test]
        public void StringLiteralRaw() => Parse(Parser.StringLiteral, "`A string`")
            .Should().Be(new StringLiteral('`', "A string"));
        
        [Test]
        public void StringLiteralRaw_Multiline() => Parse(Parser.StringLiteral, "`A\n string`")
            .Should().Be(new StringLiteral('`', "A\n string"));

        [Test]
        public void StringLiteralRaw_NoEscapes() => Parse(Parser.StringLiteral, @"`\a\b\f\n\r\t\v`")
            .Should().Be(new StringLiteral('`', @"\a\b\f\n\r\t\v"));
        
        [Test]
        [TestCase("2y52w365d25h10m30s100ms", "1460.01:10:30.100")]
        [TestCase("60w", "420.00:00:00")]
        [TestCase("48h", "2.00:00:00")]
        [TestCase("70m", "01:10:00")]
        [TestCase("180s", "00:03:00")]
        [TestCase("500ms", "00:00:00.500")]
        public void Duration(string input, string expected) => Parse(Parser.Duration, input)
            .Should().Be(new Duration(TimeSpan.Parse(expected)));
        
        
        [Test]
        [TestCase("1", 1.0)]
        [TestCase("5.", 5.0)]
        [TestCase("-5", -5.0)]
        [TestCase(".5", 0.5)]
        [TestCase("123.4567", 123.4567)]
        [TestCase("1e-5", 0.00001)]
        [TestCase("1e5", 100000)]
        [TestCase("1.55E+5", 155000)]
        public void Number(string input, double expected) => Parse(Parser.Number, input)
            .Should().Be(new NumberLiteral(expected));
        
        [Test]
        [TestCase("nan")]
        [TestCase("NaN")]
        public void Number_NaN(string input) => Parse(Parser.Number, input)
            .Should().Be(new NumberLiteral(double.NaN));
        
       [Test]
       [TestCase("Inf")]
       [TestCase("+inf")]
       public void Number_InfPos(string input) => Parse(Parser.Number, input)
           .Should().Be(new NumberLiteral(double.PositiveInfinity));
       
       [Test]
       [TestCase("-Inf")]
       [TestCase("-inf")]
       public void Number_InfNeg(string input) => Parse(Parser.Number, input)
           .Should().Be(new NumberLiteral(double.NegativeInfinity));


       [Test]
       [TestCaseSource(nameof(FunctionsAggregatesAndOperators))]
       public void LabelValueMatcher_FunctionsOperatorsAndKeywords(string identifier) =>
           Parse(Parser.LabelValueMatcher, identifier).Should().Be(identifier);

        [Test]
        public void LabelMatchers_Empty()
        {
            Parse(Parser.LabelMatchers, "{}")
                .Should().Be(new LabelMatchers(ImmutableArray<LabelMatcher>.Empty));
        }

        [Test]
        public void LabelMatchers_EmptyNoComma()
        {
            Assert.Throws<ParseException>(() => Parse(Parser.LabelMatchers, "{,}"));
        }
        
        [Test]
        public void LabelMatchers_One()
        {
            Parse(Parser.LabelMatchers, "{blah=\"my_label\"}")
                .Should().BeEquivalentTo(new LabelMatchers(new []
                {
                    new LabelMatcher("blah", Operators.LabelMatch.Equal, new StringLiteral('"', "my_label"))   
                }.ToImmutableArray()));
        }

        [Test]
        public void LabelMatchers_OneTrailingComma()
        {
            Parse(Parser.LabelMatchers, "{blah=\"my_label\" , }")
                .Should().BeEquivalentTo(new LabelMatchers(new[]
                {
                    new LabelMatcher("blah", Operators.LabelMatch.Equal, new StringLiteral('"', "my_label"))
                }.ToImmutableArray()));
        }
        
        [Test]
        public void LabelMatchers_Many()
        {
            Parse(Parser.LabelMatchers, "{ blah=\"my_label\", blah_123 != 'my_label', b123=~'label', b_!~'label' }")
                .Should().BeEquivalentTo(new LabelMatchers(new []
                {
                    new LabelMatcher("blah", Operators.LabelMatch.Equal, new StringLiteral('"', "my_label")),
                    new LabelMatcher("blah_123", Operators.LabelMatch.NotEqual, new StringLiteral('\'', "my_label")),
                    new LabelMatcher("b123", Operators.LabelMatch.Regexp, new StringLiteral('\'', "label")),
                    new LabelMatcher("b_", Operators.LabelMatch.NotRegexp, new StringLiteral('\'', "label"))
                }.ToImmutableArray()));
        }
        
        [Test]
        public void VectorSelector_MetricIdentifier()
        {
            Parse(Parser.VectorSelector, ":this_is_a_metric")
                .Should().BeEquivalentTo(new VectorSelector(new MetricIdentifier(":this_is_a_metric")));
        }
        
        [Test]
        public void VectorSelector_MetricIdentifier_And_LabelMatchers()
        {
            Parse(Parser.VectorSelector, ":this_is_a_metric { }")
                .Should().BeEquivalentTo(new VectorSelector(new MetricIdentifier(":this_is_a_metric"), new LabelMatchers(ImmutableArray<LabelMatcher>.Empty)));
        }
        
        [Test]
        public void VectorSelector_LabelMatchers()
        {
            Parse(Parser.VectorSelector, "{ }")
                .Should().BeEquivalentTo(new VectorSelector(new LabelMatchers(ImmutableArray<LabelMatcher>.Empty)));
        }

        [Test]
        public void MatrixSelector() => Parse(Parser.MatrixSelector, " metric { } [ 1m1s ] ")
            .Should().Be(new MatrixSelector(
                new VectorSelector(new MetricIdentifier("metric"), new LabelMatchers(ImmutableArray<LabelMatcher>.Empty)),
                new Duration(new TimeSpan(0, 1, 1))
            ));

        [Test]
        public void Offset_Vector()
        {
            var expr = Parse(Parser.Expr, " metric { } offset 1m");
            var offsetExpr = expr.Should().BeOfType<OffsetExpr>().Subject;
            offsetExpr.Expr.Should().BeOfType<VectorSelector>();
            offsetExpr.Duration.Value.Should().Be(TimeSpan.FromMinutes(1));
        } 
        
        [Test]
        public void Offset_MatrixSelector()
        {
            var expr = Parse(Parser.Expr, " metric { } [ 1m1s ] offset -7m");
            var offsetExpr = expr.Should().BeOfType<OffsetExpr>().Subject;
            offsetExpr.Expr.Should().BeOfType<MatrixSelector>();
            offsetExpr.Duration.Value.Should().Be(TimeSpan.FromMinutes(-7));
        } 
        
        [Test]
        public void Offset_Subquery()
        {
            var expr = Parse(Parser.Expr, " metric[ 1h:1m ] offset 1w");
            expr.Should().BeEquivalentTo(
                new OffsetExpr(
                    new SubqueryExpr(
                        new VectorSelector(new MetricIdentifier("metric")),
                        new Duration(TimeSpan.FromHours(1)),
                        new Duration(TimeSpan.FromMinutes(1))
                    ),
                    new Duration(TimeSpan.FromDays(7))
                )
            );
        } 
        
        [Test]
        public void Subquery_Offset()
        {
            var expr = Parse(Parser.Expr, " metric offset 1w [ 1h:1m ]");
            expr.Should().BeEquivalentTo(
                new SubqueryExpr(
                    new OffsetExpr(
                        new VectorSelector(new MetricIdentifier("metric")),
                        new Duration(TimeSpan.FromDays(7))
                    ),
                    new Duration(TimeSpan.FromHours(1)),
                    new Duration(TimeSpan.FromMinutes(1))
                )
            );
        } 
        
        [Test]
        public void ParenExpr_Empty()
        {
            Assert.Throws<ParseException>(() => Parse(Parser.Expr, " ( ) "));
        } 
        
        [Test]
        public void ParenExpr_MissingLeft()
        {
            Assert.Throws<ParseException>(() => Parse(Parser.Expr.AtEnd(), " ( 1 )) "));
        } 
        
        [Test]
        public void ParenExpr_MissingRight()
        {
            Assert.Throws<ParseException>(() => Parse(Parser.Expr, " (( 1 ) "));
        } 
        
        [Test]
        public void ParenExpr_Simple() => Parse(Parser.Expr, " (1) ")
            .Should().Be(new ParenExpression(new NumberLiteral(1.0)));
        
        [Test]
        public void ParenExpr_Nested() => Parse(Parser.Expr, " ((1)) ")
            .Should().Be(new ParenExpression(new ParenExpression(new NumberLiteral(1.0))));

        [Test]
        public void FunctionCall_Empty() => Parse(Parser.FunctionCall, "time ()")
            .Should().Be(new FunctionCall("time", ImmutableArray<Expr>.Empty));

        [Test]
        public void FunctionCall_InvalidFunction()
        {
            Assert.Throws<ParseException>(() => Parse(Parser.Expr, "this_doesnt_exist ()"));  
        }

        [Test]
        public void FunctionCall_OneArg() => Parse(Parser.Expr, "abs (1)")
            .Should().BeEquivalentTo(
                new FunctionCall("abs", new Expr[] { new NumberLiteral(1.0) }.ToImmutableArray())
            );
        
        [Test]
        // NOTE: we do not either validate the parameter count or types of functions 
        public void FunctionCall_MultiArg() => Parse(Parser.Expr, "abs (1, 2)")
            .Should().BeEquivalentTo(
                new FunctionCall("abs", new Expr[] { new NumberLiteral(1.0), new NumberLiteral(2.0) }.ToImmutableArray())
            );
        
        [Test]
        // NOTE: we do not either validate the parameter count or types of functions 
        public void FunctionCall_SnakeCase() => Parse(Parser.Expr, "absent_over_time (metric_name )")
            .Should().BeEquivalentTo(
                new FunctionCall("absent_over_time", new Expr[] { new VectorSelector(new MetricIdentifier("metric_name")) }.ToImmutableArray())
            );

        [Test]
        public void UnaryExpr_Plus() => Parse(Parser.UnaryExpr, "+1")
            .Should().Be(new UnaryExpr(Operators.Unary.Add, new NumberLiteral(1.0)));
        
        [Test]
        public void UnaryExpr_Minus() => Parse(Parser.UnaryExpr, "-1")
            .Should().Be(new UnaryExpr(Operators.Unary.Sub, new NumberLiteral(1.0)));
        
        [Test]
        public void Expr()
        {
            Parse(Parser.Expr, "'a string'").Should().Be(new StringLiteral('\'', "a string"));
            Parse(Parser.Expr, "1").Should().Be(new NumberLiteral(1.0));
            Parse(Parser.Expr, "1 + 1").Should().BeOfType<BinaryExpr>();
            Parse(Parser.Expr, "sum (some_expr)").Should().BeOfType<AggregateExpr>();
            Parse(Parser.Expr, "some_expr[1d:]").Should().BeOfType<SubqueryExpr>();
            Parse(Parser.Expr, "some_expr offset 1d").Should().BeOfType<OffsetExpr>();
            Parse(Parser.Expr, "some_expr").Should().BeOfType<VectorSelector>();
            Parse(Parser.Expr, "some_expr[1d]").Should().BeOfType<MatrixSelector>();
            Parse(Parser.Expr, "+(1)").Should().BeOfType<UnaryExpr>();
            Parse(Parser.Expr, "abs()").Should().BeOfType<FunctionCall>();
        }
        
        [Test]
        public void Expr_NumberWithSign()
        {
            // Make sure we don't parse this as a unary expression!
            Parse(Parser.Expr, "+1").Should().Be(new NumberLiteral(1));
            Parse(Parser.Expr, "-1").Should().Be(new NumberLiteral(-1));
        }

        [Test]
        [TestCaseSource(nameof(FunctionsAggregatesAndOperators))]
        public void Expr_FunctionMetricIdentifier(string input)
        {
            Parse(Parser.Expr, input).Should().BeOfType<VectorSelector>().Which
                .MetricIdentifier.Value.Should().Be(input);
        }
        
        [Test]
        public void Expr_NotKeywordMetricIdentifier()
        {
            Assert.Throws<ParseException>(() => Parse(Parser.Expr, "on {}"));
        }
        
        [Test]
        public void Expr_NameKeywordWorkaround()
        {
            Parse(Parser.Expr, "{__name__='on'}").Should().BeEquivalentTo(
                new VectorSelector(new LabelMatchers(new []
                {
                    new LabelMatcher("__name__", Operators.LabelMatch.Equal, new StringLiteral('\'', "on"))
                }.ToImmutableArray()))
            );
        }
        
        [Test]
        public void Expr_Complex()
        {
            var toParse =
                "sum by(job, mode) (rate(node_cpu_seconds_total[1m])) / " +
                "on(job) group_left sum by(job)(rate(node_cpu_seconds_total[1m]))";

            Parse(Parser.Expr, toParse).Should().BeEquivalentTo(
                new BinaryExpr(
                    new AggregateExpr(
                        "sum",
                        new FunctionCall(
                            "rate", 
                            new Expr[]{
                                new MatrixSelector(
                                    new VectorSelector(new MetricIdentifier("node_cpu_seconds_total")),
                                    new Duration(TimeSpan.FromMinutes(1))
                                )
                            }.ToImmutableArray()
                        ),
                        null, 
                        new []{"job", "mode"}.ToImmutableArray(),
                        false
                    ),
                    new AggregateExpr(
                        "sum",
                        new FunctionCall(
                            "rate", 
                            new Expr[]{
                                new MatrixSelector(
                                    new VectorSelector(new MetricIdentifier("node_cpu_seconds_total")),
                                    new Duration(TimeSpan.FromMinutes(1))
                                )
                            }.ToImmutableArray()
                        ),
                        null, 
                        new []{ "job" }.ToImmutableArray(),
                        false
                    ),
                    Operators.Binary.Div,
                    new VectorMatching(
                        Operators.VectorMatchCardinality.ManyToOne, 
                        new [] { "job"}.ToImmutableArray(),
                        true,
                        ImmutableArray<string>.Empty, 
                        false
                    )
                )
            );
        }
        
        // TODO probably need to expand upon our invalid test cases significantly
        [Test]
        public void Invalid_Expr()
        {
            Assert.Throws<ParseException>(() => Parse(Parser.Expr, "!= 'blah'"));
        }
        
        [Test]
        public void Invalid_DurationExpr()
        {
            // Prior to this test, a very unhelpful error message would be returned (parsing would complain about an invalid
            // opening parenthesis)
            Assert.Throws<ParseException>(() => Parse(Parser.Expr, "sum(my_metric[window])")).Message
                .Should().Be("Syntax error (line 1, column 15): unexpected identifier `window`, expected duration.");
        }

        [Test]
        public void ParseExpression_With_Comments()
        {
            Parser.ParseExpression(" #some comment \n 1 \n # another comment!").Should().Be(new NumberLiteral(1.0));
        }

        [Test]
        public void GroupingLabels_Nothing()
        {
            Assert.Throws<ParseException>(() => Parse(Parser.GroupingLabels, " "));
        }

        [Test]
        public void GroupingLabels_Empty() => Parse(Parser.GroupingLabels, " ( )")
            .Should().BeEmpty();
        
        [Test]
        public void GroupingLabels_One() => Parse(Parser.GroupingLabels, " ( one ) ")
            .Should().Equal("one");
        
        [Test]
        public void GroupingLabels_Many() => Parse(Parser.GroupingLabels, " ( one, two, three ) ")
            .Should().Equal("one", "two", "three");

        [Test]
        public void VectorMatching_Bool() => Parse(Parser.VectorMatching, "bool")
            .Should().BeEquivalentTo(new VectorMatching(
                Operators.VectorMatchCardinality.OneToOne, 
                ImmutableArray<string>.Empty, 
                false, 
                ImmutableArray<string>.Empty, 
                true)
            );
        
        [Test]
        public void VectorMatching_Ignoring() => Parse(Parser.VectorMatching, "ignoring (one, two)")
            .Should().BeEquivalentTo(new VectorMatching(
                Operators.VectorMatchCardinality.OneToOne, 
                new [] { "one", "two" }.ToImmutableArray(), 
                false, 
                ImmutableArray<string>.Empty, 
                false)
            );

        [Test]
        public void VectorMatching_On() => Parse(Parser.VectorMatching, "on ()")
            .Should().BeEquivalentTo(new VectorMatching(
                Operators.VectorMatchCardinality.OneToOne, 
                ImmutableArray<string>.Empty,  
                true, 
                ImmutableArray<string>.Empty, 
                false)
            );
        
        [Test]
        public void VectorMatching_Bool_On() => Parse(Parser.VectorMatching, "bool on ()")
            .Should().BeEquivalentTo(new VectorMatching(
                Operators.VectorMatchCardinality.OneToOne, 
                ImmutableArray<string>.Empty,  
                true, 
                ImmutableArray<string>.Empty, 
                true)
            );
        
        [Test]
        public void VectorMatching_GroupLeft() => Parse(Parser.VectorMatching, "on () group_left ()")
            .Should().BeEquivalentTo(new VectorMatching(
                Operators.VectorMatchCardinality.ManyToOne, 
                ImmutableArray<string>.Empty,  
                true, 
                ImmutableArray<string>.Empty, 
                false)
            );
        
        [Test]
        public void VectorMatching_GroupLeftEmpty() => Parse(Parser.VectorMatching, "on () group_left")
            .Should().BeEquivalentTo(new VectorMatching(
                Operators.VectorMatchCardinality.ManyToOne, 
                ImmutableArray<string>.Empty,  
                true, 
                ImmutableArray<string>.Empty, 
                false)
            );
        
        [Test]
        public void VectorMatching_GroupRight() => Parse(Parser.VectorMatching, "on () group_right (one, two)")
            .Should().BeEquivalentTo(new VectorMatching(
                Operators.VectorMatchCardinality.OneToMany, 
                ImmutableArray<string>.Empty,  
                true, 
                new []{ "one","two"}.ToImmutableArray(), 
                false)
            );
        
        [Test]
        [TestCase("group_left ()")]
        [TestCase("group_right (one)")]
        public void VectorMatching_GroupInvalid(string input)
        {
            Assert.Throws<ParseException>(() => Parse(Parser.VectorMatching.AtEnd(), input));
        }

        [Test]
        [TestCase("1 + 1", Operators.Binary.Add)]
        [TestCase("1 - 1", Operators.Binary.Sub)]
        [TestCase("1 * 1", Operators.Binary.Mul)]
        [TestCase("1 / 1", Operators.Binary.Div)]
        [TestCase("1 % 1", Operators.Binary.Mod)]
        [TestCase("1 ^ 1", Operators.Binary.Pow)]
        [TestCase("1 == 1", Operators.Binary.Eql)]
        [TestCase("1 != 1", Operators.Binary.Neq)]
        [TestCase("1 > 1", Operators.Binary.Gtr)]
        [TestCase("1 < 1", Operators.Binary.Lss)]
        [TestCase("1 >= 1", Operators.Binary.Gte)]
        [TestCase("1 <= 1", Operators.Binary.Lte)]
        [TestCase("1 and 1", Operators.Binary.And)]
        [TestCase("1 or 1", Operators.Binary.Or)]
        [TestCase("1 unless 1", Operators.Binary.Unless)]
        public void BinaryExpr_Operators(string input, Operators.Binary expected)
        {
            Parse(Parser.BinaryExpr, input).As<BinaryExpr>().Operator.Should().Be(expected);
        }
        
        [Test]
        public void BinaryExpr_SimpleVector()
        {
            Parse(Parser.BinaryExpr, "metric_name + {label='one'}")
                .Should().BeEquivalentTo(new BinaryExpr(
                    new VectorSelector(new MetricIdentifier("metric_name")),
                    new VectorSelector(new LabelMatchers(new []
                    {
                        new LabelMatcher("label", Operators.LabelMatch.Equal, new StringLiteral('\'', "one"))
                    }.ToImmutableArray())),
                    Operators.Binary.Add,
                    new VectorMatching()
                ));
        }
        
        [Test]
        public void BinaryExpr_Repetitive()
        {
            var result = Parse(Parser.BinaryExpr, "1 + 2 + 3") as BinaryExpr;
            result.LeftHandSide.Should().Be(new NumberLiteral(1.0));
            var binExpr = result.RightHandSide.Should().BeOfType<BinaryExpr>();
            
            binExpr.Which.LeftHandSide.Should().Be(new NumberLiteral(2.0));
            binExpr.Which.RightHandSide.Should().Be(new NumberLiteral(3.0));
        }
        
        [Test]
        public void BinaryExpr_Nested()
        {
            var result = Parse(Parser.BinaryExpr, "((1 + 2) + 3) + 4");
            result.As<BinaryExpr>().LeftHandSide.Should().BeOfType<ParenExpression>().Which
                .Expr.Should().BeOfType<BinaryExpr>().Which
                .LeftHandSide.Should().BeOfType<ParenExpression>().Which
                .Expr.Should().BeOfType<BinaryExpr>().Which
                .LeftHandSide.Should().Be(new NumberLiteral(1.0));
            
            Parse(Parser.Expr, "(100 * (1) / 128)");
        }
        
        [Test]
        public void BinaryExpr_VectorMatching()
        {
            Parse(Parser.BinaryExpr, "1 + bool 2")
                .Should().BeEquivalentTo(new BinaryExpr(
                    new NumberLiteral(1.0),
                    new NumberLiteral(2),
                    Operators.Binary.Add,
                    new VectorMatching(true)
                ));
        }

        [Test]
        [TestCase("avg (blah)", "avg")]
        [TestCase("bottomk (blah)", "bottomk")]
        [TestCase("count (blah)", "count")]
        [TestCase("count_values (blah)", "count_values")]
        [TestCase("group (blah)", "group")]
        [TestCase("max (blah)", "max")]
        [TestCase("min (blah)", "min")]
        [TestCase("quantile (blah)", "quantile")]
        [TestCase("stddev (blah)", "stddev")]
        [TestCase("stdvar (blah)", "stdvar")]
        [TestCase("sum (blah)", "sum")]
        [TestCase("topk (blah)", "topk")]
        public void AggregateExpr_Operator(string input, string expected) => Parse(Parser.AggregateExpr, input)
            .OperatorName.Should().Be(expected);

        [Test]
        public void AggregateExpr_NoMod() => Parse(Parser.AggregateExpr, "sum (blah)").Should().BeEquivalentTo(
            new AggregateExpr(
                "sum",
                new VectorSelector(new MetricIdentifier("blah")),
                null,
                ImmutableArray<string>.Empty, 
                false
            ));
        
        [Test]
        public void AggregateExpr_LeadingModBy() => Parse(Parser.AggregateExpr, "sum by (one, two) (blah)").Should().BeEquivalentTo(
            new AggregateExpr(
                "sum",
                new VectorSelector(new MetricIdentifier("blah")),
                null,
                new string[] {"one", "two"}.ToImmutableArray(),
                false
            ));
        
        [Test]
        public void AggregateExpr_TrailingModWithout() => Parse(Parser.AggregateExpr, "sum (blah) without (one)").Should().BeEquivalentTo(
            new AggregateExpr(
                "sum",
                new VectorSelector(new MetricIdentifier("blah")),
                null,
                new string[] {"one" }.ToImmutableArray(),
                true
            ));
        
        [Test]
        public void AggregateExpr_TwoArgs() => Parse(Parser.AggregateExpr, "quantile (0.5, blah)").Should().BeEquivalentTo(
            new AggregateExpr(
                "quantile",
                new VectorSelector(new MetricIdentifier("blah")),
                new NumberLiteral(0.5),
                ImmutableArray<string>.Empty, 
                false
            ));
        
        [Test]
        public void AggregateExpr_LabelNameAggOp()
        {
            Parse(Parser.Expr, @"sum by (quantile) (test)").Should().BeOfType<AggregateExpr>();
        }

        [Test]
        public void Subquery_WithStep() => Parse(Parser.Expr, "blah[1h:1m]").Should().BeEquivalentTo(
            new SubqueryExpr(
                new VectorSelector(new MetricIdentifier("blah")),
                new Duration(TimeSpan.FromHours(1)),
                new Duration(TimeSpan.FromMinutes(1))
            ));
         
         [Test]
         public void Subquery_WithoutStep() => Parse(Parser.Expr, "blah[1d:]").Should().BeEquivalentTo(
             new SubqueryExpr(
                 new VectorSelector(new MetricIdentifier("blah")),
                 new Duration(TimeSpan.FromDays(1)),
                 null
             ));

         [Test]
         public void Subquery_Expressions()
         {
             Parse(Parser.Expr, "vector(1) [1h:]").Should().BeOfType<SubqueryExpr>().Which
                 .Expr.Should().BeOfType<FunctionCall>();
             
             Parse(Parser.Expr, "1 + 1 [1h:]").Should().BeOfType<BinaryExpr>().Which
                 .RightHandSide.Should().BeOfType<SubqueryExpr>();
             
             Parse(Parser.Expr, "(1 + 1) [1h:]").Should().BeOfType<SubqueryExpr>().Which
                 .Expr.Should().BeOfType<ParenExpression>();
             
             Parse(Parser.Expr, "blah{} [1h:]").Should().BeOfType<SubqueryExpr>().Which
                 .Expr.Should().BeOfType<VectorSelector>();
         }

         public static T Parse<T>(TokenListParser<PromToken, T> parser, string input)
        {
            var tokens = new Tokenizer().Tokenize(input);
            try
            {
                return parser.AtEnd().Parse(tokens);
            }
            catch (ParseException)
            {
                Console.WriteLine($"Tokens are: {string.Join(",", tokens)})");
                throw;
            }
        }

         public static IEnumerable<string[]> FunctionsOperatorsAndAggregates()
         {
             // aggregate ops
             yield return new[] {"avg"};
             yield return new[] {"sum"};
             yield return new[] {"quantile"};
             yield return new[] {"max"};
             yield return new[] {"min"};

             // functions
             yield return new[] {"avg_over_time"};
             yield return new[] {"time"};
             yield return new[] {"rate"};

             // operators
             yield return new[] {"or"};
             yield return new[] {"and"};
             yield return new[] {"unless"};
         }

         public static IEnumerable<string[]> FunctionsOperatorsAggregatesAndKeywords()
         {
             foreach (var s in FunctionsAggregatesAndOperators())
                 yield return s;
             
             // keywords
             yield return new[] {"group_left"};
             yield return new[] {"group_right"};
             yield return new[] {"without"};
             yield return new[] {"ignoring"};
             yield return new[] {"by"};
             yield return new[] {"bool"};
         }

         public static IEnumerable<string[]> FunctionsAggregatesAndOperators()
         {
             // aggregate ops
             yield return new[] {"avg"};
             yield return new[] {"sum"};
             yield return new[] {"quantile"};
             yield return new[] {"max"};
             yield return new[] {"min"};

             // functions
             yield return new[] {"avg_over_time"};
             yield return new[] {"time"};
             yield return new[] {"rate"};

             // operators
             yield return new[] {"or"};
             yield return new[] {"and"};
             yield return new[] {"unless"};
         }
    }
}
