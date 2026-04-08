namespace BabaShell;

public enum TokenType
{
    EOF,
    NEWLINE,
    IDENT,
    NUMBER,
    STRING,
    SELECTOR,

    LPAREN, RPAREN,
    LBRACE, RBRACE,
    LBRACKET, RBRACKET,
    COMMA, DOT, COLON, SEMICOLON,

    PLUS, PLUS_EQUAL, MINUS, MINUS_EQUAL, STAR, STAR_EQUAL, SLASH, SLASH_EQUAL, PERCENT, PERCENT_EQUAL,
    BANG, BANG_EQUAL,
    EQUAL, EQUAL_EQUAL,
    GREATER, GREATER_EQUAL,
    LESS, LESS_EQUAL,
    RANGE,

    EMIT, WHEN, ELSE, IF, TRY, CATCH, THROW, WHILE, BREAK, CONTINUE, CLASS, NEW, THIS, EXPORT, LOOP, FOR, IN, REPEAT, TIMES, FUNC, CALL, RETURN, IMPORT, CLICKED, HOVER, SET,
    STORE, INCREASE, DECREASE, BY, WAIT, FETCH, AS,
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
