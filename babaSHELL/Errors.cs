using System;

namespace BabaShell;

public sealed class BabaError : Exception
{
    public BabaError(string message) : base(message) { }
}

public static class ErrorReporter
{
    public static void Syntax(string message, int line, int column)
    {
        WithColor(ConsoleColor.Red, () =>
        {
            Console.WriteLine($"Syntax error ({line}:{column}): {message}");
        });
        throw new BabaError(message);
    }

    public static void Runtime(string message)
    {
        WithColor(ConsoleColor.Red, () =>
        {
            Console.WriteLine($"Runtime error: {message}");
        });
        throw new BabaError(message);
    }

    public static void Warning(string message)
    {
        WithColor(ConsoleColor.Yellow, () =>
        {
            Console.WriteLine($"Warning: {message}");
        });
    }

    private static void WithColor(ConsoleColor color, Action action)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        action();
        Console.ForegroundColor = prev;
    }
}
