using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace BabaShell;

public sealed class BabaRunner
{
    private readonly Interpreter _interpreter = new();

    public int Run(string[] args)
    {
        try
        {
            if (args.Length > 0 && (args[0] == "--update" || args[0] == "-u"))
            {
                return BabaUpdater.RunAsync().GetAwaiter().GetResult();
            }
            if (args.Length > 0 && (args[0] == "--version" || args[0] == "-v"))
            {
                Console.WriteLine($"BabaShell {BabaUpdater.CurrentVersion}");
                return 0;
            }
            if (args.Length > 0 && (args[0] == "serve" || args[0] == "dev"))
            {
                if (args.Length < 2)
                {
                    ErrorReporter.Runtime("Usage: babashell serve <file.babashell> [port]");
                    return 1;
                }
                int? port = null;
                if (args.Length >= 3 && int.TryParse(args[2], out var p)) port = p;
                return BabaServer.Serve(args[1], port);
            }
            if (args.Length > 0 && (args[0] == "export" || args[0] == "build"))
            {
                if (args.Length < 2)
                {
                    ErrorReporter.Runtime("Usage: babashell export <file.babashell> [out.html]");
                    return 1;
                }
                var outPath = args.Length >= 3 ? args[2] : null;
                return BabaExporter.Export(args[1], outPath);
            }
            if (args.Length > 0 && (args[0] == "--check" || args[0] == "-c"))
            {
                var checkPath = args.Length > 1 ? args[1] : null;
                if (string.IsNullOrWhiteSpace(checkPath))
                {
                    ErrorReporter.Runtime("No input file for --check.");
                    return 1;
                }
                if (!File.Exists(checkPath))
                {
                    ErrorReporter.Runtime($"File not found: {checkPath}");
                    return 1;
                }
                var checkAbsPath = Path.GetFullPath(checkPath);
                var checkSource = File.ReadAllText(checkAbsPath);
                ParseOnly(checkAbsPath, checkSource);
                return 0;
            }

            string? path = null;
            if (args.Length > 0)
            {
                if (string.Equals(args[0], "run", StringComparison.OrdinalIgnoreCase))
                {
                    if (args.Length < 2)
                    {
                        ErrorReporter.Runtime("Usage: babashell run <file.babashell>");
                        return 1;
                    }
                    path = args[1];
                }
                else
                {
                if (IsHelpArg(args[0]))
                {
                    PrintHelp();
                    return 0;
                }

                path = args[0];
                }
            }
            else
            {
                var candidate = Path.Combine(Directory.GetCurrentDirectory(), "main.babashell");
                if (File.Exists(candidate))
                {
                    path = candidate;
                }
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                RunRepl();
                return 0;
            }

            if (!File.Exists(path))
            {
                ErrorReporter.Runtime($"File not found: {path}");
                return 1;
            }

            var absPath = Path.GetFullPath(path);
            ErrorReporter.SetFileContext(absPath);
            var source = File.ReadAllText(absPath);
            _interpreter.ExecuteFile(absPath, source);
            return 0;
        }
        catch (BabaError)
        {
            return 1;
        }
        catch (Exception ex)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Error] Unexpected error: {ex.Message}");
            Console.ForegroundColor = prev;
            return 1;
        }
    }

    private void RunRepl()
    {
        WritePrompt("BabaShell REPL. Exit: exit / quit / :q");
        var buffer = "";
        while (true)
        {
            var prompt = string.IsNullOrWhiteSpace(buffer) ? "babashell> " : "....> ";
            WritePrompt(prompt, isInline: true);
            var line = Console.ReadLine();
            if (line == null) break;

            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(buffer))
            {
                if (IsHelpArg(trimmed) || trimmed.Equals("help", StringComparison.OrdinalIgnoreCase) || trimmed == "?")
                {
                    PrintHelp();
                    continue;
                }

                if (trimmed.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals(":q", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }

            buffer += line + "\n";
            if (!IsBalanced(buffer))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(buffer))
            {
                continue;
            }

            try
            {
                _interpreter.ExecuteSource("<repl>", buffer);
            }
            catch (BabaError)
            {
                // keep running after errors
            }
            buffer = "";
        }
    }

    private static void ParseOnly(string path, string source)
    {
        ErrorReporter.SetFileContext(path);
        var baseDir = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
        var (_, stripped) = BabaHtmlDirective.Parse(source, baseDir);
        source = stripped;
        var lexer = new Lexer(source);
        var tokens = lexer.ScanTokens();
        var parser = new Parser(tokens);
        _ = parser.Parse();
    }

    private static void WritePrompt(string text, bool isInline = false)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        if (isInline) Console.Write(text);
        else Console.WriteLine(text);
        Console.ForegroundColor = prev;
    }

    private static bool IsBalanced(string source)
    {
        var depth = 0;
        var inString = false;
        var escape = false;
        foreach (var ch in source)
        {
            if (inString)
            {
                if (escape)
                {
                    escape = false;
                }
                else if (ch == '\\')
                {
                    escape = true;
                }
                else if (ch == '"')
                {
                    inString = false;
                }
                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == '{') depth++;
            if (ch == '}') depth--;
        }
        return depth <= 0 && !inString;
    }

    private static bool IsHelpArg(string value)
    {
        return value.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("/?", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("help", StringComparison.OrdinalIgnoreCase) ||
               value.Equals(":h", StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("BabaShell");
        Console.WriteLine("Usage: babashell run [file.babashell|file.baba]");
        Console.WriteLine("       babashell [file.babashell|file.baba]");
        Console.WriteLine("       babashell --check file.babashell");
        Console.WriteLine("       babashell serve file.babashell [port]");
        Console.WriteLine("       babashell export file.babashell [out.html]");
        Console.WriteLine();
        Console.WriteLine("Web:");
        Console.WriteLine("  use from ./page.html   // at top of .babashell to bind custom HTML");
        Console.WriteLine();
        Console.WriteLine("Version:");
        Console.WriteLine("  babashell --version");
        Console.WriteLine();
        Console.WriteLine("Keywords:");
        Console.WriteLine("  store, increase, decrease, by, if, when, else, repeat, times, for, in");
        Console.WriteLine("  func, call, return, wait, fetch, as, emit, set, import, true, false, null, and, or, map");
        Console.WriteLine();
        Console.WriteLine("Builtins:");
        Console.WriteLine("  help, print, random, length");
        Console.WriteLine("  red, green, yellow, blue");
        Console.WriteLine("  read, size, lower, upper, trim, contains, split, join, slice");
        Console.WriteLine("  file_read, file_write, file_append, file_exists, file_delete, file_copy, file_move");
        Console.WriteLine("  dir_exists, dir_make, dir_delete, dir_list");
        Console.WriteLine("  now, unix_time, format_time");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  store score = 0");
        Console.WriteLine("  increase score by 1");
        Console.WriteLine("  if score > 10 { emit \"win\" } else { emit \"lose\" }");
        Console.WriteLine("  repeat 3 times { emit \"tick\" }");
        Console.WriteLine("  for item in [1, 2, 3] { emit item }");
        Console.WriteLine("  wait 2s { emit \"done\" }");
        Console.WriteLine("  fetch \"https://api.github.com\" as data { emit data.current_user_url }");
        Console.WriteLine("  emit \"hello\"");
        Console.WriteLine("  when x > 3 { emit x } else { emit 0 }");
        Console.WriteLine("  loop i = 1..3 { emit i }");
        Console.WriteLine("  func add(a, b) { return a + b }");
        Console.WriteLine("  map { \"a\": 1, \"b\": 2 }");
    }
}
