using System;
using System.Threading;

namespace BabaShell;

public sealed class BabaError : Exception
{
    public BabaError(string message) : base(message) { }
}

public static class ErrorReporter
{
    private static readonly AsyncLocal<string?> FileContext = new();

    public static void SetFileContext(string? filePath)
    {
        FileContext.Value = filePath;
    }

    public static void Syntax(string message, int line, int column)
    {
        WithColor(ConsoleColor.Red, () =>
        {
            var file = FileContext.Value ?? "<input>";
            Console.WriteLine($"[Error] {file}:{line}:{column} {message}");
        });
        throw new BabaError(message);
    }

    public static void Runtime(string message)
    {
        WithColor(ConsoleColor.Red, () =>
        {
            var file = FileContext.Value ?? "<runtime>";
            Console.WriteLine($"[Error] {file} {message}");
        });
        throw new BabaError(message);
    }

    public static void Warning(string message)
    {
        WithColor(ConsoleColor.Yellow, () =>
        {
            var file = FileContext.Value ?? "<runtime>";
            Console.WriteLine($"[Warning] {file} {message}");
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
