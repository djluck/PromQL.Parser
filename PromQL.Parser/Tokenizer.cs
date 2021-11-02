using System;
using System.Collections.Generic;
using System.Linq;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;

namespace PromQL.Parser
{
    /// <summary>
    /// Converts streams of characters into <see cref="PromToken"/>s. These tokens are then consumed by the <see cref="Parser"/>
    /// to build up the abstract syntax tree.
    /// </summary>
    public class Tokenizer : Tokenizer<PromToken>
    {
        private static Dictionary<string, PromToken> KeywordsToTokens = new (StringComparer.OrdinalIgnoreCase)
        {
            // Operators
            ["and"] =     PromToken.LAND,
            ["or"] =      PromToken.LOR,
            ["unless"] =  PromToken.LUNLESS,
            ["atan2"] =   PromToken.ATAN2,

            // Keywords
            ["offset"] =       PromToken.OFFSET,
            ["by"] =           PromToken.BY,
            ["without"] =      PromToken.WITHOUT,
            ["on"] =           PromToken.ON,
            ["ignoring"] =     PromToken.IGNORING,
            ["group_left"] =   PromToken.GROUP_LEFT,
            ["group_right"] =  PromToken.GROUP_RIGHT,
            ["bool"] =         PromToken.BOOL,
            
            // TODO support inf/ nan
            ["inf"] =          PromToken.NUMBER,
            ["nan"] =          PromToken.NUMBER

            // TODO support preprocessors
            // Preprocessors
            //["start"] =  PromQlToken.START,
            //["end"] =    PromQlToken.END,
        };
        
        public TextParser<string> Comment { get; set; } =
            from start in Character.EqualTo('#')
            from rest in Character.ExceptIn('\r', '\n').Many()
            select new string(rest);

        // TODO add support for hexadecimal
        public TextParser<string> Number { get; set; } =
            from integer in Character.Numeric.Many()
            from dec in Character.EqualTo('.').Optional()
            from fraction in Character.Numeric.Many()
            from exponentPart in (
                from expontentDelim in Character.In('e', 'E')
                from sign in Character.In('+', '-').Optional()
                from exponent in Character.Numeric.AtLeastOnce()
                select exponent
            ).OptionalOrDefault()
            where integer.Length >= 1 || fraction.Length >= 1
            select new string(integer);
        
        public TextParser<string> Duration { get; set; } =
            from d in (
                from num in Character.Numeric.AtLeastOnce()
                from unit in Character.In('s', 'm', 'h', 'd', 'w', 'y').AtLeastOnce()
                select new string(num)
            ).AtLeastOnce()
            select new string(d.SelectMany(x => x).ToArray());
        
        // TODO there's quite a few rules around strings we need to look at 
        public static TextParser<string> QuotedSting(char quoteChar) =>
            from open in Character.EqualTo(quoteChar)
            from content in Character.EqualTo('\\').IgnoreThen(Character.EqualTo(quoteChar)).Try()
                .Or(Character.ExceptIn(quoteChar, '\n'))
                .Many()
            from close in Character.EqualTo(quoteChar)
            select new string(content);

        public TextParser<PromToken> Identifier { get; set; } = Span.MatchedBy(
                Character.Letter.Or(Character.In('_')).IgnoreThen(Character.LetterOrDigit.Or(Character.In('_')).Many())
            )
            .Select(x => PromToken.IDENTIFIER);
        
        public TextParser<PromToken> IndentifierOrKeyword { get; set; } = Span.MatchedBy(
            Character.Letter.Or(Character.In('_', ':')).IgnoreThen(Character.LetterOrDigit.Or(Character.In('_', ':')).Many())
            )
            .Select(x =>
            {
                var idOrKeyword = x.ToStringValue();
                if (Operators.Aggregates.Contains(idOrKeyword))
                    return PromToken.AGGREGATE_OP;
                if (KeywordsToTokens.TryGetValue(idOrKeyword, out var keyToken))
                    return keyToken;
                if (idOrKeyword.Contains(":"))
                    return PromToken.METRIC_IDENTIFIER;
                
                return PromToken.IDENTIFIER;
            });
        
        public TextParser<PromToken> String { get; set; } = QuotedSting('\'').Or(QuotedSting('"')).Select(_ => PromToken.STRING);

        public class Reader
        {
            private Result<char> _start;
            
            public Reader(TextSpan input)
            {
                Input = input;
                SkipWhiteSpace(input);
            }
            
            public TextSpan Input { get; }
            public Result<char> Position { get; private set; }

            public Result<char> Peek() => Position.Remainder.ConsumeChar();

            public bool Next()
            {
                Position = Position.Remainder.ConsumeChar();
                return Position.Remainder.IsAtEnd;
            }
            
            public bool TryParse<TParser>(TextParser<TParser> parser, out Result<TParser> result)
            {
                result = parser(Position.Location);

                if (result.HasValue)
                {
                    Position = result.Remainder.ConsumeChar();
                    _start = Position;
                    return true;
                }

                return false;
            }
            
            public bool TryParseToken<TParser>(TextParser<TParser> parser, PromToken promToken, out Result<PromToken> result)
            {
                result = default;
                
                if (!TryParse(parser, out var pResult))
                    return false;
                
                result = Result.Value(promToken, pResult.Location, pResult.Remainder);
                return true;
            }
            
            public bool TryParseToken(TextParser<PromToken> parser, out Result<PromToken> result)
            {
                result = default;
                
                if (!TryParse(parser, out var pResult))
                    return false;
                
                result = Result.Value(pResult.Value, pResult.Location, pResult.Remainder);
                return true;
            }

            public Result<PromToken> AsToken(PromToken promToken)
            {
                var r = Result.Value(promToken, _start.Location, Position.Remainder);
                Next();
                _start = Position;
                return r;
            }

            public void SkipWhiteSpace()
            {
                SkipWhiteSpace(Position.Location);
            }

            private void SkipWhiteSpace(TextSpan span)
            {
                var result = span.ConsumeChar();
                while (result.HasValue && char.IsWhiteSpace(result.Value))
                    result = result.Remainder.ConsumeChar();
                
                _start = Position = result;
            }

            public Result<PromToken> AsError(string errMsg)
            {
                return Result.Empty<PromToken>(Position.Remainder, errMsg);
            }
        }

        protected override IEnumerable<Result<PromToken>> Tokenize(TextSpan span)
        {
            var reader = new Reader(span);
            if (!reader.Position.HasValue)
                yield break;

            var bracketsOpen = false;

            do
            {
                Result<PromToken> token = default;
                var c = reader.Position.Value;
                
                // TODO brace open + comment are lexed separately from main body in PromQl lexer, is this an issue?
                if (c == '{')
                {
                    yield return reader.AsToken(PromToken.LEFT_BRACE);
                    foreach (var t in TokenizeInsideBraces(reader))
                        yield return t;
                }
                else if (reader.TryParseToken(Comment, PromToken.COMMENT, out token))
                    yield return reader.AsToken(PromToken.COMMENT);
                else if (c == ',')
                    yield return reader.AsToken(PromToken.COMMA);
                else if (c == ',')
                    yield return reader.AsToken(PromToken.COMMA);
                else if (c == '*')
                    yield return reader.AsToken(PromToken.MUL);
                else if (c == '/')
                    yield return reader.AsToken(PromToken.DIV);
                else if (c == '%')
                    yield return reader.AsToken(PromToken.MOD);
                else if (c == '+')
                    yield return reader.AsToken(PromToken.ADD);
                else if (c == '-')
                    yield return reader.AsToken(PromToken.SUB);
                else if (c == '^')
                    yield return reader.AsToken(PromToken.POW);
                else if (c == '=')
                {
                    var n = reader.Peek();
                    if (n.Value == '=')
                    {
                        reader.Next();
                        yield return reader.AsToken(PromToken.EQLC);
                    }
                    // TODO missing err condition
                    else
                        yield return reader.AsToken(PromToken.EQL);
                }
                else if (c == '!')
                {
                    var n = reader.Peek();
                    if (n.Value == '=')
                    {
                        reader.Next();
                        yield return reader.AsToken(PromToken.NEQ);
                    }
                    else
                        yield return reader.AsError("Unexpected character after !");
                }
                else if (c == '<')
                {
                    var n = reader.Peek();
                    if (n.Value == '=')
                    {
                        reader.Next();
                        yield return reader.AsToken(PromToken.LTE);
                    }
                    else
                        yield return reader.AsToken(PromToken.LSS);
                }
                else if (c == '>')
                {
                    var n = reader.Peek();
                    if (n.Value == '=')
                    {
                        reader.Next();
                        yield return reader.AsToken(PromToken.GTE);
                    }
                    else
                        yield return reader.AsToken(PromToken.GTR);
                }
                else if (reader.TryParseToken(Duration, PromToken.DURATION, out token))
                    yield return token;
                else if (reader.TryParseToken(Number, PromToken.NUMBER, out token))
                    yield return token;
                else if (reader.TryParseToken(String, out token))
                    yield return token;
                // TODO Support raw string
                else if (bracketsOpen && c == ':')
                    yield return reader.AsToken(PromToken.COLON);
                else if (reader.TryParse(IndentifierOrKeyword, out token))
                    yield return token;
                // TODO add support for subqueries
                // TODO add support for 'at'
                else if (c == '(')
                    yield return reader.AsToken(PromToken.LEFT_PAREN);
                // TODO track paren depth and support better err messages: https://github.com/prometheus/prometheus/blob/7471208b5c8ff6b65b644adedf7eb964da3d50ae/promql/parser/lex.go#L424
                else if (c == ')')
                    yield return reader.AsToken(PromToken.RIGHT_PAREN);
                else if (c == '[')
                {
                    if (bracketsOpen)
                        yield return reader.AsError("Unexpected left bracket");
                    
                    bracketsOpen = true;
                    yield return reader.AsToken(PromToken.LEFT_BRACKET);
                }
                else if (c == ']')
                {
                    if (!bracketsOpen)
                        yield return reader.AsError("Unexpected right bracket");
                    
                    bracketsOpen = false;
                    yield return reader.AsToken(PromToken.RIGHT_BRACKET);
                }
                else
                    yield return Result.Empty<PromToken>(reader.Position.Remainder);
                
                reader.SkipWhiteSpace();

            } while (reader.Position.HasValue);
        }
        
        /// <summary>
        /// Scans inside of a vector selector. Keywords are ignored and scanned as identifiers.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private IEnumerable<Result<PromToken>> TokenizeInsideBraces(Reader reader)
        {
            Result<PromToken> token;

            while (true)
            {
                reader.SkipWhiteSpace();
                var c = reader.Position.Value;
                
                
                if (c == '{')
                    yield return reader.AsError("Unexpected left brace");
                else if (c == '}')
                {
                    yield return reader.AsToken(PromToken.RIGHT_BRACE);
                    yield break;
                }
                else if (reader.Position.Remainder.IsAtEnd)
                    yield return reader.AsError("Unexpected EOF inside braces");
                else if (reader.TryParseToken(Comment, PromToken.COMMENT, out token))
                    yield return token;
                else if (reader.TryParseToken(Identifier, out token))
                    yield return token;
                else if (reader.TryParseToken(String, out token))
                    yield return token;
                else if (reader.Position.Value == ',')
                    yield return reader.AsToken(PromToken.COMMA);
                // TODO raw string
                else if (c == '=')
                {
                    if (reader.Peek().Value == '~')
                    {
                        reader.Next();
                        yield return reader.AsToken(PromToken.EQL_REGEX);
                    }
                    else
                        yield return reader.AsToken(PromToken.EQL);
                }
                else if (c == '!')
                {
                    reader.Next();
                    yield return reader.Position.Value switch
                    {
                        '=' => reader.AsToken(PromToken.NEQ),
                        '~' => reader.AsToken(PromToken.NEQ_REGEX),
                        _ => reader.AsError($"Unexpected character after ! inside braces: '{reader.Position.Value}'")
                    };
                }
                else
                    yield return reader.AsError($"Unexpected character '{c}' inside braces");
            }
        }
    }
}
