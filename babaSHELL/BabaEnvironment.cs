using System;
using System.Collections.Generic;

namespace BabaShell;

public sealed class BabaEnvironment
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

    public BabaEnvironment(BabaEnvironment? parent = null)
    {
        Parent = parent;
    }

    public BabaEnvironment? Parent { get; }

    public void Define(string name, object? value)
    {
        _values[name] = value;
    }

    public void Assign(string name, object? value)
    {
        if (_values.ContainsKey(name))
        {
            _values[name] = value;
            return;
        }

        if (Parent != null)
        {
            Parent.Assign(name, value);
            return;
        }

        _values[name] = value;
    }

    public object? Get(string name)
    {
        if (_values.TryGetValue(name, out var value)) return value;
        if (Parent != null) return Parent.Get(name);
        ErrorReporter.Runtime($"Undefined variable: {name}");
        return null;
    }

    public Dictionary<string, object?> Snapshot()
    {
        return new Dictionary<string, object?>(_values, StringComparer.OrdinalIgnoreCase);
    }
}

public interface IBabaCallable
{
    int Arity { get; }
    object? Call(Interpreter interpreter, List<object?> arguments);
}

public sealed class BabaFunction : IBabaCallable
{
    private readonly FuncStmt _declaration;
    private readonly BabaEnvironment _closure;

    public BabaFunction(FuncStmt declaration, BabaEnvironment closure)
    {
        _declaration = declaration;
        _closure = closure;
    }

    public int Arity => _declaration.Params.Count;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        var environment = new BabaEnvironment(_closure);
        for (var i = 0; i < _declaration.Params.Count; i++)
        {
            environment.Define(_declaration.Params[i], arguments[i]);
        }

        try
        {
            interpreter.ExecuteBlock(_declaration.Body.Statements, environment);
        }
        catch (ReturnSignal r)
        {
            return r.Value;
        }

        return null;
    }

    public override string ToString() => $"<func {_declaration.Name}>";
}

public sealed class BuiltinFunction : IBabaCallable
{
    private readonly Func<List<object?>, object?> _func;
    public BuiltinFunction(int arity, Func<List<object?>, object?> func)
    {
        Arity = arity;
        _func = func;
    }

    public int Arity { get; }

    public object? Call(Interpreter interpreter, List<object?> arguments) => _func(arguments);

    public override string ToString() => "<builtin>";
}

public sealed class ReturnSignal : Exception
{
    public ReturnSignal(object? value) => Value = value;
    public object? Value { get; }
}

public sealed class BreakSignal : Exception { }

public sealed class ContinueSignal : Exception { }
