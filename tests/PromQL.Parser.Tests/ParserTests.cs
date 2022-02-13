using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using PromQL.Parser.Ast;
using Superpower;
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
            .Should().Be(new StringLiteral('"', "A string", new TextSpan("\"A string\"")));
        
        [Test]
        public void StringLiteralDoubleQuoteEscaped() => Parse(Parser.StringLiteral, @"""\a\b\f\n\r\t\v\\\""""")
            .Should().Be(new StringLiteral('"', "\a\b\f\n\r\t\v\\\"", new TextSpan(@"""\a\b\f\n\r\t\v\\\""""")));
        
        [Test]
        public void StringLiteralDoubleQuoteNewline() => 
            Assert.Throws<ParseException>(() => Parse(Parser.StringLiteral, "\"\n\""));
        
        [Test]
        public void StringLiteralSingleQuote() => Parse(Parser.StringLiteral, "'A string'")
            .Should().Be(new StringLiteral('\'', "A string", new TextSpan("'A string'")));

        [Test]
        public void StringLiteralSingleQuoteEscaped() => Parse(Parser.StringLiteral, @"'\a\b\f\n\r\t\v\\\''")
            .Should().Be(new StringLiteral('\'', "\a\b\f\n\r\t\v\\\'", new TextSpan(@"'\a\b\f\n\r\t\v\\\''")));
        
        [Test]
        public void StringLiteralSingleQuoteNewline() => 
            Assert.Throws<ParseException>(() => Parse(Parser.StringLiteral, "'\n'"));
        
        [Test]
        public void StringLiteralRaw() => Parse(Parser.StringLiteral, "`A string`")
            .Should().Be(new StringLiteral('`', "A string", new TextSpan("`A string`")));
        
        [Test]
        public void StringLiteralRaw_Multiline() => Parse(Parser.StringLiteral, "`A\n string`")
            .Should().Be(new StringLiteral('`', "A\n string", new TextSpan("`A\n string`")));

        [Test]
        public void StringLiteralRaw_NoEscapes() => Parse(Parser.StringLiteral, @"`\a\b\f\n\r\t\v`")
            .Should().Be(new StringLiteral('`', @"\a\b\f\n\r\t\v", new TextSpan(@"`\a\b\f\n\r\t\v`")));
        
        [Test]
        [TestCase("2y52w365d25h10m30s100ms", "1460.01:10:30.100")]
        [TestCase("60w", "420.00:00:00")]
        [TestCase("48h", "2.00:00:00")]
        [TestCase("70m", "01:10:00")]
        [TestCase("180s", "00:03:00")]
        [TestCase("500ms", "00:00:00.500")]
        public void Duration(string input, string expected) => Parse(Parser.Duration, input)
            .Should().Be(new Duration(TimeSpan.Parse(expected), new TextSpan(input)));
        
        
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
            .Should().Be(new NumberLiteral(expected, new TextSpan(input)));
        
        [Test]
        [TestCase("nan")]
        [TestCase("NaN")]
        public void Number_NaN(string input) => Parse(Parser.Number, input)
            .Should().Be(new NumberLiteral(double.NaN, new TextSpan(input)));
        
       [Test]
       [TestCase("Inf")]
       [TestCase("+inf")]
       public void Number_InfPos(string input) => Parse(Parser.Number, input)
           .Should().Be(new NumberLiteral(double.PositiveInfinity, new TextSpan(input)));
       
       [Test]
       [TestCase("-Inf")]
       [TestCase("-inf")]
       public void Number_InfNeg(string input) => Parse(Parser.Number, input)
           .Should().Be(new NumberLiteral(double.NegativeInfinity, new TextSpan(input)));


       [Test]
       [TestCaseSource(nameof(FunctionsAggregatesAndOperators))]
       public void LabelValueMatcher_FunctionsOperatorsAndKeywords(string identifier) =>
           Parse(Parser.LabelValueMatcher, identifier).Value.Should().Be(identifier);

        [Test]
        public void LabelMatchers_Empty()
        {
            const string input = "{}";
            Parse(Parser.LabelMatchers, input)
                .Should().Be(new LabelMatchers(ImmutableArray<LabelMatcher>.Empty, new TextSpan(input)));
        }

        [Test]
        public void LabelMatchers_EmptyNoComma()
        {
            Assert.Throws<ParseException>(() => Parse(Parser.LabelMatchers, "{,}"));
        }
        
        [Test]
        public void LabelMatchers_One()
        {
            const string input = "{blah=\"my_label\"}";
            Parse(Parser.LabelMatchers, input)
                .Should().BeEquivalentTo(
                    new LabelMatchers(new []
                        {
                            new LabelMatcher(
                                "blah", 
                                Operators.LabelMatch.Equal, 
                                new StringLiteral('"', "my_label", new TextSpan(input, new Position(6, 0, 0), 10)),
                                new TextSpan(input, new Position(1, 0, 0), 15)
                            )   
                        }.ToImmutableArray(),
                        Span: new TextSpan(input)
                    )
                );
        }

        [Test]
        public void LabelMatchers_OneTrailingComma()
        {
            const string input = "{blah=\"my_label\" , }";
            Parse(Parser.LabelMatchers, input)
                .Should().BeEquivalentTo(
                    new LabelMatchers(
                        new[]
                        {
                            new LabelMatcher(
                                "blah", 
                                Operators.LabelMatch.Equal, 
                                new StringLiteral('"', "my_label", new TextSpan(input, new Position(6, 0, 0), 10)),
                                new TextSpan(input, new Position(1, 0, 0), 15)
                            ),
                        }.ToImmutableArray(),
                        new TextSpan(input)
                    )
                );
        }
        
        [Test]
        public void LabelMatchers_Many()
        {
            Parse(Parser.LabelMatchers, "{ blah=\"my_label\", blah_123 != 'my_label', b123=~'label', b_!~'label' }")
                .Should().BeEquivalentTo(
                    new LabelMatchers(
                        new []
                        {
                            new LabelMatcher("blah", Operators.LabelMatch.Equal, new StringLiteral('"', "my_label")),
                            new LabelMatcher("blah_123", Operators.LabelMatch.NotEqual, new StringLiteral('\'', "my_label")),
                            new LabelMatcher("b123", Operators.LabelMatch.Regexp, new StringLiteral('\'', "label")),
                            new LabelMatcher("b_", Operators.LabelMatch.NotRegexp, new StringLiteral('\'', "label"))
                        }.ToImmutableArray()
                    ),
                    // Don't assert over parsed Span positions, will be tedious to specify all positions
                    cfg => cfg.Excluding(x => x.Name == "Span")
                );
        }
        
        [Test]
        public void VectorSelector_MetricIdentifier()
        {
            const string input = ":this_is_a_metric";
            Parse(Parser.VectorSelector, input)
                .Should().BeEquivalentTo(
                    new VectorSelector(new MetricIdentifier(input, new TextSpan(input)), new TextSpan(input))
                );
        }
        
        [Test]
        public void VectorSelector_MetricIdentifier_And_LabelMatchers()
        {
            const string input = ":this_is_a_metric { }";
            Parse(Parser.VectorSelector, input)
                .Should().BeEquivalentTo(
                    new VectorSelector(
                        new MetricIdentifier(":this_is_a_metric", new TextSpan(input, new Position(0, 0, 0), 17)),
                        new LabelMatchers(ImmutableArray<LabelMatcher>.Empty, new TextSpan(input, new Position(18, 0, 0), 3)),
                        new TextSpan(input)
                    )
                );
        }
        
        [Test]
        public void VectorSelector_LabelMatchers()
        {
            const string input = "{ }";
            Parse(Parser.VectorSelector, input)
                .Should().BeEquivalentTo(
                    new VectorSelector(
                        new LabelMatchers(ImmutableArray<LabelMatcher>.Empty, new TextSpan(input)),
                        new TextSpan(input)
                    )
                );
        }

        [Test]
        public void MatrixSelector()
        {
            var input = "metric { } [ 1m1s ]";
            Parse(Parser.MatrixSelector, input)
                .Should().Be(new MatrixSelector(
                    new VectorSelector(
                        new MetricIdentifier("metric", new TextSpan(input, Position.Zero, 6)), 
                        new LabelMatchers(ImmutableArray<LabelMatcher>.Empty, new TextSpan(input, new Position(7, 0, 0), 3)),
                        new TextSpan(input, Position.Zero, 10)
                    ),
                    new Duration(new TimeSpan(0, 1, 1), new TextSpan(input, new Position(13, 0, 0), 4)),
                    new TextSpan(input)
                ));
        }

        [Test]
        public void Offset_Vector()
        {
            const string input = "metric { } offset 1m";
            var expr = Parse(Parser.Expr, input);
            var offsetExpr = expr.Should().BeOfType<OffsetExpr>().Subject;
            offsetExpr.Expr.Should().BeOfType<VectorSelector>();
            offsetExpr.Duration.Value.Should().Be(TimeSpan.FromMinutes(1));
            offsetExpr.Span.Should().Be(new TextSpan(input));
        } 
        
        [Test]
        public void Offset_MatrixSelector()
        {
            const string input = "metric { } [ 1m1s ] offset -7m";
            var expr = Parse(Parser.Expr, input);
            var offsetExpr = expr.Should().BeOfType<OffsetExpr>().Subject;
            offsetExpr.Expr.Should().BeOfType<MatrixSelector>();
            offsetExpr.Duration.Value.Should().Be(TimeSpan.FromMinutes(-7));
            offsetExpr.Span.Should().Be(new TextSpan(input));
        } 
        
        [Test]
        public void Offset_Subquery()
        {
            var input = "metric[ 1h:1m ] offset 1w";
            var expr = Parse(Parser.Expr, input);
            expr.Should().BeEquivalentTo(
                new OffsetExpr(
                    new SubqueryExpr(
                        new VectorSelector(new MetricIdentifier("metric")),
                        new Duration(TimeSpan.FromHours(1)),
                        new Duration(TimeSpan.FromMinutes(1))
                    ),
                    new Duration(TimeSpan.FromDays(7))
                ),
                // Don't assert over parsed Span positions, will be tedious to specify all positions
                cfg => cfg.Excluding(x => x.Name == "Span")
            );
            expr.Span.Should().Be(new TextSpan(input));
        } 
        
        [Test]
        public void Subquery_Offset()
        {
            const string input = "metric offset 1w [ 1h:1m ]";
            var expr = Parse(Parser.Expr, input);
            expr.Should().BeEquivalentTo(
                new SubqueryExpr(
                    new OffsetExpr(
                        new VectorSelector(new MetricIdentifier("metric")),
                        new Duration(TimeSpan.FromDays(7))
                    ),
                    new Duration(TimeSpan.FromHours(1)),
                    new Duration(TimeSpan.FromMinutes(1))
                ),
                // Don't assert over parsed Span positions, will be tedious to specify all positions
                cfg => cfg.Excluding(x => x.Name == "Span")
            );
            expr.Span.Should().Be(new TextSpan(input));
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
        public void ParenExpr_Simple()
        {
            var input = " (1) ";
            var parsed = Parse(Parser.Expr, input)
                .Should().Be(
                    new ParenExpression(
                        new NumberLiteral(1.0, new TextSpan(input, new Position(2, 0, 0), 1)),
                        new TextSpan(input, new Position(1, 0, 0), 3)
                    )
                );
        }

        [Test]
        public void ParenExpr_Nested()
        {
            var input = "((1))";
            var parsed = Parse(Parser.Expr, input)
                .Should().Be(
                    new ParenExpression(
                        new ParenExpression(
                            new NumberLiteral(1.0, new TextSpan(input, new Position(2, 0, 0), 1)),
                            new TextSpan(input, new Position(1, 0, 0), 3)
                        ),
                        new TextSpan(input)
                    )
                );
        }

        [Test]
        public void FunctionCall_Empty()
        {
            var input = "time ()";
            Parse(Parser.FunctionCall, input)
                .Should().Be(new FunctionCall(Functions.Map["time"], ImmutableArray<Expr>.Empty, new TextSpan(input)));
        }
        
        [Test]
        public void FunctionCall_InvalidFunction()
        {
            Assert.Throws<ParseException>(() => Parse(Parser.Expr, "this_doesnt_exist ()"));  
        }

        [Test]
        public void FunctionCall_OneArg() => Parse(Parser.Expr, "abs (1)")
            .Should().BeEquivalentTo(
                new FunctionCall(Functions.Map["abs"], new Expr[] { new NumberLiteral(1.0) }.ToImmutableArray()),
                // Don't assert over parsed Span positions, will be tedious to specify all positions
                cfg => cfg.Excluding(x => x.Name == "Span")
            );
        
        [Test]
        // NOTE: we do not either validate the parameter count or types of functions 
        public void FunctionCall_MultiArg() => Parse(Parser.Expr, "abs (1, 2)")
            .Should().BeEquivalentTo(
                new FunctionCall(Functions.Map["abs"], new Expr[] { new NumberLiteral(1.0), new NumberLiteral(2.0) }.ToImmutableArray()),
                // Don't assert over parsed Span positions, will be tedious to specify all positions
                cfg => cfg.Excluding(x => x.Name == "Span")
            );
        
        [Test]
        // NOTE: we do not either validate the parameter count or types of functions 
        public void FunctionCall_SnakeCase() => Parse(Parser.Expr, "absent_over_time (metric_name )")
            .Should().BeEquivalentTo(
                new FunctionCall(Functions.Map["absent_over_time"], new Expr[] { new VectorSelector(new MetricIdentifier("metric_name")) }.ToImmutableArray()),
                // Don't assert over parsed Span positions, will be tedious to specify all positions
                cfg => cfg.Excluding(x => x.Name == "Span")
            );

        [Test]
        public void UnaryExpr_Plus()
        {
            const string input = "+1";
            Parse(Parser.UnaryExpr, input)
                .Should().Be(
                    new UnaryExpr(
                        Operators.Unary.Add, 
                        new NumberLiteral(1.0, new TextSpan(input, new Position(1, 0 , 0), 1)), 
                        new TextSpan(input)
                    )
                );
        }
        
        [Test]
        public void UnaryExpr_Minus()
        {
            const string input = "-1";
            Parse(Parser.UnaryExpr, input)
                .Should().Be(
                    new UnaryExpr(
                        Operators.Unary.Sub, 
                        new NumberLiteral(1.0, new TextSpan(input, new Position(1, 0 , 0), 1)), 
                        new TextSpan(input)
                    )
                );
        }

        [Test]
        public void Expr()
        {
            Parse(Parser.Expr, "'a string'").Should().BeOfType<StringLiteral>();
            Parse(Parser.Expr, "1").Should().BeOfType<NumberLiteral>();
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
            Parse(Parser.Expr, "+1").Should().Be(new NumberLiteral(1, new TextSpan("+1")));
            Parse(Parser.Expr, "-1").Should().Be(new NumberLiteral(-1, new TextSpan("-1")));
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
                }.ToImmutableArray())),
                // Don't assert over parsed Span positions, will be tedious to specify all positions
                cfg => cfg.Excluding(x => x.Name == "Span")
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
                            Functions.Map["rate"], 
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
                            Functions.Map["rate"], 
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
                ),
                // Don't assert over parsed Span positions, will be tedious to specify all positions
                cfg => cfg.Excluding(x => x.Name == "Span")
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
            Parser.ParseExpression(" #some comment \n 1 \n # another comment!").Should().BeOfType<NumberLiteral>();
        }

        [Test]
        public void GroupingLabels_Nothing()
        {
            Assert.Throws<ParseException>(() => Parse(Parser.GroupingLabels, " "));
        }

        [Test]
        public void GroupingLabels_Empty() => Parse(Parser.GroupingLabels, " ( )")
            .Value.Should().BeEmpty();
        
        [Test]
        public void GroupingLabels_One() => Parse(Parser.GroupingLabels, " ( one ) ")
            .Value.Should().Equal("one");

        [Test]
        public void GroupingLabels_Many()
        {
            var parsed = Parse(Parser.GroupingLabels, " ( one, two, three ) ");
            parsed.Value.Should().Equal("one", "two", "three");
            parsed.Span.ToString().Should().Be("( one, two, three )");
        }

        [Test]
        public void VectorMatching_Bool()
        {
            const string input = "bool";
            Parse(Parser.VectorMatching, input)
                .Should().BeEquivalentTo(new VectorMatching(
                    Operators.VectorMatchCardinality.OneToOne,
                    ImmutableArray<string>.Empty,
                    false,
                    ImmutableArray<string>.Empty,
                    true,
                    new TextSpan(input))
                );
        }

        [Test]
        public void VectorMatching_Ignoring()
        {
            const string input = "ignoring (one, two)";
            
            Parse(Parser.VectorMatching, input)
                .Should().BeEquivalentTo(new VectorMatching(
                    Operators.VectorMatchCardinality.OneToOne,
                    new[] {"one", "two"}.ToImmutableArray(),
                    false,
                    ImmutableArray<string>.Empty,
                    false,
                    new TextSpan(input))
                );
        }

        [Test]
        public void VectorMatching_On()
        {
            const string input = "on ()";
            
            Parse(Parser.VectorMatching, input)
                .Should().BeEquivalentTo(new VectorMatching(
                    Operators.VectorMatchCardinality.OneToOne,
                    ImmutableArray<string>.Empty,
                    true,
                    ImmutableArray<string>.Empty,
                    false,
                    new TextSpan(input))
                );
        }

        [Test]
        public void VectorMatching_Bool_On()
        {
            const string input = "bool on ()";
            Parse(Parser.VectorMatching, input)
                .Should().BeEquivalentTo(new VectorMatching(
                    Operators.VectorMatchCardinality.OneToOne,
                        ImmutableArray<string>.Empty,
                        true,
                        ImmutableArray<string>.Empty,
                        true,
                        new TextSpan(input)
                    )
                );
        }

        [Test]
        public void VectorMatching_GroupLeft()
        {
            const string input = "on () group_left ()";
            Parse(Parser.VectorMatching, input)
                .Should().BeEquivalentTo(new VectorMatching(
                    Operators.VectorMatchCardinality.ManyToOne,
                        ImmutableArray<string>.Empty,
                        true,
                        ImmutableArray<string>.Empty,
                        false,
                        new TextSpan(input)
                    )
                );
        }

        [Test]
        public void VectorMatching_GroupLeftEmpty()
        {
            const string input = "on () group_left";
            
            Parse(Parser.VectorMatching, input)
                .Should().BeEquivalentTo(new VectorMatching(
                    Operators.VectorMatchCardinality.ManyToOne,
                    ImmutableArray<string>.Empty,
                    true,
                    ImmutableArray<string>.Empty,
                    false,
                    new TextSpan(input))
                );
        }

        [Test]
        public void VectorMatching_GroupRight()
        {
            const string input = "on () group_right (one, two)";
            Parse(Parser.VectorMatching, input)
                .Should().BeEquivalentTo(
                    new VectorMatching(
                        Operators.VectorMatchCardinality.OneToMany, 
                        ImmutableArray<string>.Empty,  
                        true, 
                        new []{ "one","two"}.ToImmutableArray(), 
                        false,
                        new TextSpan(input)
                    )
                );
        } 
        
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
            var parsed = Parse(Parser.BinaryExpr, input).As<BinaryExpr>();
            parsed.Operator.Should().Be(expected);
            parsed.Span.Should().Be(new TextSpan(input));
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
                    ),
                    // Don't assert over parsed Span positions, will be tedious to specify all positions
                    cfg => cfg.Excluding(x => x.Name == "Span")
                );
        }
        
        [Test]
        public void BinaryExpr_Repetitive()
        {
            var input = "1 + 2 + 3";
            var result = Parse(Parser.BinaryExpr, input) as BinaryExpr;
            result.LeftHandSide.Should().Be(new NumberLiteral(1.0, new TextSpan(input, new Position(0, 0, 0), 1)));
            var binExpr = result.RightHandSide.Should().BeOfType<BinaryExpr>();
            
            binExpr.Which.LeftHandSide.Should().Be(new NumberLiteral(2.0, new TextSpan(input, new Position(4, 0, 0), 1)));
            binExpr.Which.RightHandSide.Should().Be(new NumberLiteral(3.0, new TextSpan(input, new Position(8, 0, 0), 1)));
        }
        
        [Test]
        public void BinaryExpr_Nested()
        {
            var result = Parse(Parser.BinaryExpr, "((1 + 2) + 3) + 4");
            result.As<BinaryExpr>().LeftHandSide.Should().BeOfType<ParenExpression>().Which
                .Expr.Should().BeOfType<BinaryExpr>().Which
                .LeftHandSide.Should().BeOfType<ParenExpression>().Which
                .Expr.Should().BeOfType<BinaryExpr>().Which
                .LeftHandSide.Should().BeOfType<NumberLiteral>().Subject.Value.Should().Be(1);
            
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
                    ),
                    // Don't assert over parsed Span positions, will be tedious to specify all positions
                    cfg => cfg.Excluding(x => x.Name == "Span")
                );
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
        public void AggregateExpr_NoMod()
        {
            const string input = "sum (blah)";
            
            var result = Parse(Parser.AggregateExpr, input);
            result.Should().BeEquivalentTo(
                new AggregateExpr(
                        "sum",
                        new VectorSelector(new MetricIdentifier("blah")),
                        null,
                        ImmutableArray<string>.Empty,
                        false
                    ),
                    // Don't assert over parsed Span positions, will be tedious to specify all positions
                    cfg => cfg.Excluding(x => x.Name == "Span")
                );

            result.Span.Should().Be(new TextSpan(input));
        }

        [Test]
        public void AggregateExpr_LeadingModBy()
        {
            const string input = "sum by (one, two) (blah)";
            var result = Parse(Parser.AggregateExpr, input);
            
            result.Should().BeEquivalentTo(
                new AggregateExpr(
                    "sum",
                    new VectorSelector(new MetricIdentifier("blah")),
                    null,
                    new string[] {"one", "two"}.ToImmutableArray(),
                    false
                ),
                // Don't assert over parsed Span positions, will be tedious to specify all positions
                cfg => cfg.Excluding(x => x.Name == "Span")
            );
            result.Span.Should().Be(new TextSpan(input));
        }

        [Test]
        public void AggregateExpr_TrailingModWithout()
        {
            const string input = "sum (blah) without (one)";

            var result = Parse(Parser.AggregateExpr, input);
            result.Should().BeEquivalentTo(
                new AggregateExpr(
                    "sum",
                    new VectorSelector(new MetricIdentifier("blah")),
                    null,
                    new string[] {"one"}.ToImmutableArray(),
                    true
                ),
                // Don't assert over parsed Span positions, will be tedious to specify all positions
                cfg => cfg.Excluding(x => x.Name == "Span")
            );
            result.Span.Should().Be(new TextSpan(input));
        }

        [Test]
        public void AggregateExpr_TwoArgs() => Parse(Parser.AggregateExpr, "quantile (0.5, blah)").Should().BeEquivalentTo(
            new AggregateExpr(
                "quantile",
                new VectorSelector(new MetricIdentifier("blah")),
                new NumberLiteral(0.5),
                ImmutableArray<string>.Empty, 
                false
            ),
            // Don't assert over parsed Span positions, will be tedious to specify all positions
            cfg => cfg.Excluding(x => x.Name == "Span")
        );
        
        [Test]
        public void AggregateExpr_LabelNameAggOp()
        {
            Parse(Parser.Expr, @"sum by (quantile) (test)").Should().BeOfType<AggregateExpr>();
        }

        [Test]
        public void Subquery_WithStep()
        {
            const string input = "blah[1h:1m]";
            var result = Parse(Parser.Expr, input);
            
            result.Should().BeEquivalentTo(
                new SubqueryExpr(
                    new VectorSelector(new MetricIdentifier("blah")),
                    new Duration(TimeSpan.FromHours(1)),
                    new Duration(TimeSpan.FromMinutes(1))
                ),
                // Don't assert over parsed Span positions, will be tedious to specify all positions
            cfg => cfg.Excluding(x => x.Name == "Span")
            );
            result.Span.Should().Be(new TextSpan(input));
        }

        [Test]
        public void Subquery_WithoutStep()
        {
            const string input = "blah[1d:]";
            var result = Parse(Parser.Expr, input);
            
            result.Should().BeEquivalentTo(
                new SubqueryExpr(
                    new VectorSelector(new MetricIdentifier("blah")),
                    new Duration(TimeSpan.FromDays(1)),
                    null
                ),
                // Don't assert over parsed Span positions, will be tedious to specify all positions
                cfg => cfg.Excluding(x => x.Name == "Span")
            );
            result.Span.Should().Be(new TextSpan(input));
        }

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
