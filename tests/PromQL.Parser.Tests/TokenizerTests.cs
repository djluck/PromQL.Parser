using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Superpower;

namespace PromQL.Parser.Tests
{
    [TestFixture]
    public class TokenizerTests
    {
        [Test]
        public void Comment()
        {
            var tokens = new Tokenizer().Tokenize("  # this is a comment \n# and another\n,,== 1000");

            tokens.Select(x => x.Kind).Should().Equal(
                PromToken.COMMENT,
                PromToken.COMMENT,
                PromToken.COMMA,
                PromToken.COMMA,
                PromToken.EQLC,
                PromToken.NUMBER
            );
        }
        
        [Test]
        [TestCase("1d")]
        [TestCase("1d1h1m1s")]
        [TestCase("1w")]
        public void Duration(string input)
        {
            var tokens = new Tokenizer().Tokenize(input);

            tokens.Single().Kind.Should().Be(PromToken.DURATION);
        }
        
        [Test]
        public void Number_And_Duration()
        {
            var tokens = new Tokenizer().Tokenize("1 1d");

            tokens.Select(x => x.Kind).Should().Equal(
                PromToken.NUMBER,
                PromToken.DURATION
            );
        }
        
        [Test]
        [TestCase("\"Test!\"")]
        [TestCase("\"Test with \\\"escape\"")]
        [TestCase("'Test'")]
        [TestCase("'Test with \\\'escape'")]
        public void String(string input)
        {
            var tokens = new Tokenizer().Tokenize(input);

            tokens.Select(x => x.Kind).Should().Equal(
                PromToken.STRING
            );
        }
        
        [Test]
        [TestCase("blah123", PromToken.IDENTIFIER)]
        [TestCase("_blah_123", PromToken.IDENTIFIER)]
        [TestCase("blah:blah_123", PromToken.METRIC_IDENTIFIER)]
        [TestCase(":blah:blah_123", PromToken.METRIC_IDENTIFIER)]
        [TestCase("avg", PromToken.AGGREGATE_OP)]
        [TestCase("SUM", PromToken.AGGREGATE_OP)]
        public void Identifier(string input, PromToken expected)
        {
            var tokens = new Tokenizer().Tokenize(input);

            tokens.Select(x => x.Kind).Should().Equal(
                expected
            );
        }
        
        [Test]
        public void Braces()
        {
            var tokens = new Tokenizer().Tokenize("{ first = 'one',second=~'test',third!='',avg!~'' }");

            tokens.Select(x => x.Kind).Should().Equal(
                PromToken.LEFT_BRACE,
                // matcher #1
                PromToken.IDENTIFIER,
                PromToken.EQL,
                PromToken.STRING,
                PromToken.COMMA,
                // matcher #2
                PromToken.IDENTIFIER,
                PromToken.EQL_REGEX,
                PromToken.STRING,
                PromToken.COMMA,
                // matcher #3
                PromToken.IDENTIFIER,
                PromToken.NEQ,
                PromToken.STRING,
                PromToken.COMMA,
                // matcher #4
                PromToken.IDENTIFIER,
                PromToken.NEQ_REGEX,
                PromToken.STRING,
                PromToken.RIGHT_BRACE
            );
        }
        
        [Test]
        public void Brackets()
        {
            var tokens = new Tokenizer().Tokenize("[1h10s]");

            tokens.Select(x => x.Kind).Should().Equal(
                PromToken.LEFT_BRACKET,
                PromToken.DURATION,
                PromToken.RIGHT_BRACKET
            );
        }

        [Test]
        public void Braces_EOF()
        {
            Assert.Throws<ParseException>(() => new Tokenizer().Tokenize("http_request_count{"))
                .Message.Should().Contain("Unexpected end of input inside braces");
        }
        
        [Test]
        public void Braces_ExtraBrace()
        {
            Assert.Throws<ParseException>(() => new Tokenizer().Tokenize("http_request_count{{"))
                .Message.Should().Contain("Unexpected left brace");
        }
        
        [Test]
        public void Paren_Unbalanaced()
        {
            Assert.Throws<ParseException>(() => new Tokenizer().Tokenize("(1)) + 1"))
                .Message.Should().Contain("Unexpected right parenthesis");
        }
        
        [Test]
        public void Paren_Unclosed()
        {
            Assert.Throws<ParseException>(() => new Tokenizer().Tokenize("(((hello)"))
                .Message.Should().Contain("Unclosed left parenthesis");
        }
        
        [Test]
        public void Bracket_TooManyStart()
        {
            Assert.Throws<ParseException>(() => new Tokenizer().Tokenize("blah[[1m]"))
                .Message.Should().Contain("Unexpected left bracket");
        }
        
        [Test]
        public void Bracket_TooManyEnd()
        {
            Assert.Throws<ParseException>(() => new Tokenizer().Tokenize("blah[1m]]"))
                .Message.Should().Contain("Unexpected right bracket");
        }
        
        [Test]
        public void Bracket_Unclosed()
        {
            Assert.Throws<ParseException>(() => new Tokenizer().Tokenize("blah[1m]]"))
                .Message.Should().Contain("Unexpected right bracket");
        }
    }
}
