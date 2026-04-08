using System;
using System.Collections.Generic;
using System.Globalization;

namespace BabaShell;

public sealed class Lexer
{
    private readonly string _source;
    private readonly List<Token> _tokens = new();
    private int _start;
    private int _current;
    private int _line = 1;
    private int _column = 1;

    private static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["emit"] = TokenType.EMIT,
        ["when"] = TokenType.WHEN,
        ["if"] = TokenType.IF,
        ["else"] = TokenType.ELSE,
        ["try"] = TokenType.TRY,
        ["catch"] = TokenType.CATCH,
        ["throw"] = TokenType.THROW,
        ["while"] = TokenType.WHILE,
        ["break"] = TokenType.BREAK,
        ["continue"] = TokenType.CONTINUE,
        ["class"] = TokenType.CLASS,
        ["new"] = TokenType.NEW,
        ["this"] = TokenType.THIS,
        ["loop"] = TokenType.LOOP,
        ["for"] = TokenType.FOR,
        ["in"] = TokenType.IN,
        ["repeat"] = TokenType.REPEAT,
        ["times"] = TokenType.TIMES,
        ["func"] = TokenType.FUNC,
        ["call"] = TokenType.CALL,
        ["return"] = TokenType.RETURN,
        ["import"] = TokenType.IMPORT,
        ["clicked"] = TokenType.CLICKED,
        ["hover"] = TokenType.HOVER,
        ["set"] = TokenType.SET,
        ["store"] = TokenType.STORE,
        ["increase"] = TokenType.INCREASE,
        ["decrease"] = TokenType.DECREASE,
        ["by"] = TokenType.BY,
        ["wait"] = TokenType.WAIT,
        ["fetch"] = TokenType.FETCH,
        ["as"] = TokenType.AS,
        ["true"] = TokenType.TRUE,
        ["false"] = TokenType.FALSE,
        ["null"] = TokenType.NULL,
        ["and"] = TokenType.AND,
        ["or"] = TokenType.OR,
        ["map"] = TokenType.MAP,
    };

    public Lexer(string source)
    {
        _source = source;
    }

    public List<Token> ScanTokens()
    {
        while (!IsAtEnd())
        {
            _start = _current;
            ScanToken();
        }

        _tokens.Add(new Token(TokenType.EOF, string.Empty, null, _line, _column));
        return _tokens;
    }

    private void ScanToken()
    {
        var c = Advance();
        switch (c)
        {
            case '(': AddToken(TokenType.LPAREN); break;
            case ')': AddToken(TokenType.RPAREN); break;
            case '{': AddToken(TokenType.LBRACE); break;
            case '}': AddToken(TokenType.RBRACE); break;
            case '[': AddToken(TokenType.LBRACKET); break;
            case ']': AddToken(TokenType.RBRACKET); break;
            case ',': AddToken(TokenType.COMMA); break;
            case '.': AddToken(Match('.') ? TokenType.RANGE : TokenType.DOT); break;
            case ':': AddToken(TokenType.COLON); break;
            case ';': AddToken(TokenType.SEMICOLON); break;
            case '+': AddToken(Match('=') ? TokenType.PLUS_EQUAL : TokenType.PLUS); break;
            case '-': AddToken(Match('=') ? TokenType.MINUS_EQUAL : TokenType.MINUS); break;
            case '*': AddToken(Match('=') ? TokenType.STAR_EQUAL : TokenType.STAR); break;
            case '%': AddToken(Match('=') ? TokenType.PERCENT_EQUAL : TokenType.PERCENT); break;
            case '!': AddToken(Match('=') ? TokenType.BANG_EQUAL : TokenType.BANG); break;
            case '=': AddToken(Match('=') ? TokenType.EQUAL_EQUAL : TokenType.EQUAL); break;
            case '<': AddToken(Match('=') ? TokenType.LESS_EQUAL : TokenType.LESS); break;
            case '>': AddToken(Match('=') ? TokenType.GREATER_EQUAL : TokenType.GREATER); break;
            case '/':
                if (Match('/'))
                {
                    while (Peek() != '\n' && !IsAtEnd()) Advance();
                }
                else
                {
                    AddToken(Match('=') ? TokenType.SLASH_EQUAL : TokenType.SLASH);
                }
                break;
            case ' ':
            case '\r':
            case '\t':
                break;
            case '\n':
                _line++;
                _column = 1;
                AddToken(TokenType.NEWLINE);
                break;
            case '"': String(); break;
            case '#': Selector(); break;
            default:
                if (IsDigit(c))
                {
                    Number();
                }
                else if (IsAlpha(c))
                {
                    Identifier();
                }
                else
                {
                    ErrorReporter.Syntax($"Unknown character: '{c}'", _line, _column - 1);
                }
                break;
        }
    }

    private void Identifier()
    {
        while (IsAlphaNumeric(Peek())) Advance();

        var text = _source.Substring(_start, _current - _start);
        if (Keywords.TryGetValue(text, out var type))
        {
            AddToken(type);
        }
        else
        {
            AddToken(TokenType.IDENT);
        }
    }

    private void Number()
    {
        while (IsDigit(Peek())) Advance();
        if (Peek() == '.' && IsDigit(PeekNext()))
        {
            Advance();
            while (IsDigit(Peek())) Advance();
        }

        var text = _source.Substring(_start, _current - _start);
        if (!double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            ErrorReporter.Syntax($"Invalid number: {text}", _line, _column);
        }

        AddToken(TokenType.NUMBER, value);
    }

    private void String()
    {
        var value = new System.Text.StringBuilder();
        while (!IsAtEnd() && Peek() != '"')
        {
            if (Peek() == '\n')
            {
                _line++;
                _column = 1;
            }

            if (Peek() == '\\')
            {
                Advance();
                if (IsAtEnd()) break;
                var esc = Advance();
                value.Append(esc switch
                {
                    'n' => '\n',
                    't' => '\t',
                    '"' => '"',
                    '\\' => '\\',
                    _ => esc
                });
            }
            else
            {
                value.Append(Advance());
            }
        }

        if (IsAtEnd())
        {
            ErrorReporter.Syntax("Unterminated string.", _line, _column);
        }

        Advance();
        AddToken(TokenType.STRING, value.ToString());
    }

    private void Selector()
    {
        while (IsSelectorChar(Peek())) Advance();
        var text = _source.Substring(_start, _current - _start);
        AddToken(TokenType.SELECTOR, text);
    }

    private bool Match(char expected)
    {
        if (IsAtEnd()) return false;
        if (_source[_current] != expected) return false;
        _current++;
        _column++;
        return true;
    }

    private char Peek() => IsAtEnd() ? '\0' : _source[_current];
    private char PeekNext() => _current + 1 >= _source.Length ? '\0' : _source[_current + 1];

    private bool IsAtEnd() => _current >= _source.Length;

    private char Advance()
    {
        _current++;
        _column++;
        return _source[_current - 1];
    }

    private void AddToken(TokenType type) => AddToken(type, null);

    private void AddToken(TokenType type, object? literal)
    {
        var text = _source.Substring(_start, _current - _start);
        _tokens.Add(new Token(type, text, literal, _line, _column - (_current - _start)));
    }

    private static bool IsDigit(char c) => c is >= '0' and <= '9';
    private static bool IsAlpha(char c) => char.IsLetter(c) || c == '_';
    private static bool IsAlphaNumeric(char c) => IsAlpha(c) || IsDigit(c);
    private static bool IsSelectorChar(char c) => IsAlphaNumeric(c) || c == '-' || c == '_';
}
