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
        public static TokenListParser<PromToken, ParsedValue<Operators.Unary>> UnaryOperator =
            Token.EqualTo(PromToken.ADD).Select(t => Operators.Unary.Add.ToParsedValue(t.Span)).Or(
                Token.EqualTo(PromToken.SUB).Select(t => Operators.Unary.Sub.ToParsedValue(t.Span))
            );
        
        public static TokenListParser<PromToken, UnaryExpr> UnaryExpr =
            from op in Parse.Ref(() => UnaryOperator)
            from expr in Parse.Ref(() => Expr)
            select new UnaryExpr(op.Value, expr, op.Span.UntilEnd(expr.Span));
        
        private static IEnumerable<PromToken> FindTokensMatching(Func<TokenAttribute, bool> predicate) => typeof(PromToken).GetMembers()
            .Select(enumMember => (enumMember, attr: enumMember.GetCustomAttributes(typeof(TokenAttribute), false).Cast<TokenAttribute>().SingleOrDefault()))
            .Where(x => x.attr != null && predicate(x.attr))
            .Select(x => Enum.Parse<PromToken>(x.enumMember.Name))
            .ToHashSet();

        private static readonly HashSet<PromToken> AlphanumericOperatorTokens = FindTokensMatching(attr => attr.Category == "Operator" && Regex.IsMatch(attr.Example, "^[a-zA-Z0-9]+$"))
            .ToHashSet();

        private static readonly HashSet<PromToken> KeywordAndAlphanumericOperatorTokens = FindTokensMatching(attr => attr.Category == "Keyword")
            .Concat(AlphanumericOperatorTokens)
            .ToHashSet();

        public static TokenListParser<PromToken, MetricIdentifier> MetricIdentifier =
            from id in Token.EqualTo(PromToken.METRIC_IDENTIFIER)
                .Or(Token.EqualTo(PromToken.IDENTIFIER))
                .Or(Token.EqualTo(PromToken.AGGREGATE_OP).Where(t => Operators.Aggregates.ContainsKey(t.ToStringValue()), "aggregate_op"))
                .Or(Token.Matching<PromToken>(t => AlphanumericOperatorTokens.Contains(t), "operator"))
            select new MetricIdentifier(id.ToStringValue(), id.Span);

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
            select new LabelMatchers(matchers.ToImmutableArray(), lb.Span.UntilEnd(rb.Span));
        
        public static TokenListParser<PromToken, VectorSelector> VectorSelector =
        (
            from m in MetricIdentifier
            from lm in LabelMatchers.AsNullable().OptionalOrDefault()
            select new VectorSelector(m, lm, lm != null ? m.Span!.Value.UntilEnd(lm.Span) : m.Span!)
        ).Or(
            from lm in LabelMatchers
            select new VectorSelector(lm, lm.Span)
        );

        public static TokenListParser<PromToken, MatrixSelector> MatrixSelector =
            from vs in VectorSelector
            from lb in Token.EqualTo(PromToken.LEFT_BRACKET)
            from d in Parse.Ref(() => Duration)
            from rb in Token.EqualTo(PromToken.RIGHT_BRACKET)
            select new MatrixSelector(vs, d, vs.Span!.Value.UntilEnd(rb.Span));

        // TODO see https://github.com/prometheus/prometheus/blob/7471208b5c8ff6b65b644adedf7eb964da3d50ae/promql/parser/generated_parser.y#L679
        public static TokenListParser<PromToken, ParsedValue<string>> LabelValueMatcher =
            from id in Token.EqualTo(PromToken.IDENTIFIER)
                .Or(Token.EqualTo(PromToken.AGGREGATE_OP).Where(x => Operators.Aggregates.ContainsKey(x.ToStringValue())))
                // Inside of grouping options label names can be recognized as keywords by the lexer. This is a list of keywords that could also be a label name.
                // See https://github.com/prometheus/prometheus/blob/7471208b5c8ff6b65b644adedf7eb964da3d50ae/promql/parser/generated_parser.y#L678 for more info.
                .Or(Token.Matching<PromToken>(t => KeywordAndAlphanumericOperatorTokens.Contains(t), "keyword_or_operator"))
            .Or(Token.EqualTo(PromToken.OFFSET))
            select new ParsedValue<string>(id.ToStringValue(), id.Span);
        
        public static TokenListParser<PromToken, LabelMatcher> LabelMatcher =
            from id in LabelValueMatcher
            from op in MatchOp
            from str in StringLiteral
            select new LabelMatcher(id.Value, op, (StringLiteral)str, id.Span.UntilEnd(str.Span));
        
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
            ).OptionalOrDefault(new Token<PromToken>(PromToken.ADD, TextSpan.None))
            from n in Token.EqualTo(PromToken.NUMBER)
            select new NumberLiteral(
                (n.ToStringValue(), s.Kind) switch
                {
                    (var v, PromToken.ADD) when v.Equals("Inf", StringComparison.OrdinalIgnoreCase) => double.PositiveInfinity,
                    (var v, PromToken.SUB) when v.Equals("Inf", StringComparison.OrdinalIgnoreCase) => double.NegativeInfinity,
                    (var v, var op) => double.Parse(v) * (op == PromToken.SUB ? -1.0 : 1.0)
                },
                s.Span.Length > 0 ? s.Span.UntilEnd(n.Span) : n.Span
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

                    return new Duration(ts, n.Span);
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
                        return new StringLiteral('\'', SingleQuoteStringLiteral.Parse(t.Span.ToStringValue()), t.Span);
                    if (c.Value == '"')
                        return new StringLiteral('"', DoubleQuoteStringLiteral.Parse(t.Span.ToStringValue()), t.Span);
                    if (c.Value == '`')
                        return new StringLiteral('`', RawString.Parse(t.Span.ToStringValue()), t.Span);
                    
                    throw new ParseException($"Unexpected string quote", t.Span.Position);
                });

        private static readonly HashSet<Type> ValidOffsetExpressions = new HashSet<Type>
        {
            typeof(MatrixSelector),
            typeof(VectorSelector),
            typeof(SubqueryExpr),
        };

        public static Func<Expr, TokenListParser<PromToken, OffsetExpr>> OffsetExpr = (Expr expr) =>
        (
            from offset in Token.EqualTo(PromToken.OFFSET)
            from neg in Token.EqualTo(PromToken.SUB).Optional()
            from duration in Duration
                // Where needs to be called once the parser has definitely been advanced beyond the initial token (offset)
                // it parses in order for Or() to consider this a partial failure
                .Where(_ => 
                        ValidOffsetExpressions.Contains(expr.GetType()), 
                    "offset modifier must be preceded by an instant vector selector or range vector selector or a subquery"
                )
            select new OffsetExpr(
                expr,
                new Duration(new TimeSpan(duration.Value.Ticks * (neg.HasValue ? -1 : 1))),
                expr.Span!.Value.UntilEnd(duration.Span)
            )
        );

        public static TokenListParser<PromToken, ParenExpression> ParenExpression =
            from lp in Token.EqualTo(PromToken.LEFT_PAREN)
            from e in Parse.Ref(() => Expr)
            from rp in Token.EqualTo(PromToken.RIGHT_PAREN)
            select new ParenExpression(e, lp.Span.UntilEnd(rp.Span));

        public static TokenListParser<PromToken, ParsedValue<Expr[]>> FunctionArgs =
            from lp in Token.EqualTo(PromToken.LEFT_PAREN).Try()
            from args in Parse.Ref(() => Expr).ManyDelimitedBy(Token.EqualTo(PromToken.COMMA))
            from rp in Token.EqualTo(PromToken.RIGHT_PAREN)
            select args.ToParsedValue(lp.Span, rp.Span);
        
        public static TokenListParser<PromToken, FunctionCall> FunctionCall =
            from id in Token.EqualTo(PromToken.IDENTIFIER).Where(x => Functions.Map.ContainsKey(x.ToStringValue())).Try()
            let function = Functions.Map[id.ToStringValue()] 
            from args in FunctionArgs
                .Where(a => function.IsVariadic || (!function.IsVariadic && function.ArgTypes.Length == a.Value.Length), $"Incorrect number of argument(s) in call to {function.Name}, expected {function.ArgTypes.Length} argument(s)")
                .Where(a => !function.IsVariadic || (function.IsVariadic && a.Value.Length >= function.MinArgCount), $"Incorrect number of argument(s) in call to {function.Name}, expected at least {function.MinArgCount} argument(s)")
                // TODO validate "at most" arguments- https://github.com/prometheus/prometheus/blob/7471208b5c8ff6b65b644adedf7eb964da3d50ae/promql/parser/parse.go#L552
            select new FunctionCall(function, args.Value.ToImmutableArray(), id.Span.UntilEnd(args.Span));


        public static TokenListParser<PromToken, ParsedValue<ImmutableArray<string>>> GroupingLabels =
            from lParen in Token.EqualTo(PromToken.LEFT_PAREN)
            from labels in (LabelValueMatcher.ManyDelimitedBy(Token.EqualTo(PromToken.COMMA)))
            from rParen in Token.EqualTo(PromToken.RIGHT_PAREN)
            select labels.Select(x => x.Value).ToImmutableArray().ToParsedValue(lParen.Span, rParen.Span);

        public static TokenListParser<PromToken, ParsedValue<bool>> BoolModifier =
            from b in Token.EqualTo(PromToken.BOOL).Optional()
            select b.HasValue.ToParsedValue(b?.Span ?? TextSpan.None);
        
        public static TokenListParser<PromToken, VectorMatching> OnOrIgnoring =
            from b in BoolModifier
            from onOrIgnoring in Token.EqualTo(PromToken.ON).Or(Token.EqualTo(PromToken.IGNORING))
            from onOrIgnoringLabels in GroupingLabels
            select new VectorMatching(
                Operators.VectorMatchCardinality.OneToOne,
                onOrIgnoringLabels.Value,
                onOrIgnoring.HasValue && onOrIgnoring.Kind == PromToken.ON,
                ImmutableArray<string>.Empty,
                b.Value,
                b.HasSpan ? b.Span.UntilEnd(onOrIgnoringLabels.Span) : onOrIgnoring.Span.UntilEnd(onOrIgnoringLabels.Span)
            );

        public static Func<Expr, TokenListParser<PromToken, SubqueryExpr>> SubqueryExpr = (Expr expr) =>
            from lb in Token.EqualTo(PromToken.LEFT_BRACKET)
            from range in Duration
            from colon in Token.EqualTo(PromToken.COLON)
            from step in Duration.AsNullable().OptionalOrDefault()
            from rb in Token.EqualTo(PromToken.RIGHT_BRACKET)
            select new SubqueryExpr(expr, range, step, expr.Span!.Value.UntilEnd(rb.Span));

        public static TokenListParser<PromToken, VectorMatching> VectorMatching =
            from vectMatching in (
                from vm in OnOrIgnoring
                from grp in Token.EqualTo(PromToken.GROUP_LEFT).Or(Token.EqualTo(PromToken.GROUP_RIGHT))
                from grpLabels in GroupingLabels.OptionalOrDefault(ImmutableArray<string>.Empty.ToEmptyParsedValue())
                select vm with
                {
                    MatchCardinality = grp switch
                    {
                        {HasValue : false} => Operators.VectorMatchCardinality.OneToOne,
                        {Kind: PromToken.GROUP_LEFT} => Operators.VectorMatchCardinality.ManyToOne,
                        {Kind: PromToken.GROUP_RIGHT} => Operators.VectorMatchCardinality.OneToMany,
                        _ => Operators.VectorMatchCardinality.OneToOne
                    },
                    Include = grpLabels.Value,
                    Span = vm.Span!.Value.UntilEnd(grpLabels.HasSpan ? grpLabels.Span : grp.Span)
                }
            ).Try().Or(
                from vm in OnOrIgnoring
                select vm
            ).Try().Or(
                from b in BoolModifier
                select new VectorMatching(b.Value) { Span = b.Span }
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
            // Sprache doesn't support lef recursive grammars so we have to parse out binary expressions as lists of non-binary expressions 
            from head in Parse.Ref(() => ExprNotBinary)
            from tail in (
                from opToken in Token.Matching<PromToken>(x => BinaryOperatorMap.ContainsKey(x), "binary_op")
                let op = BinaryOperatorMap[opToken.Kind]
                from vm in VectorMatching.AsNullable().OptionalOrDefault()
                    .Where(x => x is not { ReturnBool: true } || (x.ReturnBool && Operators.BinaryComparisonOperators.Contains(op)), "bool modifier can only be used on comparison operators")
                from expr in Parse.Ref(() => ExprNotBinary)
                select (op, vm, expr)
            ).AtLeastOnce()
            select CreateBinaryExpression(head, tail);

        /// <summary>
        /// Creates a binary expression from a collection of two or more operands and one or more operators.
        /// </summary>
        /// <remarks>
        /// This function need to ensure operator precedence is maintained, e.g. 1 + 2 * 3 or 4 should parsed as (1 + (2 * 3)) or 4.
        /// </remarks>
        /// <param name="head">The first operand</param>
        /// <param name="tail">The trailing operators, vector matching + operands</param>
        private static BinaryExpr CreateBinaryExpression(Expr head, (Operators.Binary op, VectorMatching? vm, Expr expr)[] tail)
        {
            // Just two operands, no need to do any precedence checking
            if (tail.Length == 1)
                return new BinaryExpr(head, tail[0].expr, tail[0].op, tail[0].vm, head.Span!.Value.UntilEnd(tail[0].expr.Span));

            // Three + operands and we need to group subexpressions by precedence. First things first: create linked lists of all our operands and operators
            var operands = new LinkedList<Expr>(new[] { head }.Concat(tail.Select(x => x.expr)));
            var operators = new LinkedList<(Operators.Binary op, VectorMatching? vm)>(tail.Select(x => (x.op, x.vm)));

            // Iterate through each level of operator precedence, moving from highest -> lowest
            foreach (var precedenceLevel in Operators.BinaryPrecedence)
            {
                var lhs = operands.First;
                var op = operators.First;
                
                // While we have operators left to consume, iterate through each operand + operator
                while (op != null)
                {
                    var rhs = lhs!.Next!;
                    
                    // This operator has the same precedence of the current precedence level- create a new binary subexpression with the current operands + operators
                    if (precedenceLevel.Contains(op.Value.op))
                    {
                        var b = new BinaryExpr(lhs.Value, rhs.Value, op.Value.op, op.Value.vm, lhs.Value.Span!.Value.UntilEnd(rhs.Value.Span)); // TODO span matching
                        var bNode = operands.AddBefore(rhs, b); 
                        
                        // Remove the previous operands (will replace with our new binary expression)
                        operands.Remove(lhs);
                        operands.Remove(rhs);
                        
                        lhs = bNode;
                        var nextOp = op.Next;
                        
                        // Remove the operator
                        operators.Remove(op);
                        op = nextOp;
                    }
                    else
                    {
                        // Move on to the next operand + operator
                        lhs = rhs;
                        op = op.Next;
                    }
                }
            }

            return (BinaryExpr)operands.Single();
        }

        public static TokenListParser<PromToken, ParsedValue<(bool without, ImmutableArray<string> labels)>> AggregateModifier =
            from kind in Token.EqualTo(PromToken.BY).Try()
                .Or(Token.EqualTo(PromToken.WITHOUT).Try())
            from labels in GroupingLabels
            select (kind.Kind == PromToken.WITHOUT, labels.Value).ToParsedValue(kind.Span, labels.Span);

        public static TokenListParser<PromToken, AggregateExpr> AggregateExpr =
            from op in Token.EqualTo(PromToken.AGGREGATE_OP)
                .Where(x => Operators.Aggregates.ContainsKey(x.ToStringValue())).Try()
            let aggOps = Operators.Aggregates[op.ToStringValue()]
            from argsAndMod in (
                from args in FunctionArgs
                from mod in AggregateModifier.OptionalOrDefault(
                    (without: false, labels: ImmutableArray<string>.Empty).ToEmptyParsedValue()
                )
                select (mod, args: args.Value).ToParsedValue(args.Span, mod.HasSpan ? mod.Span : args.Span)
            ).Or(
                from mod in AggregateModifier
                from args in FunctionArgs
                select (mod, args: args.Value).ToParsedValue(mod.Span, args.Span)
            )
            .Where(x => aggOps.ParameterType == null || (aggOps.ParameterType != null && x.Value.args.Length == 2), "wrong number of arguments for aggregate expression provided, expected 2, got 1")
            .Where(x => aggOps.ParameterType != null || (aggOps.ParameterType == null && x.Value.args.Length == 1), "wrong number of arguments for aggregate expression provided, expected 1, got 2")
            select new AggregateExpr(
                aggOps, 
                argsAndMod.Value.args.Length > 1 ? argsAndMod.Value.args[1] : argsAndMod.Value.args[0], 
                argsAndMod.Value.args.Length > 1 ? argsAndMod.Value.args[0] : null, 
                argsAndMod.Value.mod.Value.labels, 
                argsAndMod.Value.mod.Value.without,
                Span: op.Span.UntilEnd(argsAndMod.Span)
            );

        public static TokenListParser<PromToken, Expr> ExprNotBinary =
             from head in OneOf(
                 // TODO can we optimize order here?
                 Parse.Ref(() => ParenExpression).Cast<PromToken, ParenExpression, Expr>(),
                 Parse.Ref(() => AggregateExpr).Cast<PromToken, AggregateExpr, Expr>(),
                 Parse.Ref(() => FunctionCall).Cast<PromToken, FunctionCall, Expr>(),
                 Parse.Ref(() => Number).Cast<PromToken, NumberLiteral, Expr>().Try(),
                 Parse.Ref(() => UnaryExpr).Cast<PromToken, UnaryExpr, Expr>(),
                 Parse.Ref(() => MatrixSelector).Cast<PromToken, MatrixSelector, Expr>().Try(),
                 Parse.Ref(() => VectorSelector).Cast<PromToken, VectorSelector, Expr>(),
                 Parse.Ref(() => StringLiteral).Cast<PromToken, StringLiteral, Expr>()
             )
#pragma warning disable CS8602
             from offsetOrSubquery in Parse.Ref(() => OffsetOrSubquery(head))
#pragma warning restore CS8602
             select offsetOrSubquery == null ? head : offsetOrSubquery;

        public static Func<Expr, TokenListParser<PromToken, Expr?>> OffsetOrSubquery = (Expr expr) =>
            (
                from offset in OffsetExpr(expr)
                select (Expr) offset
            ).Or(
                from subquery in SubqueryExpr(expr)
                select (Expr) subquery
            )
            .AsNullable()
            .Or(
                Parse.Return<PromToken, Expr?>(null)
            );
        
        public static TokenListParser<PromToken, Expr> Expr { get; } =
             from head in Parse.Ref(() => BinaryExpr).Cast<PromToken, BinaryExpr, Expr>().Try().Or(ExprNotBinary)
             from offsetOrSubquery in OffsetOrSubquery(head)
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
    
    public struct ParsedValue<T>
    {
        public ParsedValue(T value, TextSpan span)
        {
            Value = value;
            Span = span;
        }
            
        public T Value { get; }
        public bool HasSpan => Span != TextSpan.None;
        public TextSpan Span { get; }
    }

    public static class Extensions
    {
        public static ParsedValue<T> ToParsedValue<T>(this T result, TextSpan start, TextSpan end)
        {
            return new ParsedValue<T>(result, start.UntilEnd(end));
        }
        
        public static ParsedValue<T> ToEmptyParsedValue<T>(this T result)
        {
            return new ParsedValue<T>(result, TextSpan.None);
        }
        
        public static TextSpan UntilEnd(this TextSpan @base, TextSpan? next)
        {
            if (next == null)
                return @base;
            
            int absolute1 = next.Value.Position.Absolute + next.Value.Length;
            int absolute2 = @base.Position.Absolute;
            return @base.First(absolute1 - absolute2);
        }

        public static ParsedValue<T> ToParsedValue<T>(this T result, TextSpan span)
        {
            return new ParsedValue<T>(result, span);
        }

    }
}
