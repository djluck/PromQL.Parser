using System;
using FluentAssertions;
using NUnit.Framework;
using PromQL.Parser.Ast;
using Superpower;
using Superpower.Parsers;

namespace PromQL.Parser.Tests
{
    [TestFixture]
    public class ExtensibilityTests
    {
        /// <summary>
        /// This is a simple test of the tokenizers and parsers extensibility- can we recognize the $__rate_interval + $__interval
        /// durations that grafana uses?
        /// </summary>
        /// <remarks>
        /// This library was designed to be semi-extensible but it certainly wasn't the primary goal. However, some validation that
        /// we can match a commonly-used extension to PromQL is good to have.
        /// </remarks>
        [Test]
        public void Can_Parse_Grafana_Durations()
        {
            var tokenizer = new Tokenizer();
            // Replace the default duration tokenizer with one to recognize the grafana variables
            tokenizer.Duration = 
                from num in Span.EqualTo("$__rate_interval").Try().Or(Span.EqualTo("$__interval")).Try()
                select num.ToStringValue();

            var original = Parser.Duration;
            // Replace the default Duration parser with one that will return a special duration value (-1) when we encounter a duration token.
            // We have to to do this as the default parser will be expected a prometheus duration and will attempt to match a regex against our newly 
            // accepted and invalid duration.
            Parser.Duration = from t in Token.EqualTo(PromToken.DURATION)
                select new Duration(new TimeSpan(-1));

            // Should work for vector selectors..
            Parser.ParseExpression("my_metric[$__interval]", tokenizer).Should().BeOfType<MatrixSelector>()
                .Which.Duration.Value.Should().Be(new TimeSpan(-1));
            
            // ..and subqueries
            Parser.ParseExpression("my_metric[$__rate_interval:]", tokenizer).Should().BeOfType<SubqueryExpr>()
                .Which.Range.Value.Should().Be(new TimeSpan(-1));;
            
            // And then prove we can modify how expressions are printed
            var printer = new CustomPrinter();
            printer.ToPromQl(Parser.ParseExpression("my_metric[$__interval]", tokenizer))
                .Should().Be("my_metric[$__my_replaced_value]");

            // Restore original parser so rest of tests can pass
            Parser.Duration = original;
        }

        public class CustomPrinter : Printer
        {
            public override void Visit(Duration d)
            {
                if (d.Value == new TimeSpan(-1))
                    Write($"$__my_replaced_value");
                else
                    base.Visit(d);
            }
        }
    }
}