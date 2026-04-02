using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace BabaShell;

public static class Builtins
{
    public static void Register(BabaEnvironment env)
    {
        env.Define("help", new BuiltinFunction(0, _ =>
        {
            Console.WriteLine("BabaShell");
            Console.WriteLine("Usage: babashell [file.babashell]");
            Console.WriteLine();
            Console.WriteLine("Keywords:");
            Console.WriteLine("  emit, when, else, loop, func, return, import, true, false, null, and, or, map");
            Console.WriteLine();
            Console.WriteLine("Builtins:");
            Console.WriteLine("  help");
            Console.WriteLine("  red, green, yellow, blue");
            Console.WriteLine("  read, size, lower, upper, trim, contains, split, join, slice");
            Console.WriteLine("  file_read, file_write, file_append, file_exists, file_delete, file_copy, file_move");
            Console.WriteLine("  dir_exists, dir_make, dir_delete, dir_list");
            Console.WriteLine("  now, unix_time, format_time");
            return null;
        }));

        env.Define("read", new BuiltinFunction(1, args =>
        {
            Console.Write(args[0]?.ToString() ?? "");
            return Console.ReadLine() ?? "";
        }));

        env.Define("size", new BuiltinFunction(1, args =>
        {
            if (args[0] is string s) return (double)s.Length;
            if (args[0] is List<object?> list) return (double)list.Count;
            if (args[0] is Dictionary<string, object?> dict) return (double)dict.Count;
            return 0.0;
        }));

        env.Define("lower", new BuiltinFunction(1, args =>
        {
            return (args[0]?.ToString() ?? "").ToLowerInvariant();
        }));

        env.Define("upper", new BuiltinFunction(1, args =>
        {
            return (args[0]?.ToString() ?? "").ToUpperInvariant();
        }));

        env.Define("trim", new BuiltinFunction(1, args =>
        {
            return (args[0]?.ToString() ?? "").Trim();
        }));

        env.Define("contains", new BuiltinFunction(2, args =>
        {
            var s = args[0]?.ToString() ?? "";
            var sub = args[1]?.ToString() ?? "";
            return s.Contains(sub, StringComparison.OrdinalIgnoreCase);
        }));

        env.Define("split", new BuiltinFunction(2, args =>
        {
            var s = args[0]?.ToString() ?? "";
            var sep = args[1]?.ToString() ?? "";
            var parts = s.Split(new[] { sep }, StringSplitOptions.None);
            var list = new List<object?>();
            foreach (var p in parts) list.Add(p);
            return list;
        }));

        env.Define("join", new BuiltinFunction(2, args =>
        {
            var list = args[0] as List<object?> ?? new List<object?>();
            var sep = args[1]?.ToString() ?? "";
            var parts = new List<string>();
            foreach (var item in list) parts.Add(item?.ToString() ?? "");
            return string.Join(sep, parts);
        }));

        env.Define("slice", new BuiltinFunction(-1, args =>
        {
            if (args.Count < 2 || args.Count > 3) return "";
            var s = args[0]?.ToString() ?? "";
            var start = (int)ToNumber(args[1]);
            var len = args.Count == 3 ? (int)ToNumber(args[2]) : s.Length - start;
            if (start < 0) start = 0;
            if (start > s.Length) return "";
            if (len < 0) return "";
            if (start + len > s.Length) len = s.Length - start;
            return s.Substring(start, len);
        }));

        env.Define("red", new BuiltinFunction(1, args =>
        {
            WriteColor(ConsoleColor.Red, args[0]);
            return null;
        }));

        env.Define("green", new BuiltinFunction(1, args =>
        {
            WriteColor(ConsoleColor.Green, args[0]);
            return null;
        }));

        env.Define("yellow", new BuiltinFunction(1, args =>
        {
            WriteColor(ConsoleColor.Yellow, args[0]);
            return null;
        }));

        env.Define("blue", new BuiltinFunction(1, args =>
        {
            WriteColor(ConsoleColor.Cyan, args[0]);
            return null;
        }));

        env.Define("file_read", new BuiltinFunction(1, args =>
        {
            var path = args[0]?.ToString() ?? "";
            return File.ReadAllText(path);
        }));

        env.Define("file_write", new BuiltinFunction(2, args =>
        {
            var path = args[0]?.ToString() ?? "";
            var text = args[1]?.ToString() ?? "";
            File.WriteAllText(path, text);
            return null;
        }));

        env.Define("file_append", new BuiltinFunction(2, args =>
        {
            var path = args[0]?.ToString() ?? "";
            var text = args[1]?.ToString() ?? "";
            File.AppendAllText(path, text);
            return null;
        }));

        env.Define("file_exists", new BuiltinFunction(1, args =>
        {
            var path = args[0]?.ToString() ?? "";
            return File.Exists(path);
        }));

        env.Define("file_delete", new BuiltinFunction(1, args =>
        {
            var path = args[0]?.ToString() ?? "";
            if (File.Exists(path)) File.Delete(path);
            return null;
        }));

        env.Define("file_copy", new BuiltinFunction(2, args =>
        {
            var from = args[0]?.ToString() ?? "";
            var to = args[1]?.ToString() ?? "";
            File.Copy(from, to, true);
            return null;
        }));

        env.Define("file_move", new BuiltinFunction(2, args =>
        {
            var from = args[0]?.ToString() ?? "";
            var to = args[1]?.ToString() ?? "";
            if (File.Exists(to)) File.Delete(to);
            File.Move(from, to);
            return null;
        }));

        env.Define("dir_exists", new BuiltinFunction(1, args =>
        {
            var path = args[0]?.ToString() ?? "";
            return Directory.Exists(path);
        }));

        env.Define("dir_make", new BuiltinFunction(1, args =>
        {
            var path = args[0]?.ToString() ?? "";
            Directory.CreateDirectory(path);
            return null;
        }));

        env.Define("dir_delete", new BuiltinFunction(-1, args =>
        {
            if (args.Count == 0) return null;
            var path = args[0]?.ToString() ?? "";
            var recursive = args.Count > 1 && args[1] is bool b && b;
            if (Directory.Exists(path)) Directory.Delete(path, recursive);
            return null;
        }));

        env.Define("dir_list", new BuiltinFunction(1, args =>
        {
            var path = args[0]?.ToString() ?? "";
            var list = new List<object?>();
            if (Directory.Exists(path))
            {
                foreach (var item in Directory.GetFileSystemEntries(path))
                {
                    list.Add(item);
                }
            }
            return list;
        }));

        env.Define("now", new BuiltinFunction(0, _ => DateTime.Now.ToString("o", CultureInfo.InvariantCulture)));

        env.Define("unix_time", new BuiltinFunction(0, _ =>
        {
            var unix = DateTimeOffset.Now.ToUnixTimeSeconds();
            return (double)unix;
        }));

        env.Define("format_time", new BuiltinFunction(2, args =>
        {
            var input = args[0]?.ToString() ?? "";
            var format = args[1]?.ToString() ?? "o";
            if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            {
                return dt.ToString(format, CultureInfo.InvariantCulture);
            }
            return input;
        }));
    }

    public static Dictionary<string, object?> CreateMathModule()
    {
        var module = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["pi"] = Math.PI,
            ["sqrt"] = new BuiltinFunction(1, args => Math.Sqrt(ToNumber(args[0]))),
            ["pow"] = new BuiltinFunction(2, args => Math.Pow(ToNumber(args[0]), ToNumber(args[1]))),
            ["abs"] = new BuiltinFunction(1, args => Math.Abs(ToNumber(args[0]))),
            ["sin"] = new BuiltinFunction(1, args => Math.Sin(ToNumber(args[0]))),
            ["cos"] = new BuiltinFunction(1, args => Math.Cos(ToNumber(args[0]))),
            ["tan"] = new BuiltinFunction(1, args => Math.Tan(ToNumber(args[0]))),
            ["ceil"] = new BuiltinFunction(1, args => Math.Ceiling(ToNumber(args[0]))),
            ["floor"] = new BuiltinFunction(1, args => Math.Floor(ToNumber(args[0]))),
            ["round"] = new BuiltinFunction(1, args => Math.Round(ToNumber(args[0]))),
            ["random"] = new BuiltinFunction(0, _ => new Random().NextDouble()),
        };

        return module;
    }

    private static void WriteColor(ConsoleColor color, object? value)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(value?.ToString() ?? "");
        Console.ForegroundColor = prev;
    }

    private static double ToNumber(object? value)
    {
        if (value is double d) return d;
        if (value is int i) return i;
        if (value is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var res)) return res;
        return 0;
    }
}
