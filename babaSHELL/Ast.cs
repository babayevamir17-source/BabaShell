using System.Collections.Generic;

namespace BabaShell;

public abstract class Stmt { }

public sealed class ExprStmt(Expr Expression) : Stmt
{
    public Expr Expression { get; } = Expression;
}

public sealed class PrintStmt(List<Expr> Expressions) : Stmt
{
    public List<Expr> Expressions { get; } = Expressions;
}

public sealed class BlockStmt(List<Stmt> Statements) : Stmt
{
    public List<Stmt> Statements { get; } = Statements;
}

public sealed class IfStmt(Expr Condition, Stmt ThenBranch, Stmt? ElseBranch) : Stmt
{
    public Expr Condition { get; } = Condition;
    public Stmt ThenBranch { get; } = ThenBranch;
    public Stmt? ElseBranch { get; } = ElseBranch;
}

public sealed class ForStmt(string Name, Expr Start, Expr End, Stmt Body) : Stmt
{
    public string Name { get; } = Name;
    public Expr Start { get; } = Start;
    public Expr End { get; } = End;
    public Stmt Body { get; } = Body;
}

public sealed class FuncStmt(string Name, List<string> Params, BlockStmt Body) : Stmt
{
    public string Name { get; } = Name;
    public List<string> Params { get; } = Params;
    public BlockStmt Body { get; } = Body;
}

public sealed class ReturnStmt(Expr? Value) : Stmt
{
    public Expr? Value { get; } = Value;
}

public sealed class ImportStmt(string Path) : Stmt
{
    public string Path { get; } = Path;
}

public sealed class WhenEventStmt(string Selector, string EventName, Stmt Body) : Stmt
{
    public string Selector { get; } = Selector;
    public string EventName { get; } = EventName;
    public Stmt Body { get; } = Body;
}

public abstract class Expr { }

public sealed class LiteralExpr(object? Value) : Expr
{
    public object? Value { get; } = Value;
}

public sealed class VariableExpr(string Name) : Expr
{
    public string Name { get; } = Name;
}

public sealed class AssignExpr(string Name, Expr Value) : Expr
{
    public string Name { get; } = Name;
    public Expr Value { get; } = Value;
}

public sealed class BinaryExpr(Expr Left, Token Operator, Expr Right) : Expr
{
    public Expr Left { get; } = Left;
    public Token Operator { get; } = Operator;
    public Expr Right { get; } = Right;
}

public sealed class UnaryExpr(Token Operator, Expr Right) : Expr
{
    public Token Operator { get; } = Operator;
    public Expr Right { get; } = Right;
}

public sealed class CallExpr(Expr Callee, List<Expr> Arguments) : Expr
{
    public Expr Callee { get; } = Callee;
    public List<Expr> Arguments { get; } = Arguments;
}

public sealed class GetExpr(Expr Object, string Name) : Expr
{
    public Expr Object { get; } = Object;
    public string Name { get; } = Name;
}

public sealed class SetExpr(Expr Object, string Name, Expr Value) : Expr
{
    public Expr Object { get; } = Object;
    public string Name { get; } = Name;
    public Expr Value { get; } = Value;
}

public sealed class IndexExpr(Expr Object, Expr Index) : Expr
{
    public Expr Object { get; } = Object;
    public Expr Index { get; } = Index;
}

public sealed class SetIndexExpr(Expr Object, Expr Index, Expr Value) : Expr
{
    public Expr Object { get; } = Object;
    public Expr Index { get; } = Index;
    public Expr Value { get; } = Value;
}

public sealed class ArrayExpr(List<Expr> Elements) : Expr
{
    public List<Expr> Elements { get; } = Elements;
}

public sealed class DictExpr(List<string> Keys, List<Expr> Values) : Expr
{
    public List<string> Keys { get; } = Keys;
    public List<Expr> Values { get; } = Values;
}
