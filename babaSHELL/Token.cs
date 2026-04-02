namespace BabaShell;

public enum TokenType
{
    EOF,
    NEWLINE,
    IDENT,
    NUMBER,
    STRING,

    LPAREN, RPAREN,
    LBRACE, RBRACE,
    LBRACKET, RBRACKET,
    COMMA, DOT, COLON, SEMICOLON,

    PLUS, MINUS, STAR, SLASH, PERCENT,
    BANG, BANG_EQUAL,
    EQUAL, EQUAL_EQUAL,
    GREATER, GREATER_EQUAL,
    LESS, LESS_EQUAL,
    RANGE,

    EMIT, WHEN, ELSE, LOOP, FUNC, RETURN, IMPORT,
    TRUE, FALSE, NULL,
    AND, OR,
    MAP
}

public sealed class Token
{
    public Token(TokenType type, string lexeme, object? literal, int line, int column)
    {
        Type = type;
        Lexeme = lexeme;
        Literal = literal;
        Line = line;
        Column = column;
    }

    public TokenType Type { get; }
    public string Lexeme { get; }
    public object? Literal { get; }
    public int Line { get; }
    public int Column { get; }

    public override string ToString() => $"{Type} {Lexeme} {Literal}";
}
