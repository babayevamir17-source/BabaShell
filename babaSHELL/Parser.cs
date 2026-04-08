using System;
using System.Collections.Generic;

namespace BabaShell;

public sealed class Parser
{
    private readonly List<Token> _tokens;
    private int _current;

    public Parser(List<Token> tokens)
    {
        _tokens = tokens;
    }

    public List<Stmt> Parse()
    {
        var statements = new List<Stmt>();
        while (!IsAtEnd())
        {
            if (Match(TokenType.NEWLINE, TokenType.SEMICOLON)) continue;
            statements.Add(Declaration());
        }
        return statements;
    }

    private Stmt Declaration()
    {
        if (Match(TokenType.CLASS)) return ClassDeclaration();
        if (Match(TokenType.FUNC)) return FunctionDeclaration();
        if (Match(TokenType.STORE)) return StoreDeclaration();
        return Statement();
    }

    private Stmt Statement()
    {
        if (Match(TokenType.EMIT)) return PrintStatement();
        if (Match(TokenType.IF)) return IfStatement();
        if (Match(TokenType.TRY)) return TryCatchStatement();
        if (Match(TokenType.THROW)) return ThrowStatement();
        if (Match(TokenType.WHILE)) return WhileStatement();
        if (Match(TokenType.WHEN)) return WhenStatement();
        if (Match(TokenType.BREAK))
        {
            ConsumeLineEnd();
            return new BreakStmt();
        }
        if (Match(TokenType.CONTINUE))
        {
            ConsumeLineEnd();
            return new ContinueStmt();
        }
        if (Match(TokenType.SET)) return SetStatement();
        if (Match(TokenType.INCREASE)) return AdjustStatement(isIncrease: true);
        if (Match(TokenType.DECREASE)) return AdjustStatement(isIncrease: false);
        if (Match(TokenType.REPEAT)) return RepeatStatement();
        if (Match(TokenType.FOR)) return ForEachStatement();
        if (Match(TokenType.LOOP)) return ForStatement();
        if (Match(TokenType.WAIT)) return WaitStatement();
        if (Match(TokenType.FETCH)) return FetchStatement();
        if (Match(TokenType.CALL)) return CallStatement();
        if (Match(TokenType.RETURN)) return ReturnStatement();
        if (Match(TokenType.IMPORT)) return ImportStatement();
        if (Match(TokenType.LBRACE)) return new BlockStmt(Block());
        return ExpressionStatement();
    }

    private Stmt StoreDeclaration()
    {
        var name = Consume(TokenType.IDENT, "Expected variable name after 'store'.").Lexeme;
        Consume(TokenType.EQUAL, "Expected '=' in store declaration.");
        var init = Expression();
        ConsumeLineEnd();
        return new VarDeclStmt(name, init);
    }

    private Stmt PrintStatement()
    {
        var exprs = new List<Expr> { Expression() };
        while (Match(TokenType.COMMA))
        {
            exprs.Add(Expression());
        }
        ConsumeLineEnd();
        return new PrintStmt(exprs);
    }

    private Stmt WhenStatement()
    {
        if (!Check(TokenType.SELECTOR) && !Check(TokenType.STRING) && !Check(TokenType.IDENT))
        {
            var token = Peek();
            ErrorReporter.Syntax("Expected a selector after 'when'. Use 'if' for conditions.", token.Line, token.Column);
        }

        var selectorToken = Advance();
        string? eventName = null;
        if (Match(TokenType.CLICKED))
        {
            eventName = "clicked";
        }
        else if (Match(TokenType.HOVER))
        {
            eventName = "hover";
        }
        else if (Check(TokenType.IDENT))
        {
            eventName = Advance().Lexeme;
        }

        if (eventName == null)
        {
            var token = Peek();
            ErrorReporter.Syntax("Expected an event name after the selector in 'when'. Use 'if' for conditional logic.", token.Line, token.Column);
        }

        var selector = selectorToken.Type == TokenType.STRING
            ? (string)selectorToken.Literal!
            : selectorToken.Lexeme;
        var body = Statement();
        return new WhenEventStmt(selector, eventName!, body);
    }

    private Stmt SetStatement()
    {
        string selector;
        if (Match(TokenType.SELECTOR))
        {
            selector = Previous().Lexeme;
        }
        else if (Match(TokenType.STRING))
        {
            selector = (string)Previous().Literal!;
        }
        else
        {
            var t = Peek();
            ErrorReporter.Syntax("Expected selector after 'set'.", t.Line, t.Column);
            selector = "";
        }

        var prop = Consume(TokenType.IDENT, "Expected property name.").Lexeme;
        // Support kebab-case properties like background-color
        while (Match(TokenType.MINUS))
        {
            var part = Consume(TokenType.IDENT, "Expected property name part after '-'.").Lexeme;
            prop += "-" + part;
        }
        _ = Match(TokenType.EQUAL);
        var value = Expression();
        ConsumeLineEnd();
        return new SetStmt(selector, prop, value);
    }

    private Stmt IfStatement()
    {
        var condition = Expression();
        var thenBranch = ParseControlBody();
        ConsumeOptionalSeparators();
        Stmt? elseBranch = null;
        if (Match(TokenType.ELSE))
        {
            ConsumeOptionalSeparators();
            elseBranch = Match(TokenType.IF)
                ? IfStatement()
                : ParseControlBody();
        }
        return new IfStmt(condition, thenBranch, elseBranch);
    }

    private Stmt WhileStatement()
    {
        var condition = Expression();
        var body = ParseControlBody();
        return new WhileStmt(condition, body);
    }

    private Stmt TryCatchStatement()
    {
        var tryBranch = ParseControlBody();
        ConsumeOptionalSeparators();
        Consume(TokenType.CATCH, "Expected 'catch' after try block.");

        string? errorName = null;
        if (Check(TokenType.IDENT))
        {
            errorName = Advance().Lexeme;
        }

        var catchBranch = ParseControlBody();
        return new TryCatchStmt(tryBranch, errorName, catchBranch);
    }

    private Stmt ThrowStatement()
    {
        var value = Expression();
        ConsumeLineEnd();
        return new ThrowStmt(value);
    }

    private Stmt AdjustStatement(bool isIncrease)
    {
        var name = Consume(TokenType.IDENT, $"Expected variable name after '{(isIncrease ? "increase" : "decrease")}'.").Lexeme;
        Consume(TokenType.BY, "Expected 'by'.");
        var amount = Expression();
        ConsumeLineEnd();
        return new AdjustStmt(name, isIncrease, amount);
    }

    private Stmt RepeatStatement()
    {
        var countExpr = Expression();
        Consume(TokenType.TIMES, "Expected 'times' after repeat count.");
        var body = ParseControlBody();
        return new RepeatStmt(countExpr, body);
    }

    private Stmt ForEachStatement()
    {
        var name = Consume(TokenType.IDENT, "Expected loop variable name after 'for'.").Lexeme;
        Consume(TokenType.IN, "Expected 'in'.");
        var collection = Expression();
        var body = ParseControlBody();
        return new ForEachStmt(name, collection, body);
    }

    private Stmt WaitStatement()
    {
        var durationMs = ParseDurationMs();
        var body = ParseControlBody();
        return new WaitStmt(durationMs, body);
    }

    private int ParseDurationMs()
    {
        var numberToken = Consume(TokenType.NUMBER, "Expected duration number after 'wait'.");
        var value = numberToken.Literal is double d ? d : 0;
        var unit = "ms";
        if (Check(TokenType.IDENT))
        {
            unit = Advance().Lexeme.ToLowerInvariant();
        }

        var ms = unit switch
        {
            "ms" => value,
            "s" or "sec" or "secs" or "second" or "seconds" => value * 1000,
            "m" or "min" or "mins" or "minute" or "minutes" => value * 60_000,
            _ => value
        };

        if (ms < 0) ms = 0;
        return (int)Math.Round(ms);
    }

    private Stmt FetchStatement()
    {
        var urlExpr = Expression();
        Consume(TokenType.AS, "Expected 'as' in fetch statement.");
        var target = Consume(TokenType.IDENT, "Expected variable name after 'as'.").Lexeme;
        var body = ParseControlBody();
        return new FetchStmt(urlExpr, target, body);
    }

    private Stmt CallStatement()
    {
        var callee = Expression();
        if (callee is not CallExpr)
        {
            var t = Previous();
            ErrorReporter.Syntax("Expected function call after 'call'.", t.Line, t.Column);
        }
        ConsumeLineEnd();
        return new ExprStmt(callee);
    }

    private Stmt ForStatement()
    {
        var name = Consume(TokenType.IDENT, "Expected loop variable name.").Lexeme;
        Consume(TokenType.EQUAL, "Expected '='.");
        var start = Expression();
        Consume(TokenType.RANGE, "Expected '..'.");
        var end = Expression();
        var body = ParseControlBody();
        return new ForStmt(name, start, end, body);
    }

    private Stmt ReturnStatement()
    {
        if (Check(TokenType.NEWLINE) || Check(TokenType.SEMICOLON) || Check(TokenType.RBRACE))
        {
            ConsumeLineEnd();
            return new ReturnStmt(null);
        }
        var value = Expression();
        ConsumeLineEnd();
        return new ReturnStmt(value);
    }

    private Stmt ImportStatement()
    {
        string path;
        if (Match(TokenType.STRING))
        {
            path = (string)Previous().Literal!;
        }
        else
        {
            var parts = new List<string>();
            var first = Consume(TokenType.IDENT, "Expected import name.").Lexeme;
            parts.Add(first);
            while (Match(TokenType.DOT))
            {
                parts.Add(Consume(TokenType.IDENT, "Expected import name.").Lexeme);
            }
            path = string.Join(".", parts);
        }
        ConsumeLineEnd();
        return new ImportStmt(path);
    }

    private Stmt ExpressionStatement()
    {
        var expr = Expression();
        ConsumeLineEnd();
        return new ExprStmt(expr);
    }

    private Stmt FunctionDeclaration()
    {
        var name = Consume(TokenType.IDENT, "Expected function name.").Lexeme;
        return ParseFunctionBody(name);
    }

    private Stmt ClassDeclaration()
    {
        var name = Consume(TokenType.IDENT, "Expected class name.").Lexeme;
        Consume(TokenType.LBRACE, "Expected class body.");
        var methods = new List<FuncStmt>();

        while (!Check(TokenType.RBRACE) && !IsAtEnd())
        {
            if (Match(TokenType.NEWLINE, TokenType.SEMICOLON)) continue;
            Consume(TokenType.FUNC, "Expected 'func' before class method.");
            var methodName = Consume(TokenType.IDENT, "Expected method name.").Lexeme;
            methods.Add((FuncStmt)ParseFunctionBody(methodName));
        }

        Consume(TokenType.RBRACE, "Expected '}' after class body.");
        return new ClassStmt(name, methods);
    }

    private Stmt ParseFunctionBody(string name)
    {
        Consume(TokenType.LPAREN, "Expected '('.");
        var parameters = new List<string>();
        if (!Check(TokenType.RPAREN))
        {
            do
            {
                parameters.Add(Consume(TokenType.IDENT, "Expected parameter name.").Lexeme);
            } while (Match(TokenType.COMMA));
        }
        Consume(TokenType.RPAREN, "Expected ')'.");
        Consume(TokenType.LBRACE, "Expected function body.");
        var body = Block();
        return new FuncStmt(name, parameters, new BlockStmt(body));
    }

    private List<Stmt> Block()
    {
        var statements = new List<Stmt>();
        while (!Check(TokenType.RBRACE) && !IsAtEnd())
        {
            if (Match(TokenType.NEWLINE, TokenType.SEMICOLON)) continue;
            statements.Add(Declaration());
        }
        Consume(TokenType.RBRACE, "Expected '}'.");
        return statements;
    }

    private Expr Expression() => Assignment();

    private Expr Assignment()
    {
        var expr = Or();

        if (Match(TokenType.EQUAL, TokenType.PLUS_EQUAL, TokenType.MINUS_EQUAL, TokenType.STAR_EQUAL, TokenType.SLASH_EQUAL, TokenType.PERCENT_EQUAL))
        {
            var assignmentToken = Previous();
            var value = Assignment();
            Expr finalValue = value;

            if (expr is VariableExpr v)
            {
                if (assignmentToken.Type != TokenType.EQUAL)
                {
                    finalValue = new BinaryExpr(v, CompoundToBinaryOperator(assignmentToken), value);
                }
                return new AssignExpr(v.Name, finalValue);
            }
            if (expr is GetExpr g)
            {
                if (assignmentToken.Type != TokenType.EQUAL)
                {
                    finalValue = new BinaryExpr(new GetExpr(g.Object, g.Name), CompoundToBinaryOperator(assignmentToken), value);
                }
                return new SetExpr(g.Object, g.Name, finalValue);
            }
            if (expr is IndexExpr i)
            {
                if (assignmentToken.Type != TokenType.EQUAL)
                {
                    finalValue = new BinaryExpr(new IndexExpr(i.Object, i.Index), CompoundToBinaryOperator(assignmentToken), value);
                }
                return new SetIndexExpr(i.Object, i.Index, finalValue);
            }

            ErrorReporter.Syntax("Invalid assignment target.", assignmentToken.Line, assignmentToken.Column);
        }

        return expr;
    }

    private Expr Or()
    {
        var expr = And();
        while (Match(TokenType.OR))
        {
            var op = Previous();
            var right = And();
            expr = new BinaryExpr(expr, op, right);
        }
        return expr;
    }

    private Expr And()
    {
        var expr = Equality();
        while (Match(TokenType.AND))
        {
            var op = Previous();
            var right = Equality();
            expr = new BinaryExpr(expr, op, right);
        }
        return expr;
    }

    private Expr Equality()
    {
        var expr = Comparison();
        while (Match(TokenType.BANG_EQUAL, TokenType.EQUAL_EQUAL))
        {
            var op = Previous();
            var right = Comparison();
            expr = new BinaryExpr(expr, op, right);
        }
        return expr;
    }

    private Expr Comparison()
    {
        var expr = Term();
        while (Match(TokenType.GREATER, TokenType.GREATER_EQUAL, TokenType.LESS, TokenType.LESS_EQUAL))
        {
            var op = Previous();
            var right = Term();
            expr = new BinaryExpr(expr, op, right);
        }
        return expr;
    }

    private Expr Term()
    {
        var expr = Factor();
        while (Match(TokenType.PLUS, TokenType.MINUS))
        {
            var op = Previous();
            var right = Factor();
            expr = new BinaryExpr(expr, op, right);
        }
        return expr;
    }

    private Expr Factor()
    {
        var expr = Unary();
        while (Match(TokenType.STAR, TokenType.SLASH, TokenType.PERCENT))
        {
            var op = Previous();
            var right = Unary();
            expr = new BinaryExpr(expr, op, right);
        }
        return expr;
    }

    private Expr Unary()
    {
        if (Match(TokenType.BANG, TokenType.MINUS))
        {
            var op = Previous();
            var right = Unary();
            return new UnaryExpr(op, right);
        }
        if (Match(TokenType.NEW))
        {
            return new NewExpr(Call());
        }
        return Call();
    }

    private Expr Call()
    {
        var expr = Primary();

        while (true)
        {
            if (Match(TokenType.LPAREN))
            {
                var args = new List<Expr>();
                if (!Check(TokenType.RPAREN))
                {
                    do
                    {
                        args.Add(Expression());
                    } while (Match(TokenType.COMMA));
                }
                Consume(TokenType.RPAREN, "Expected ')'.");
                expr = new CallExpr(expr, args);
            }
            else if (Match(TokenType.DOT))
            {
                var name = Consume(TokenType.IDENT, "Expected property name.").Lexeme;
                expr = new GetExpr(expr, name);
            }
            else if (Match(TokenType.LBRACKET))
            {
                var index = Expression();
                Consume(TokenType.RBRACKET, "Expected ']'.");
                expr = new IndexExpr(expr, index);
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    private Expr Primary()
    {
        if (Match(TokenType.NUMBER)) return new LiteralExpr(Previous().Literal);
        if (Match(TokenType.STRING)) return new LiteralExpr(Previous().Literal);
        if (Match(TokenType.SELECTOR)) return new LiteralExpr(Previous().Literal ?? Previous().Lexeme);
        if (Match(TokenType.TRUE)) return new LiteralExpr(true);
        if (Match(TokenType.FALSE)) return new LiteralExpr(false);
        if (Match(TokenType.NULL)) return new LiteralExpr(null);
        if (Match(TokenType.THIS)) return new VariableExpr("this");

        if (Match(TokenType.IDENT)) return new VariableExpr(Previous().Lexeme);

        if (Match(TokenType.LPAREN))
        {
            var expr = Expression();
            Consume(TokenType.RPAREN, "Expected ')'.");
            return expr;
        }

        if (Match(TokenType.LBRACKET))
        {
            var elements = new List<Expr>();
            if (!Check(TokenType.RBRACKET))
            {
                do
                {
                    elements.Add(Expression());
                } while (Match(TokenType.COMMA));
            }
            Consume(TokenType.RBRACKET, "Expected ']'.");
            return new ArrayExpr(elements);
        }

        if (Match(TokenType.MAP))
        {
            Consume(TokenType.LBRACE, "Expected '{'.");
            var keys = new List<string>();
            var values = new List<Expr>();
            if (!Check(TokenType.RBRACE))
            {
                do
                {
                    var keyToken = Consume(TokenType.STRING, "Map key must be a string.");
                    Consume(TokenType.COLON, "Expected ':'.");
                    var valueExpr = Expression();
                    keys.Add((string)keyToken.Literal!);
                    values.Add(valueExpr);
                } while (Match(TokenType.COMMA));
            }
            Consume(TokenType.RBRACE, "Expected '}'.");
            return new DictExpr(keys, values);
        }

        var token = Peek();
        ErrorReporter.Syntax("Expected expression.", token.Line, token.Column);
        return new LiteralExpr(null);
    }

    private void ConsumeLineEnd()
    {
        if (Match(TokenType.SEMICOLON)) return;
        if (Match(TokenType.NEWLINE))
        {
            while (Match(TokenType.NEWLINE)) { }
            return;
        }
        if (Check(TokenType.RBRACE) || Check(TokenType.EOF)) return;
    }

    private void ConsumeOptionalSeparators()
    {
        while (Match(TokenType.NEWLINE, TokenType.SEMICOLON)) { }
    }

    private Stmt ParseControlBody()
    {
        ConsumeOptionalSeparators();
        return Statement();
    }

    private static Token CompoundToBinaryOperator(Token token)
    {
        var binaryType = token.Type switch
        {
            TokenType.PLUS_EQUAL => TokenType.PLUS,
            TokenType.MINUS_EQUAL => TokenType.MINUS,
            TokenType.STAR_EQUAL => TokenType.STAR,
            TokenType.SLASH_EQUAL => TokenType.SLASH,
            TokenType.PERCENT_EQUAL => TokenType.PERCENT,
            _ => token.Type
        };

        return new Token(binaryType, token.Lexeme, token.Literal, token.Line, token.Column);
    }

    private bool Match(params TokenType[] types)
    {
        foreach (var type in types)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
        }
        return false;
    }

    private Token Consume(TokenType type, string message)
    {
        if (Check(type)) return Advance();
        var t = Peek();
        ErrorReporter.Syntax(message, t.Line, t.Column);
        return t;
    }

    private bool Check(TokenType type) => !IsAtEnd() && Peek().Type == type;
    private bool CheckNext(TokenType type) => _current + 1 < _tokens.Count && _tokens[_current + 1].Type == type;

    private Token Advance()
    {
        if (!IsAtEnd()) _current++;
        return Previous();
    }

    private bool IsAtEnd() => Peek().Type == TokenType.EOF;
    private Token Peek() => _tokens[_current];
    private Token Previous() => _tokens[_current - 1];
}
