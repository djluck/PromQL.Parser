using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using PromQL.Parser.Ast;
using Superpower;
using Superpower.Display;
using Superpower.Model;
using Superpower.Parsers;

namespace PromQL.Parser
{
    
// Nullability checker goes a bit haywire in the presence of all the Parse.Ref() statements- need to disable some of it's checks
#pragma warning disable CS8603
    
    /// <summary>
    /// Contains parsers for all syntactic components of PromQL expressions.
    /// </summary>
    public static class Parser
    {
        public static TokenListParser<PromToken, Operators.Unary> UnaryOperator =
            Token.EqualTo(PromToken.ADD).Select(_ => Operators.Unary.Add).Or(
                Token.EqualTo(PromToken.SUB).Select(_ => Operators.Unary.Sub)
            );
        
        public static TokenListParser<PromToken, UnaryExpr> UnaryExpr =
            from op in Parse.Ref(() => UnaryOperator)
            from expr in Parse.Ref(() => Expr)
            select new UnaryExpr(op, expr);

        private static readonly HashSet<PromToken> KeywordAndAlphanumericOperatorTokens = typeof(PromToken)
            .GetMembers()
            .Select(enumMember => (enumMember, attr: enumMember.GetCustomAttributes(typeof(TokenAttribute), false).Cast<TokenAttribute>().SingleOrDefault()))
            .Where(x => x.attr != null && (x.attr.Category == "Keyword" || (x.attr.Category == "Operator" && Regex.IsMatch(x.attr.Example, "^[a-zA-Z0-9]+$"))))
            .Select(x => Enum.Parse<PromToken>(x.enumMember.Name))
            .ToHashSet();

        public static TokenListParser<PromToken, MetricIdentifier> MetricIdentifier =
            from id in Token.EqualTo(PromToken.METRIC_IDENTIFIER)
                .Or(Token.EqualTo(PromToken.IDENTIFIER))
                .Or(Token.EqualTo(PromToken.AGGREGATE_OP).Where(t => Operators.Aggregates.Contains(t.ToStringValue()), "aggregate_op"))
                .Or(Token.Matching<PromToken>(t => KeywordAndAlphanumericOperatorTokens.Contains(t), "operator"))
            select new MetricIdentifier(id.ToStringValue());

        public static TokenListParser<PromToken, LabelMatchers> LabelMatchers =
            from lb in Token.EqualTo(PromToken.LEFT_BRACE)
            from matchers in (
                from matcherHead in LabelMatcher
                from matcherTail in (
                    from c in Token.EqualTo(PromToken.COMMA)
                    from m in LabelMatcher
                    select m
                ).Try().Many()
                from comma in Token.EqualTo(PromToken.COMMA).Optional()
                select new [] { matcherHead }.Concat(matcherTail)
            ).OptionalOrDefault(Array.Empty<LabelMatcher>())
            from rb in Token.EqualTo(PromToken.RIGHT_BRACE)
            select new LabelMatchers(matchers.ToImmutableArray());
        
        public static TokenListParser<PromToken, VectorSelector> VectorSelector =
        (
            from m in MetricIdentifier
            from lm in LabelMatchers.AsNullable().OptionalOrDefault()
            select new VectorSelector(m, lm)
        ).Or(
            from lm in LabelMatchers
            select new VectorSelector(lm)
        );

        public static TokenListParser<PromToken, MatrixSelector> MatrixSelector =
            from vs in VectorSelector
            from d in Parse.Ref(() => Duration).Between(Token.EqualTo(PromToken.LEFT_BRACKET), Token.EqualTo(PromToken.RIGHT_BRACKET))
            select new MatrixSelector(vs, d);

        // TODO see https://github.com/prometheus/prometheus/blob/7471208b5c8ff6b65b644adedf7eb964da3d50ae/promql/parser/generated_parser.y#L679
        public static TokenListParser<PromToken, string> LabelValueMatcher =
            from id in Token.EqualTo(PromToken.IDENTIFIER)
                .Or(Token.EqualTo(PromToken.AGGREGATE_OP).Where(x => Operators.Aggregates.Contains(x.ToStringValue())))
                // Inside of grouping options label names can be recognized as keywords by the lexer. This is a list of keywords that could also be a label name.
                // See https://github.com/prometheus/prometheus/blob/7471208b5c8ff6b65b644adedf7eb964da3d50ae/promql/parser/generated_parser.y#L678 for more info.
                .Or(Token.Matching<PromToken>(t => KeywordAndAlphanumericOperatorTokens.Contains(t), "operator"))
            .Or(Token.EqualTo(PromToken.OFFSET))
            select id.ToStringValue();
        
        public static TokenListParser<PromToken, LabelMatcher> LabelMatcher =
            from id in LabelValueMatcher
            from op in MatchOp
            from str in StringLiteral
            select new LabelMatcher(id, op, (StringLiteral)str);
        
        public static TokenListParser<PromToken, Operators.LabelMatch> MatchOp =
            Token.EqualTo(PromToken.EQL).Select(_ => Operators.LabelMatch.Equal)
                .Or(
                    Token.EqualTo(PromToken.NEQ).Select(_ => Operators.LabelMatch.NotEqual)
                ).Or(
                    Token.EqualTo(PromToken.EQL_REGEX).Select(_ => Operators.LabelMatch.Regexp)
                ).Or(
                    Token.EqualTo(PromToken.NEQ_REGEX).Select(_ => Operators.LabelMatch.NotRegexp)
                );

        public static TokenListParser<PromToken, NumberLiteral> Number =
            from s in (
                Token.EqualTo(PromToken.ADD).Or(Token.EqualTo(PromToken.SUB))
            ).OptionalOrDefault(new Token<PromToken>(PromToken.ADD, TextSpan.Empty))
            from n in Token.EqualTo(PromToken.NUMBER)
            select new NumberLiteral(
                (n.ToStringValue(), s.Kind) switch
                {
                    (var v, PromToken.ADD) when v.Equals("Inf", StringComparison.OrdinalIgnoreCase) => double.PositiveInfinity,
                    (var v, PromToken.SUB) when v.Equals("Inf", StringComparison.OrdinalIgnoreCase) => double.NegativeInfinity,
                    (var v, var op) => double.Parse(v) * (op == PromToken.SUB ? -1.0 : 1.0)
                }
            );

        /// <summary>
        /// Taken from https://github.com/prometheus/common/blob/88f1636b699ae4fb949d292ffb904c205bf542c9/model/time.go#L186
        /// </summary>
        /// <returns></returns>
        public static Regex DurationRegex =
            new Regex("^(([0-9]+)y)?(([0-9]+)w)?(([0-9]+)d)?(([0-9]+)h)?(([0-9]+)m)?(([0-9]+)s)?(([0-9]+)ms)?$",
                RegexOptions.Compiled);

        public static TokenListParser<PromToken, Duration> Duration =
            Token.EqualTo(PromToken.DURATION)
                .Select(n =>
                {
                    static TimeSpan ParseComponent(Match m, int index, Func<int, TimeSpan> parser)
                    {
                        if (m.Groups[index].Success)
                            return parser(int.Parse(m.Groups[index].Value));

                        return TimeSpan.Zero;
                    }

                    var match = DurationRegex.Match(n.ToStringValue());
                    if (!match.Success)
                        throw new ParseException($"Invalid duration: {n.ToStringValue()}", n.Position);

                    var ts = TimeSpan.Zero;
                    ts += ParseComponent(match, 2, i => TimeSpan.FromDays(i) * 365);
                    ts += ParseComponent(match, 4, i => TimeSpan.FromDays(i) * 7);
                    ts += ParseComponent(match, 6, i => TimeSpan.FromDays(i));
                    ts += ParseComponent(match, 8, i => TimeSpan.FromHours(i));
                    ts += ParseComponent(match, 10, i => TimeSpan.FromMinutes(i));
                    ts += ParseComponent(match, 12, i => TimeSpan.FromSeconds(i));
                    ts += ParseComponent(match, 14, i => TimeSpan.FromMilliseconds(i));

                    return new Duration(ts);
                });

        // TODO support unicode, octal and hex escapes
        public static TextParser<string> StringText(char quoteChar) =>
            from open in Character.EqualTo(quoteChar)
            from content in (
                from escape in Character.EqualTo('\\')
                // Taken from https://github.com/prometheus/prometheus/blob/7471208b5c8ff6b65b644adedf7eb964da3d50ae/promql/parser/lex.go#L554
                from value in Character.In(quoteChar, 'a', 'b', 'f', 'n', 'r', 't', 'v', '\\')
                    .Message("Unexpected escape sequence")
                select (char)(
                    value switch
                    {
                        'a' => '\a',
                        'b' => '\b',
                        'f' => '\f',
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        'v' => '\v',
                        _ => value
                    }
                )
            ).Or(Character.ExceptIn(quoteChar, '\n'))
            .Many()
            from close in Character.EqualTo(quoteChar)
            select new string(content);

        public static TextParser<string> SingleQuoteStringLiteral = StringText('\'');
        public static TextParser<string> DoubleQuoteStringLiteral = StringText('"');
        
         public static TextParser<string> RawString =>
            from open in Character.EqualTo('`')
            from content in Character.Except('`').Many()
            from close in Character.EqualTo('`')
            select new string(content);

        public static TokenListParser<PromToken, StringLiteral> StringLiteral =
            Token.EqualTo(PromToken.STRING)
                .Select(t =>
                {
                    var c = t.Span.ConsumeChar();
                    if (c.Value == '\'')
                        return new StringLiteral('\'', SingleQuoteStringLiteral.Parse(t.Span.ToStringValue()));
                    if (c.Value == '"')
                        return new StringLiteral('"', DoubleQuoteStringLiteral.Parse(t.Span.ToStringValue()));
                    if (c.Value == '`')
                        return new StringLiteral('`', RawString.Parse(t.Span.ToStringValue()));
                    
                    throw new ParseException($"Unexpected string quote", t.Span.Position);
                });


        public static Func<Expr, TokenListParser<PromToken, OffsetExpr>> OffsetExpr = (Expr expr) =>
            from offset in Token.EqualTo(PromToken.OFFSET)
            from neg in Token.EqualTo(PromToken.SUB).Optional()
            from duration in Duration
            select new OffsetExpr(expr, new Duration(new TimeSpan(duration.Value.Ticks * (neg.HasValue ? -1 : 1))));

        public static TokenListParser<PromToken, ParenExpression> ParenExpression =
            from e in Parse.Ref(() => Expr).Between(Token.EqualTo(PromToken.LEFT_PAREN), Token.EqualTo(PromToken.RIGHT_PAREN))
            select new ParenExpression(e);

        public static TokenListParser<PromToken, Expr[]> FunctionArgs = Parse.Ref(() => Expr).ManyDelimitedBy(Token.EqualTo(PromToken.COMMA))
            .Between(Token.EqualTo(PromToken.LEFT_PAREN), Token.EqualTo(PromToken.RIGHT_PAREN));
        
        public static TokenListParser<PromToken, FunctionCall> FunctionCall =
            from id in Token.EqualTo(PromToken.IDENTIFIER).Where(x => Functions.Names.Contains(x.ToStringValue()))
            from args in FunctionArgs
            select new FunctionCall(id.ToStringValue(), args.ToImmutableArray());


        public static TokenListParser<PromToken, ImmutableArray<string>> GroupingLabels =
            from labels in (LabelValueMatcher.ManyDelimitedBy(Token.EqualTo(PromToken.COMMA)))
                .Between(Token.EqualTo(PromToken.LEFT_PAREN), Token.EqualTo(PromToken.RIGHT_PAREN))
            select labels.Select(x => x).ToImmutableArray();

        public static TokenListParser<PromToken, bool> BoolModifier =
            from b in Token.EqualTo(PromToken.BOOL).Optional()
            select b.HasValue;
        
        public static TokenListParser<PromToken, VectorMatching> OnOrIgnoring =
            from b in BoolModifier
            from onOrIgnoring in Token.EqualTo(PromToken.ON).Or(Token.EqualTo(PromToken.IGNORING))
            from onOrIgnoringLabels in GroupingLabels
            select new VectorMatching(
                Operators.VectorMatchCardinality.OneToOne,
                onOrIgnoringLabels,
                onOrIgnoring.HasValue && onOrIgnoring.Kind == PromToken.ON,
                ImmutableArray<string>.Empty,
                b
            );

        public static Func<Expr, TokenListParser<PromToken, SubqueryExpr>> SubqueryExpr = (Expr expr) =>
            from lb in Token.EqualTo(PromToken.LEFT_BRACKET)
            from range in Duration
            from colon in Token.EqualTo(PromToken.COLON)
            from step in Duration.AsNullable().OptionalOrDefault()
            from rb in Token.EqualTo(PromToken.RIGHT_BRACKET)
            select new SubqueryExpr(expr, range, step);

        public static TokenListParser<PromToken, VectorMatching> VectorMatching =
            from vectMatching in (
                from vm in OnOrIgnoring
                from grp in Token.EqualTo(PromToken.GROUP_LEFT).Or(Token.EqualTo(PromToken.GROUP_RIGHT))
                from grpLabels in GroupingLabels.OptionalOrDefault(ImmutableArray<string>.Empty)
                select vm with
                {
                    MatchCardinality = grp switch
                    {
                        {HasValue : false} => Operators.VectorMatchCardinality.OneToOne,
                        {Kind: PromToken.GROUP_LEFT} => Operators.VectorMatchCardinality.ManyToOne,
                        {Kind: PromToken.GROUP_RIGHT} => Operators.VectorMatchCardinality.OneToMany,
                        _ => Operators.VectorMatchCardinality.OneToOne
                    },
                    Include = grpLabels
                }
            ).Try().Or(
                from vm in OnOrIgnoring
                select vm
            ).Try().Or(
                from b in BoolModifier
                select new VectorMatching(b)
            )
            select vectMatching;

        private static IReadOnlyDictionary<PromToken, Operators.Binary> BinaryOperatorMap = new Dictionary<PromToken, Operators.Binary>()
        {
            [PromToken.ADD] = Operators.Binary.Add,
            [PromToken.LAND] = Operators.Binary.And,
            [PromToken.ATAN2] = Operators.Binary.Atan2,
            [PromToken.DIV] = Operators.Binary.Div,
            [PromToken.EQLC] = Operators.Binary.Eql,
            [PromToken.GTE] = Operators.Binary.Gte,
            [PromToken.GTR] = Operators.Binary.Gtr,
            [PromToken.LSS] = Operators.Binary.Lss,
            [PromToken.LTE] = Operators.Binary.Lte,
            [PromToken.MOD] = Operators.Binary.Mod,
            [PromToken.MUL] = Operators.Binary.Mul,
            [PromToken.NEQ] = Operators.Binary.Neq,
            [PromToken.LOR] = Operators.Binary.Or,
            [PromToken.POW] = Operators.Binary.Pow,
            [PromToken.SUB] = Operators.Binary.Sub,
            [PromToken.LUNLESS] = Operators.Binary.Unless
        };
        
        public static TokenListParser<PromToken, BinaryExpr> BinaryExpr =
            from lhs in Parse.Ref(() => ExprNotBinary)
            from op in Token.Matching<PromToken>(x => BinaryOperatorMap.ContainsKey(x), "binary_op")
            from vm in VectorMatching.AsNullable().OptionalOrDefault()
            from rhs in Parse.Ref(() => Expr)
            select new BinaryExpr(lhs, rhs, BinaryOperatorMap[op.Kind], vm);

        public static TokenListParser<PromToken, (bool without, ImmutableArray<string> labels)> AggregateModifier =
            from kind in Token.EqualTo(PromToken.BY)
                .Or(Token.EqualTo(PromToken.WITHOUT))
            from labels in GroupingLabels
            select (kind.Kind == PromToken.WITHOUT, labels);

        public static TokenListParser<PromToken, AggregateExpr> AggregateExpr =
            from op in Token.EqualTo(PromToken.AGGREGATE_OP).Where(x => Operators.Aggregates.Contains(x.Span.ToStringValue()))
            from argsAndMod in (
                from args in FunctionArgs
                from mod in AggregateModifier.OptionalOrDefault((without: false, labels: ImmutableArray<string>.Empty))
                select (mod, args)
            ).Or(
                from mod in AggregateModifier
                from args in FunctionArgs
                select (mod, args)
            )
            .Where(x => x.args.Length >= 1, "At least one argument is required for aggregate expressions")
            .Where(x => x.args.Length <= 2, "A maximum of two arguments is supported for aggregate expressions")
            select new AggregateExpr(op.ToStringValue(), argsAndMod.args.Length > 1 ? argsAndMod.args[1] : argsAndMod.args[0], argsAndMod.args.Length > 1 ? argsAndMod.args[0] : null, argsAndMod.mod.labels, argsAndMod.mod.without );

        public static TokenListParser<PromToken, Expr> ExprNotBinary =
             from head in OneOf(
                 // TODO can we optimize order here?
                 Parse.Ref(() => ParenExpression).Cast<PromToken, ParenExpression, Expr>(),
                 Parse.Ref(() => AggregateExpr).Cast<PromToken, AggregateExpr, Expr>().Try(),
                 Parse.Ref(() => FunctionCall).Cast<PromToken, FunctionCall, Expr>().Try(),
                 Number.Cast<PromToken, NumberLiteral, Expr>().Try(),
                 Parse.Ref(() => UnaryExpr).Cast<PromToken, UnaryExpr, Expr>(),
                 MatrixSelector.Cast<PromToken, MatrixSelector, Expr>().Try(),
                 VectorSelector.Cast<PromToken, VectorSelector, Expr>(),
                 StringLiteral.Cast<PromToken, StringLiteral, Expr>()
             )
#pragma warning disable CS8602
             from offsetOrSubquery in Parse.Ref(() => OffsetOrSubquery(head)).AsNullable().OptionalOrDefault()
#pragma warning restore CS8602
             select offsetOrSubquery == null ? head : offsetOrSubquery;

        public static Func<Expr, TokenListParser<PromToken, Expr>> OffsetOrSubquery = (Expr expr) =>
             from offsetOfSubquery in (
                 from offset in OffsetExpr(expr)
                 select (Expr)offset
             ).Or(
                 from subquery in SubqueryExpr(expr)
                 select (Expr)subquery
             )
             select offsetOfSubquery;
        
        public static TokenListParser<PromToken, Expr> Expr { get; } =
             from head in Parse.Ref(() => BinaryExpr).Cast<PromToken, BinaryExpr, Expr>().Try().Or(ExprNotBinary)
             from offsetOrSubquery in OffsetOrSubquery(head).AsNullable().OptionalOrDefault()
             select offsetOrSubquery == null ? head : offsetOrSubquery;

        /// <summary>
        /// Parse the specified input as a PromQL expression.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="tokenizer">Pass a customized tokenizer. By default, will create a new instance of <see cref="Tokenizer"/>.</param>
        /// <returns></returns>
        public static Expr ParseExpression(string input, Tokenizer? tokenizer = null)
        {
            tokenizer ??= new Tokenizer();
            return Expr.AtEnd().Parse(new TokenList<PromToken>(
                tokenizer.Tokenize(input).Where(x => x.Kind != PromToken.COMMENT).ToArray()
            ));
        }
        
        private static TokenListParser<PromToken, T> OneOf<T>(params TokenListParser<PromToken, T>[] parsers)
        {
            TokenListParser<PromToken, T> expr = parsers[0].Try();

            foreach (var p in parsers.Skip(1))
            {
                expr = expr.Or(p);
            }

            return expr;
        }
    }
}
