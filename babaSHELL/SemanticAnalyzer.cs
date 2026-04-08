using System;
using System.Collections.Generic;
using System.IO;

namespace BabaShell;

public sealed class SemanticAnalyzer
{
    private readonly Stack<HashSet<string>> _scopes = new();
    private readonly HashSet<string> _globals = Builtins.GetGlobalNames();
    private int _loopDepth;
    private int _functionDepth;
    private int _classDepth;

    public void Analyze(List<Stmt> statements)
    {
        BeginScope();
        foreach (var global in _globals)
        {
            Declare(global);
        }

        foreach (var statement in statements)
        {
            Analyze(statement);
        }
        EndScope();
    }

    private void Analyze(Stmt stmt)
    {
        switch (stmt)
        {
            case ExprStmt e:
                Analyze(e.Expression);
                break;
            case PrintStmt p:
                foreach (var expr in p.Expressions) Analyze(expr);
                break;
            case BlockStmt b:
                BeginScope();
                foreach (var statement in b.Statements) Analyze(statement);
                EndScope();
                break;
            case IfStmt i:
                Analyze(i.Condition);
                Analyze(i.ThenBranch);
                if (i.ElseBranch != null) Analyze(i.ElseBranch);
                break;
            case TryCatchStmt tc:
                Analyze(tc.TryBranch);
                BeginScope();
                if (!string.IsNullOrWhiteSpace(tc.ErrorName))
                {
                    Declare(tc.ErrorName!);
                }
                Analyze(tc.CatchBranch);
                EndScope();
                break;
            case ThrowStmt t:
                Analyze(t.Value);
                break;
            case WhileStmt w:
                Analyze(w.Condition);
                WithLoop(() => Analyze(w.Body));
                break;
            case BreakStmt:
                if (_loopDepth == 0) ErrorReporter.Runtime("Semantic error: 'break' can only be used inside a loop.");
                break;
            case ContinueStmt:
                if (_loopDepth == 0) ErrorReporter.Runtime("Semantic error: 'continue' can only be used inside a loop.");
                break;
            case VarDeclStmt v:
                Analyze(v.Initializer);
                DeclareInCurrentScope(v.Name, $"Semantic error: variable '{v.Name}' is already declared in this scope.");
                break;
            case AdjustStmt a:
                EnsureDefined(a.Name, $"Semantic error: variable '{a.Name}' is not defined.");
                Analyze(a.Amount);
                break;
            case ForStmt f:
                Analyze(f.Start);
                Analyze(f.End);
                WithLoop(() =>
                {
                    BeginScope();
                    DeclareInCurrentScope(f.Name, $"Semantic error: loop variable '{f.Name}' is already declared in this scope.");
                    Analyze(f.Body);
                    EndScope();
                });
                break;
            case RepeatStmt r:
                Analyze(r.Count);
                WithLoop(() => Analyze(r.Body));
                break;
            case ForEachStmt fe:
                Analyze(fe.Collection);
                WithLoop(() =>
                {
                    BeginScope();
                    DeclareInCurrentScope(fe.Name, $"Semantic error: loop variable '{fe.Name}' is already declared in this scope.");
                    Analyze(fe.Body);
                    EndScope();
                });
                break;
            case FuncStmt fn:
                DeclareInCurrentScope(fn.Name, $"Semantic error: function '{fn.Name}' is already declared in this scope.");
                AnalyzeFunction(fn, inClass: false);
                break;
            case ReturnStmt r:
                if (_functionDepth == 0) ErrorReporter.Runtime("Semantic error: 'return' can only be used inside a function.");
                if (r.Value != null) Analyze(r.Value);
                break;
            case ImportStmt im:
                DeclareInCurrentScope(GetModuleName(im.Path), $"Semantic error: module alias '{GetModuleName(im.Path)}' is already declared in this scope.");
                break;
            case WhenEventStmt we:
                Analyze(we.Body);
                break;
            case SetStmt s:
                Analyze(s.Value);
                break;
            case WaitStmt w:
                Analyze(w.Body);
                break;
            case FetchStmt f:
                Analyze(f.Url);
                BeginScope();
                DeclareInCurrentScope(f.TargetName, $"Semantic error: variable '{f.TargetName}' is already declared in this scope.");
                Analyze(f.Body);
                EndScope();
                break;
            case ClassStmt c:
                DeclareInCurrentScope(c.Name, $"Semantic error: class '{c.Name}' is already declared in this scope.");
                AnalyzeClass(c);
                break;
            case ExportStmt ex:
                Analyze(ex.Declaration);
                break;
            default:
                break;
        }
    }

    private void Analyze(Expr expr)
    {
        switch (expr)
        {
            case LiteralExpr:
                return;
            case VariableExpr v:
                if (string.Equals(v.Name, "this", StringComparison.OrdinalIgnoreCase) && _classDepth == 0)
                {
                    ErrorReporter.Runtime("Semantic error: 'this' can only be used inside a class method.");
                }
                EnsureDefined(v.Name, $"Semantic error: variable '{v.Name}' is not defined.");
                return;
            case AssignExpr a:
                EnsureDefined(a.Name, $"Semantic error: variable '{a.Name}' is not defined.");
                Analyze(a.Value);
                return;
            case BinaryExpr b:
                Analyze(b.Left);
                Analyze(b.Right);
                return;
            case UnaryExpr u:
                Analyze(u.Right);
                return;
            case CallExpr c:
                Analyze(c.Callee);
                foreach (var argument in c.Arguments) Analyze(argument);
                return;
            case GetExpr g:
                Analyze(g.Object);
                return;
            case SetExpr s:
                Analyze(s.Object);
                Analyze(s.Value);
                return;
            case IndexExpr i:
                Analyze(i.Object);
                Analyze(i.Index);
                return;
            case SetIndexExpr si:
                Analyze(si.Object);
                Analyze(si.Index);
                Analyze(si.Value);
                return;
            case ArrayExpr a:
                foreach (var element in a.Elements) Analyze(element);
                return;
            case DictExpr d:
                foreach (var value in d.Values) Analyze(value);
                return;
            case NewExpr n:
                Analyze(n.Target);
                return;
            default:
                return;
        }
    }

    private void AnalyzeFunction(FuncStmt function, bool inClass)
    {
        _functionDepth++;
        if (inClass) _classDepth++;

        BeginScope();
        if (inClass)
        {
            Declare("this");
        }
        foreach (var parameter in function.Params)
        {
            DeclareInCurrentScope(parameter, $"Semantic error: parameter '{parameter}' is already declared in this scope.");
        }
        foreach (var statement in function.Body.Statements)
        {
            Analyze(statement);
        }
        EndScope();

        if (inClass) _classDepth--;
        _functionDepth--;
    }

    private void AnalyzeClass(ClassStmt klass)
    {
        foreach (var method in klass.Methods)
        {
            AnalyzeFunction(method, inClass: true);
        }
    }

    private void BeginScope()
    {
        _scopes.Push(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private void EndScope()
    {
        _scopes.Pop();
    }

    private void Declare(string name)
    {
        if (_scopes.Count == 0) return;
        _scopes.Peek().Add(name);
    }

    private void DeclareInCurrentScope(string name, string duplicateMessage)
    {
        if (_scopes.Count == 0) return;
        var scope = _scopes.Peek();
        if (scope.Contains(name))
        {
            ErrorReporter.Runtime(duplicateMessage);
        }
        scope.Add(name);
    }

    private void EnsureDefined(string name, string message)
    {
        if (_globals.Contains(name)) return;
        foreach (var scope in _scopes)
        {
            if (scope.Contains(name)) return;
        }
        ErrorReporter.Runtime(message);
    }

    private void WithLoop(Action action)
    {
        _loopDepth++;
        try
        {
            action();
        }
        finally
        {
            _loopDepth--;
        }
    }

    private static string GetModuleName(string path)
    {
        var normalized = path.EndsWith(".babashell", StringComparison.OrdinalIgnoreCase)
            ? path
            : path + ".babashell";
        var moduleName = Path.GetFileNameWithoutExtension(normalized);
        var dot = moduleName.LastIndexOf('.');
        if (dot >= 0 && dot < moduleName.Length - 1)
        {
            moduleName = moduleName[(dot + 1)..];
        }
        return moduleName;
    }
}
