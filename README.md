# PromQl.Parser
A parser for the Prometheus Query Language (PromQL), written in C# and using the [Superpower](https://github.com/datalust/superpower) parsing library.

## Installation
**Requires .NET core 3.1+**

TODO


## User guide
### Parsing and validating PromQl
`Parser.ParseExpression` will parse a provided expression into an Abstract Syntax Tree (AST) that represents the PromQL components:

```csharp
Parser.ParseExpression(@"
	# This is a test expression
	sum by (code) (rate(http_request_count{code !~ '5xx'}[1m]))
	/ sum by (code) (rate(http_request_count[1m]))
").ToString()
```

Returns:
```
BinaryExpr { 
  LeftHandSide = AggregateExpr { 
    OperatorName = sum, 
    Expr = FunctionCall { 
      Identifier = rate, 
      Args = System.Collections.Immutable.ImmutableArray`1[PromQL.Parser.Expr] }, 
      Param = , 
      GroupingLabels = System.Collections.Immutable.ImmutableArray`1[System.String], 
      Without = False 
  }, 
  RightHandSide = AggregateExpr { 
    OperatorName = sum, 
    Expr = FunctionCall { 
      Identifier = rate, 
      Args = System.Collections.Immutable.ImmutableArray`1[PromQL.Parser.Expr] 
    }, 
    Param = , 
    GroupingLabels = System.Collections.Immutable.ImmutableArray`1[System.String], 
    Without = False 
  }, 
  Operator = Div, 
  VectorMatching = VectorMatching { 
    MatchCardinality = OneToOne, 
    MatchingLabels = System.Collections.Immutable.ImmutableArray`1[System.String], 
    On = False, 
    Include = System.Collections.Immutable.ImmutableArray`1[System.String], 
    ReturnBool = False 
  } 
}
```

Invalid expressions will throw an exception:
```csharp
// Too many brackets + missing rest of matrix selector
Parser.ParseExpression("http_request_count[[ ")
```
Throws:
```
Superpower.ParseException: Syntax error (line 1, column 21): Unexpected left bracket.
   at Superpower.Tokenizer`1.Tokenize(String source)
   at PromQL.Parser.Parser.ParseExpression(String input, Tokenizer tokenizer)
   at UserQuery.Main() in C:\Users\james.luck\AppData\Local\Temp\LINQPad6\_aryncqeb\dwfjew\LINQPadQuery:line [[
```

**Only syntax is validated, semantic validation is not applied**. For example, the following expressions 
are considered valid by this library:

```
# Invalid number of function arguments
time(1, 2, 3, 4)

# Invalid number and types of aggregation function arguments
sum(1.0, "a string")

# Binary operations not defined for strings
"a" + "b"

...
```

### Modifying PromQL expressions
The Abstract Syntax Tree is represented using [`record` types](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record).
This means making copies of the AST is trivial:
```csharp
var expr = (BinaryExpr) Parser.ParseExpression(@"1 + 1");

// original expression is not mutated
var newExpr = expr with { RightHandSide = new NumberLiteral(2) };
```

`newExpr` equals:
```
BinaryExpr { 
  LeftHandSide = NumberLiteral { Value = 1 }, 
  RightHandSide = NumberLiteral { Value = 2 }, 
  Operator = Add, 
  VectorMatching = ...
}
```

### Emitting PromQL expressions
An Abstract Syntax Tree can be converted back to its PromQL string representation, e.g:
```csharp
var printer = new Printer();
var expr = Parser.ParseExpression(@"
# A comment
sum(
	avg_over_time(metric[1h:5m])
) by (label1)
");
printer.ToPromQl(expr);
```

Produces:
```
// NOTE:
// 1. Comments are not preserved
// 2. Whitespace/ indentation is not preserved
// 3. Language elements may in a different order than originally specified
sum by (label1) (avg_over_time(metric[1h:5m]))
```

### Creating PromQl expressions
- Won't guard against creation of semantically invalid expressions
- Won't guard against all forms of syntactically invalid expressions

### Parsing extensions to PromQL

## Unsupported language features
This library aims to parse 99% of the PromQL language. However, some language features are currently unsupported:
- Unicode string escaping
- Hexadecimal/ octal string escaping
- Hexadecimal number representation 
- [The `@` modifier](https://prometheus.io/docs/prometheus/latest/querying/basics/#modifier)
- Operator associativity (all operators are parsed with the same associativity)

If any of these features are important to you, please create an issue or submit a pull request. 


