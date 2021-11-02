using Superpower.Display;

namespace PromQL.Parser
{
	/// <summary>
	/// Lexical tokens that comprise a PromQL expression. 
	/// </summary>
	/// <remarks>
	/// Tokens are taken from https://github.com/prometheus/prometheus/blob/7471208b5c8ff6b65b644adedf7eb964da3d50ae/promql/parser/generated_parser.y#L43-L135
	/// Name casing is preserved (even though they violate the general C# style guidelines) 
	/// </remarks>
	public enum PromToken
	{
		None = 0,

		[Token(Example = "=")] EQL,
		[Token(Example = ":")] COLON,
		[Token(Example = ",")] COMMA,
		[Token] COMMENT,
		[Token] DURATION,

		// TODO Don't currentl use error, could be useful for more informative error messages?
		[Token] ERROR,

		[Token] IDENTIFIER,
		[Token(Example = "{")] LEFT_BRACE,
		[Token(Example = "[")] LEFT_BRACKET,
		[Token(Example = "(")] LEFT_PAREN,
		[Token] METRIC_IDENTIFIER,
		[Token] NUMBER,
		[Token(Example = "}")] RIGHT_BRACE,
		[Token(Example = "]")] RIGHT_BRACKET,
		[Token(Example = ")")] RIGHT_PAREN,
		[Token(Example = ";")] SEMICOLON,
		[Token] STRING,
		[Token] TIMES,

		// Operators
		[Token(Category = "Operator", Example = "+")]
		ADD,

		[Token(Category = "Operator", Example = "/")]
		DIV,

		[Token(Category = "Operator", Example = "==")]
		EQLC,

		[Token(Category = "Operator", Example = "=~")]
		EQL_REGEX,

		[Token(Category = "Operator", Example = ">=")]
		GTE,

		[Token(Category = "Operator", Example = ">")]
		GTR,

		[Token(Category = "Operator")]
		LAND,

		[Token(Category = "Operator")]
		LOR,

		[Token(Category = "Operator", Example = "<")]
		LSS,

		[Token(Category = "Operator", Example = "<=")]
		LTE,

		[Token(Category = "Operator")]
		LUNLESS,

		[Token(Category = "Operator", Example = "%")]
		MOD,

		[Token(Category = "Operator", Example = "*")]
		MUL,

		[Token(Category = "Operator", Example = "!=")]
		NEQ,

		[Token(Category = "Operator", Example = "!~")]
		NEQ_REGEX,

		[Token(Category = "Operator", Example = "^")]
		POW,

		[Token(Category = "Operator", Example = "-")]
		SUB,

		[Token(Category = "Operator", Example = "@")]
		AT,

		[Token(Category = "Operator", Example = "atan2")]
		ATAN2,

		// Aggregators
		[Token]
		AGGREGATE_OP,

		// Keywords
		BOOL,

		[Token(Category = "Keyword")]
		BY,

		[Token(Category = "Keyword")]
		GROUP_LEFT,

		[Token(Category = "Keyword")]
		GROUP_RIGHT,

		[Token(Category = "Keyword")]
		IGNORING,

		[Token(Category = "Keyword")]
		OFFSET,

		[Token(Category = "Keyword")]
		ON,

		[Token(Category = "Keyword")]
		WITHOUT,

		// Preprocessors
	}
}
