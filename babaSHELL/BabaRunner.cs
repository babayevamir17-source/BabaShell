using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace BabaShell;

public sealed class BabaRunner
{
    private readonly Interpreter _interpreter = new();
    private const string BannerTop = "BabaShell";
    private static readonly string[] BannerLines =
    {
        "██████╗  █████╗ ██████╗  █████╗ ███████╗██╗  ██╗███████╗██╗     ██╗     ",
        "██╔══██╗██╔══██╗██╔══██╗██╔══██╗██╔════╝██║  ██║██╔════╝██║     ██║     ",
        "██████╔╝███████║██████╔╝███████║███████╗███████║█████╗  ██║     ██║     ",
        "██╔══██╗██╔══██║██╔══██╗██╔══██║╚════██║██╔══██║██╔══╝  ██║     ██║     ",
        "██████╔╝██║  ██║██████╔╝██║  ██║███████║██║  ██║███████╗███████╗███████╗",
        "╚═════╝ ╚═╝  ╚═╝╚═════╝ ╚═╝  ╚═╝╚══════╝╚═╝  ╚═╝╚══════╝╚══════╝╚══════╝"
    };

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
        catch (ThrowSignal thrown)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Error] Uncaught throw: {thrown.Value}");
            Console.ForegroundColor = prev;
            return 1;
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
        PrintBanner();
        WritePrompt("Interactive REPL ready. Exit: exit / quit / :q");
        WriteHint("Type 'help' to see commands, builtins, and examples.");
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
        source = BabaScriptPreprocessor.StripWebOnlyLines(stripped);
        var lexer = new Lexer(source);
        var tokens = lexer.ScanTokens();
        var parser = new Parser(tokens);
        var statements = parser.Parse();
        new SemanticAnalyzer().Analyze(statements);
    }

    private static void WritePrompt(string text, bool isInline = false)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        if (isInline) Console.Write(text);
        else Console.WriteLine(text);
        Console.ForegroundColor = prev;
    }

    private static void WriteAccent(string text, ConsoleColor color, bool isInline = false)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        if (isInline) Console.Write(text);
        else Console.WriteLine(text);
        Console.ForegroundColor = prev;
    }

    private static void WriteHint(string text)
    {
        WriteAccent(text, ConsoleColor.DarkGray);
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
        PrintBanner();
        WriteSection("Usage");
        Console.WriteLine("  babashell run <file.babashell>");
        Console.WriteLine("  babashell <file.babashell>");
        Console.WriteLine("  babashell --check <file.babashell>");
        Console.WriteLine("  babashell serve <file.babashell> [port]");
        Console.WriteLine("  babashell export <file.babashell> [out.html]");
        Console.WriteLine("  babashell --version");
        Console.WriteLine();

        WriteSection("Workflow");
        Console.WriteLine("  1. Build logic in a .babashell file");
        Console.WriteLine("  2. Bind HTML/CSS with 'use from {./page.html}' and 'use from {./theme.css}'");
        Console.WriteLine("  3. Run locally with 'babashell serve app.babashell 3000'");
        Console.WriteLine("  4. Export a self-contained page with 'babashell export app.babashell out.html'");
        Console.WriteLine();

        WriteSection("Core Language");
        Console.WriteLine("  store, export, import, if, else if, else, try, catch, throw");
        Console.WriteLine("  while, break, continue, repeat, times, for, in");
        Console.WriteLine("  func, return, class, new, this");
        Console.WriteLine("  wait, fetch, as, emit, set, when");
        Console.WriteLine("  true, false, null, and, or, map");
        Console.WriteLine();

        WriteSection("Builtins");
        Console.WriteLine("  help, print, input, confirm, ask_number, choose, clear");
        Console.WriteLine("  random, length, size, type_of, parse_number, to_string");
        Console.WriteLine("  lower, upper, trim, contains, starts_with, ends_with, replace");
        Console.WriteLine("  split, join, slice, regex_is_match");
        Console.WriteLine("  file_read, file_write, file_append, file_exists, file_delete");
        Console.WriteLine("  dir_exists, dir_make, dir_delete, dir_list");
        Console.WriteLine("  now, unix_time, format_time");
        Console.WriteLine("  math.*, str.*, arr.*, obj.*, json.*, net.*, bot.*, crypto.*");
        Console.WriteLine();

        WriteSection("Examples");
        Console.WriteLine("  store score = 0");
        Console.WriteLine("  score += 1");
        Console.WriteLine("  if score > 10 { emit \"win\" } else { emit \"lose\" }");
        Console.WriteLine("  when #btn clicked { set #out text \"clicked\" }");
        Console.WriteLine("  use from {./index.html}");
        Console.WriteLine("  use from {./style.css}");
        Console.WriteLine("  style.#btn.background-color: #2563eb");
        Console.WriteLine("  class Person { func init(name) { this.name = name } }");
        Console.WriteLine("  export func add(a, b) { return a + b }");
        Console.WriteLine("  import mathlib");
        Console.WriteLine();

        WriteHint("Semantic analysis runs before execution. '--check' validates syntax + scope rules.");
    }

    private static void PrintBanner()
    {
        var border = new string('═', 78);
        WriteAccent($"╔{border}╗", ConsoleColor.DarkCyan);
        for (var i = 0; i < BannerLines.Length; i++)
        {
            var color = i switch
            {
                0 => ConsoleColor.Cyan,
                1 => ConsoleColor.Cyan,
                2 => ConsoleColor.Blue,
                3 => ConsoleColor.Blue,
                4 => ConsoleColor.Magenta,
                _ => ConsoleColor.DarkMagenta
            };

            WriteAccent("║ ", ConsoleColor.DarkCyan, isInline: true);
            WriteAccent(BannerLines[i].PadRight(76), color, isInline: true);
            WriteAccent(" ║", ConsoleColor.DarkCyan);
        }

        WriteAccent($"╠{border}╣", ConsoleColor.DarkCyan);
        WriteAccent("║ ", ConsoleColor.DarkCyan, isInline: true);
        WriteAccent($" {BannerTop} v{BabaUpdater.CurrentVersion} ".PadRight(24), ConsoleColor.White, isInline: true);
        WriteAccent("Language runtime, web scripting, modules, and semantic analysis".PadRight(52), ConsoleColor.Gray, isInline: true);
        WriteAccent(" ║", ConsoleColor.DarkCyan);
        WriteAccent("║ ", ConsoleColor.DarkCyan, isInline: true);
        WriteAccent(" Commands ".PadRight(24), ConsoleColor.Yellow, isInline: true);
        WriteAccent("run  serve  export  --check  --version  help".PadRight(52), ConsoleColor.Gray, isInline: true);
        WriteAccent(" ║", ConsoleColor.DarkCyan);
        WriteAccent("║ ", ConsoleColor.DarkCyan, isInline: true);
        WriteAccent(" Quick start ".PadRight(24), ConsoleColor.Green, isInline: true);
        WriteAccent("use from {./index.html}  ->  when #btn clicked { ... }".PadRight(52), ConsoleColor.Gray, isInline: true);
        WriteAccent(" ║", ConsoleColor.DarkCyan);
        WriteAccent($"╚{border}╝", ConsoleColor.DarkCyan);
        Console.WriteLine();
    }

    private static void WriteSection(string title)
    {
        WriteAccent(title, ConsoleColor.Yellow);
    }
}
