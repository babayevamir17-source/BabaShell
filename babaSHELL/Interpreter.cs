using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace BabaShell;

public sealed class Interpreter
{
    private readonly BabaEnvironment _globals = new();
    private BabaEnvironment _environment;
    private readonly HashSet<string> _imported = new(StringComparer.OrdinalIgnoreCase);
    private string _currentFile = string.Empty;
    private readonly List<WhenEventStmt> _eventHandlers = new();

    public Interpreter()
    {
        _environment = _globals;
        Builtins.Register(_globals);
    }

    public void ExecuteFile(string path, string source)
    {
        ExecuteSource(path, source);
    }

    public void ExecuteSource(string virtualPath, string source)
    {
        _currentFile = virtualPath;
        var baseDir = Path.GetDirectoryName(_currentFile) ?? Directory.GetCurrentDirectory();
        var (_, stripped) = BabaHtmlDirective.Parse(source, baseDir);
        source = stripped;
        var lexer = new Lexer(source);
        var tokens = lexer.ScanTokens();
        var parser = new Parser(tokens);
        var statements = parser.Parse();
        Execute(statements);
    }

    private Dictionary<string, object?> ExecuteModule(string path, string source)
    {
        _currentFile = path;
        var lexer = new Lexer(source);
        var tokens = lexer.ScanTokens();
        var parser = new Parser(tokens);
        var statements = parser.Parse();

        var moduleEnv = new BabaEnvironment(_globals);
        ExecuteBlock(statements, moduleEnv);
        return moduleEnv.Snapshot();
    }

    public void Execute(List<Stmt> statements)
    {
        foreach (var stmt in statements)
        {
            Execute(stmt);
        }
    }

    public void ExecuteBlock(List<Stmt> statements, BabaEnvironment environment)
    {
        var previous = _environment;
        try
        {
            _environment = environment;
            foreach (var stmt in statements) Execute(stmt);
        }
        finally
        {
            _environment = previous;
        }
    }

    private void Execute(Stmt stmt)
    {
        switch (stmt)
        {
            case ExprStmt e:
                Evaluate(e.Expression);
                break;
            case PrintStmt p:
                var outputs = new List<string>();
                foreach (var expr in p.Expressions)
                {
                    outputs.Add(Stringify(Evaluate(expr)));
                }
                Console.WriteLine(string.Join(" ", outputs));
                break;
            case BlockStmt b:
                ExecuteBlock(b.Statements, new BabaEnvironment(_environment));
                break;
            case IfStmt i:
                if (IsTruthy(Evaluate(i.Condition))) Execute(i.ThenBranch);
                else if (i.ElseBranch != null) Execute(i.ElseBranch);
                break;
            case ForStmt f:
                ExecuteFor(f);
                break;
            case FuncStmt fn:
                _environment.Define(fn.Name, new BabaFunction(fn, _environment));
                break;
            case ReturnStmt r:
                throw new ReturnSignal(r.Value == null ? null : Evaluate(r.Value));
            case ImportStmt im:
                ExecuteImport(im.Path);
                break;
            case WhenEventStmt we:
                _eventHandlers.Add(we);
                break;
            default:
                ErrorReporter.Runtime("Unknown statement.");
                break;
        }
    }

    private void ExecuteFor(ForStmt f)
    {
        var startVal = Evaluate(f.Start);
        var endVal = Evaluate(f.End);
        var start = ToNumber(startVal, "Loop start must be a number.");
        var end = ToNumber(endVal, "Loop end must be a number.");

        var step = start <= end ? 1 : -1;
        for (var i = start; step > 0 ? i <= end : i >= end; i += step)
        {
            _environment.Assign(f.Name, i);
            Execute(f.Body);
        }
    }

    private void ExecuteImport(string path)
    {
        if (string.Equals(path, "math", StringComparison.OrdinalIgnoreCase))
        {
            var module = Builtins.CreateMathModule();
            _environment.Define("math", module);
            return;
        }

        var normalized = path.EndsWith(".babashell", StringComparison.OrdinalIgnoreCase)
            ? path
            : path + ".babashell";

        var baseDir = Path.GetDirectoryName(_currentFile) ?? Directory.GetCurrentDirectory();
        var fullPath = Path.GetFullPath(Path.Combine(baseDir, normalized));

        if (_imported.Contains(fullPath)) return;
        if (!File.Exists(fullPath))
        {
            ErrorReporter.Runtime($"Import file not found: {fullPath}");
            return;
        }

        _imported.Add(fullPath);
        var source = File.ReadAllText(fullPath);
        var prevFile = _currentFile;
        var exports = ExecuteModule(fullPath, source);
        _currentFile = prevFile;

        var moduleName = Path.GetFileNameWithoutExtension(fullPath);
        var dot = moduleName.LastIndexOf('.');
        if (dot >= 0 && dot < moduleName.Length - 1)
        {
            moduleName = moduleName[(dot + 1)..];
        }
        var moduleDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var globalsSnapshot = _globals.Snapshot();
        foreach (var kv in exports)
        {
            if (globalsSnapshot.ContainsKey(kv.Key)) continue;
            moduleDict[kv.Key] = kv.Value;
        }

        _environment.Define(moduleName, moduleDict);
    }

    private object? Evaluate(Expr expr)
    {
        switch (expr)
        {
            case LiteralExpr l:
                return l.Value;
            case VariableExpr v:
                return _environment.Get(v.Name);
            case AssignExpr a:
                var value = Evaluate(a.Value);
                _environment.Assign(a.Name, value);
                return value;
            case BinaryExpr b:
                return EvaluateBinary(b);
            case UnaryExpr u:
                return EvaluateUnary(u);
            case CallExpr c:
                return EvaluateCall(c);
            case GetExpr g:
                return EvaluateGet(g);
            case SetExpr s:
                return EvaluateSet(s);
            case IndexExpr i:
                return EvaluateIndex(i);
            case SetIndexExpr si:
                return EvaluateSetIndex(si);
            case ArrayExpr a:
                var list = new List<object?>();
                foreach (var el in a.Elements) list.Add(Evaluate(el));
                return list;
            case DictExpr d:
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < d.Keys.Count; i++)
                {
                    dict[d.Keys[i]] = Evaluate(d.Values[i]);
                }
                return dict;
            default:
                ErrorReporter.Runtime("Unknown expression.");
                return null;
        }
    }

    private object? EvaluateBinary(BinaryExpr b)
    {
        var left = Evaluate(b.Left);
        var right = Evaluate(b.Right);

        switch (b.Operator.Type)
        {
            case TokenType.PLUS:
                if (left is string || right is string) return Stringify(left) + Stringify(right);
                return ToNumber(left, "Addition requires numbers.") + ToNumber(right, "Addition requires numbers.");
            case TokenType.MINUS:
                return ToNumber(left, "Subtraction requires numbers.") - ToNumber(right, "Subtraction requires numbers.");
            case TokenType.STAR:
                return ToNumber(left, "Multiplication requires numbers.") * ToNumber(right, "Multiplication requires numbers.");
            case TokenType.SLASH:
                return ToNumber(left, "Division requires numbers.") / ToNumber(right, "Division requires numbers.");
            case TokenType.PERCENT:
                return ToNumber(left, "Remainder requires numbers.") % ToNumber(right, "Remainder requires numbers.");
            case TokenType.EQUAL_EQUAL:
                return IsEqual(left, right);
            case TokenType.BANG_EQUAL:
                return !IsEqual(left, right);
            case TokenType.GREATER:
                return ToNumber(left, "Comparison requires numbers.") > ToNumber(right, "Comparison requires numbers.");
            case TokenType.GREATER_EQUAL:
                return ToNumber(left, "Comparison requires numbers.") >= ToNumber(right, "Comparison requires numbers.");
            case TokenType.LESS:
                return ToNumber(left, "Comparison requires numbers.") < ToNumber(right, "Comparison requires numbers.");
            case TokenType.LESS_EQUAL:
                return ToNumber(left, "Comparison requires numbers.") <= ToNumber(right, "Comparison requires numbers.");
            case TokenType.AND:
                return IsTruthy(left) && IsTruthy(right);
            case TokenType.OR:
                return IsTruthy(left) || IsTruthy(right);
            default:
                ErrorReporter.Runtime("Unknown operator.");
                return null;
        }
    }

    private object? EvaluateUnary(UnaryExpr u)
    {
        var right = Evaluate(u.Right);
        return u.Operator.Type switch
        {
            TokenType.MINUS => -ToNumber(right, "Negation requires a number."),
            TokenType.BANG => !IsTruthy(right),
            _ => null
        };
    }

    private object? EvaluateCall(CallExpr c)
    {
        var callee = Evaluate(c.Callee);
        if (callee is not IBabaCallable callable)
        {
            ErrorReporter.Runtime("Target is not callable.");
            return null;
        }

        var args = new List<object?>();
        foreach (var arg in c.Arguments) args.Add(Evaluate(arg));

        if (args.Count != callable.Arity && callable.Arity != -1)
        {
            ErrorReporter.Runtime($"Function expects {callable.Arity} argument(s), got {args.Count}.");
        }

        return callable.Call(this, args);
    }

    private object? EvaluateGet(GetExpr g)
    {
        var obj = Evaluate(g.Object);
        if (obj is Dictionary<string, object?> dict)
        {
            if (dict.TryGetValue(g.Name, out var value)) return value;
            ErrorReporter.Runtime($"Property not found: {g.Name}");
        }
        ErrorReporter.Runtime("Property access is only supported on maps.");
        return null;
    }

    private object? EvaluateSet(SetExpr s)
    {
        var obj = Evaluate(s.Object);
        if (obj is Dictionary<string, object?> dict)
        {
            var value = Evaluate(s.Value);
            dict[s.Name] = value;
            return value;
        }
        ErrorReporter.Runtime("Property assignment is only supported on maps.");
        return null;
    }

    private object? EvaluateIndex(IndexExpr i)
    {
        var obj = Evaluate(i.Object);
        var index = Evaluate(i.Index);

        if (obj is List<object?> list)
        {
            var idx = (int)ToNumber(index, "Index must be a number.");
            if (idx < 0 || idx >= list.Count) ErrorReporter.Runtime("Index out of range.");
            return list[idx];
        }
        if (obj is Dictionary<string, object?> dict)
        {
            var key = Stringify(index);
            if (dict.TryGetValue(key, out var value)) return value;
            ErrorReporter.Runtime($"Key not found: {key}");
        }

        ErrorReporter.Runtime("Indexing is only supported on arrays and maps.");
        return null;
    }

    private object? EvaluateSetIndex(SetIndexExpr s)
    {
        var obj = Evaluate(s.Object);
        var index = Evaluate(s.Index);
        var value = Evaluate(s.Value);

        if (obj is List<object?> list)
        {
            var idx = (int)ToNumber(index, "Index must be a number.");
            if (idx < 0 || idx >= list.Count) ErrorReporter.Runtime("Index out of range.");
            list[idx] = value;
            return value;
        }
        if (obj is Dictionary<string, object?> dict)
        {
            var key = Stringify(index);
            dict[key] = value;
            return value;
        }

        ErrorReporter.Runtime("Index assignment is only supported on arrays and maps.");
        return null;
    }

    private static bool IsTruthy(object? value)
    {
        if (value == null) return false;
        if (value is bool b) return b;
        return true;
    }

    private static bool IsEqual(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null) return false;
        return a.Equals(b);
    }

    private static double ToNumber(object? value, string message)
    {
        if (value is double d) return d;
        if (value is int i) return i;
        if (value is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var res)) return res;
        ErrorReporter.Runtime(message);
        return 0;
    }

    private static string Stringify(object? value)
    {
        if (value == null) return "null";
        if (value is double d) return d.ToString(CultureInfo.InvariantCulture);
        if (value is bool b) return b ? "true" : "false";
        if (value is List<object?> list)
        {
            var parts = new List<string>();
            foreach (var item in list) parts.Add(Stringify(item));
            return "[" + string.Join(", ", parts) + "]";
        }
        if (value is Dictionary<string, object?> dict)
        {
            var parts = new List<string>();
            foreach (var kv in dict)
            {
                parts.Add($"\"{kv.Key}\": {Stringify(kv.Value)}");
            }
            return "{" + string.Join(", ", parts) + "}";
        }
        return value.ToString() ?? "";
    }
}
