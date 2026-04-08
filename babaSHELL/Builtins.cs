using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BabaShell;

public static class Builtins
{
    private static readonly HttpClient Http = new();

    public static void Register(BabaEnvironment env)
    {
        RegisterCore(env);
        RegisterString(env);
        RegisterCollections(env);
        RegisterFilesystem(env);
        RegisterTime(env);
        RegisterNetwork(env);
        RegisterCrypto(env);
        RegisterColor(env);
        RegisterModules(env);
    }

    private static void RegisterCore(BabaEnvironment env)
    {
        env.Define("help", new BuiltinFunction(0, _ =>
        {
            Console.WriteLine("BabaShell v1");
            Console.WriteLine("Usage: babashell run file.babashell");
            Console.WriteLine("Web: babashell serve file.babashell [port]");
            Console.WriteLine();
            Console.WriteLine("Keywords:");
            Console.WriteLine("  store, increase, decrease, by, if, else, while, break, continue, when, repeat, times, for, in");
            Console.WriteLine("  func, call, return, wait, fetch, as, emit, set, import, true, false, null, and, or, map");
            Console.WriteLine();
            Console.WriteLine("Builtin groups:");
            Console.WriteLine("  Core: print, input, confirm, ask_number, choose, clear, random, length, type_of, parse_number, to_string");
            Console.WriteLine("  String: lower, upper, trim, contains, split, join, slice, replace, starts_with, ends_with");
            Console.WriteLine("  Collections: push, pop, shift, unshift, keys, values, has_key");
            Console.WriteLine("  File/Dir: file_*, dir_*");
            Console.WriteLine("  Network/Bot: http_get, http_post_json, discord_webhook_send");
            Console.WriteLine("  Crypto: hash_sha256, hash_md5, base64_encode, base64_decode");
            return null;
        }));

        env.Define("print", new BuiltinFunction(-1, args =>
        {
            Console.WriteLine(string.Join(" ", args.Select(Stringify)));
            return null;
        }));

        env.Define("read", new BuiltinFunction(1, args =>
        {
            Console.Write(args[0]?.ToString() ?? "");
            return Console.ReadLine() ?? "";
        }));

        env.Define("input", new BuiltinFunction(-1, args =>
        {
            var prompt = args.Count > 0 ? args[0]?.ToString() ?? "" : "";
            if (!string.IsNullOrEmpty(prompt))
            {
                Console.Write(prompt);
            }
            return Console.ReadLine() ?? "";
        }));

        env.Define("confirm", new BuiltinFunction(1, args =>
        {
            var prompt = args[0]?.ToString() ?? "Continue?";
            Console.Write($"{prompt} [y/N] ");
            var answer = (Console.ReadLine() ?? "").Trim();
            return answer.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                   answer.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }));

        env.Define("ask_number", new BuiltinFunction(-1, args =>
        {
            var prompt = args.Count > 0 ? args[0]?.ToString() ?? "Enter a number:" : "Enter a number:";
            var fallback = args.Count > 1 ? ToNumber(args[1]) : 0;
            Console.Write($"{prompt} ");
            var raw = Console.ReadLine();
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
            return fallback;
        }));

        env.Define("choose", new BuiltinFunction(-1, args =>
        {
            if (args.Count < 2)
            {
                ErrorReporter.Runtime("choose requires a prompt and at least one option.");
            }

            var prompt = args[0]?.ToString() ?? "Choose:";
            Console.WriteLine(prompt);
            for (var i = 1; i < args.Count; i++)
            {
                Console.WriteLine($"  {i}. {Stringify(args[i])}");
            }
            Console.Write("> ");
            var raw = Console.ReadLine();
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var selected))
            {
                if (selected >= 1 && selected < args.Count)
                {
                    return args[selected];
                }
            }
            return args[1];
        }));

        env.Define("clear", new BuiltinFunction(0, _ =>
        {
            Console.Clear();
            return null;
        }));

        env.Define("random", new BuiltinFunction(-1, args =>
        {
            var r = Random.Shared.NextDouble();
            if (args.Count == 0) return r;
            if (args.Count == 1) return r * ToNumber(args[0]);
            var min = ToNumber(args[0]);
            var max = ToNumber(args[1]);
            return min + (r * (max - min));
        }));

        env.Define("length", new BuiltinFunction(1, args => (double)CountOf(args[0])));
        env.Define("size", new BuiltinFunction(1, args => (double)CountOf(args[0])));

        env.Define("type_of", new BuiltinFunction(1, args =>
        {
            var v = args[0];
            return v switch
            {
                null => "null",
                string => "string",
                bool => "bool",
                double or int or long or float or decimal => "number",
                List<object?> => "array",
                Dictionary<string, object?> => "map",
                IBabaCallable => "function",
                _ => "object"
            };
        }));

        env.Define("parse_number", new BuiltinFunction(1, args => ToNumber(args[0])));
        env.Define("to_string", new BuiltinFunction(1, args => Stringify(args[0])));
    }

    private static void RegisterString(BabaEnvironment env)
    {
        env.Define("lower", new BuiltinFunction(1, args => (args[0]?.ToString() ?? "").ToLowerInvariant()));
        env.Define("upper", new BuiltinFunction(1, args => (args[0]?.ToString() ?? "").ToUpperInvariant()));
        env.Define("trim", new BuiltinFunction(1, args => (args[0]?.ToString() ?? "").Trim()));
        env.Define("contains", new BuiltinFunction(2, args => (args[0]?.ToString() ?? "").Contains(args[1]?.ToString() ?? "", StringComparison.OrdinalIgnoreCase)));
        env.Define("starts_with", new BuiltinFunction(2, args => (args[0]?.ToString() ?? "").StartsWith(args[1]?.ToString() ?? "", StringComparison.OrdinalIgnoreCase)));
        env.Define("ends_with", new BuiltinFunction(2, args => (args[0]?.ToString() ?? "").EndsWith(args[1]?.ToString() ?? "", StringComparison.OrdinalIgnoreCase)));
        env.Define("replace", new BuiltinFunction(3, args => (args[0]?.ToString() ?? "").Replace(args[1]?.ToString() ?? "", args[2]?.ToString() ?? "", StringComparison.OrdinalIgnoreCase)));

        env.Define("split", new BuiltinFunction(2, args =>
        {
            var input = args[0]?.ToString() ?? "";
            var sep = args[1]?.ToString() ?? "";
            var result = input.Split(new[] { sep }, StringSplitOptions.None);
            return result.Cast<object?>().ToList();
        }));

        env.Define("join", new BuiltinFunction(2, args =>
        {
            var list = ToList(args[0]);
            var sep = args[1]?.ToString() ?? "";
            return string.Join(sep, list.Select(Stringify));
        }));

        env.Define("slice", new BuiltinFunction(-1, args =>
        {
            var input = args.Count > 0 ? args[0]?.ToString() ?? "" : "";
            var start = args.Count > 1 ? (int)ToNumber(args[1]) : 0;
            var len = args.Count > 2 ? (int)ToNumber(args[2]) : input.Length - start;
            if (start < 0) start = 0;
            if (start > input.Length) return "";
            if (len < 0) return "";
            if (start + len > input.Length) len = input.Length - start;
            return input.Substring(start, len);
        }));

        env.Define("regex_is_match", new BuiltinFunction(2, args =>
        {
            var input = args[0]?.ToString() ?? "";
            var pattern = args[1]?.ToString() ?? "";
            return Regex.IsMatch(input, pattern);
        }));
    }

    private static void RegisterCollections(BabaEnvironment env)
    {
        env.Define("push", new BuiltinFunction(2, args =>
        {
            var list = ToList(args[0]);
            list.Add(args[1]);
            return (double)list.Count;
        }));

        env.Define("pop", new BuiltinFunction(1, args =>
        {
            var list = ToList(args[0]);
            if (list.Count == 0) return null;
            var idx = list.Count - 1;
            var value = list[idx];
            list.RemoveAt(idx);
            return value;
        }));

        env.Define("shift", new BuiltinFunction(1, args =>
        {
            var list = ToList(args[0]);
            if (list.Count == 0) return null;
            var value = list[0];
            list.RemoveAt(0);
            return value;
        }));

        env.Define("unshift", new BuiltinFunction(2, args =>
        {
            var list = ToList(args[0]);
            list.Insert(0, args[1]);
            return (double)list.Count;
        }));

        env.Define("keys", new BuiltinFunction(1, args =>
        {
            var map = ToMap(args[0]);
            return map.Keys.Cast<object?>().ToList();
        }));

        env.Define("values", new BuiltinFunction(1, args =>
        {
            var map = ToMap(args[0]);
            return map.Values.Cast<object?>().ToList();
        }));

        env.Define("has_key", new BuiltinFunction(2, args =>
        {
            var map = ToMap(args[0]);
            var key = args[1]?.ToString() ?? "";
            return map.ContainsKey(key);
        }));
    }

    private static void RegisterFilesystem(BabaEnvironment env)
    {
        env.Define("file_read", new BuiltinFunction(1, args => File.ReadAllText(args[0]?.ToString() ?? "")));
        env.Define("file_write", new BuiltinFunction(2, args =>
        {
            File.WriteAllText(args[0]?.ToString() ?? "", args[1]?.ToString() ?? "");
            return null;
        }));
        env.Define("file_append", new BuiltinFunction(2, args =>
        {
            File.AppendAllText(args[0]?.ToString() ?? "", args[1]?.ToString() ?? "");
            return null;
        }));
        env.Define("file_exists", new BuiltinFunction(1, args => File.Exists(args[0]?.ToString() ?? "")));
        env.Define("file_delete", new BuiltinFunction(1, args =>
        {
            var path = args[0]?.ToString() ?? "";
            if (File.Exists(path)) File.Delete(path);
            return null;
        }));
        env.Define("file_copy", new BuiltinFunction(2, args =>
        {
            File.Copy(args[0]?.ToString() ?? "", args[1]?.ToString() ?? "", true);
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

        env.Define("dir_exists", new BuiltinFunction(1, args => Directory.Exists(args[0]?.ToString() ?? "")));
        env.Define("dir_make", new BuiltinFunction(1, args =>
        {
            Directory.CreateDirectory(args[0]?.ToString() ?? "");
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
            if (!Directory.Exists(path)) return new List<object?>();
            return Directory.GetFileSystemEntries(path).Cast<object?>().ToList();
        }));
    }

    private static void RegisterTime(BabaEnvironment env)
    {
        env.Define("now", new BuiltinFunction(0, _ => DateTime.Now.ToString("o", CultureInfo.InvariantCulture)));
        env.Define("unix_time", new BuiltinFunction(0, _ => (double)DateTimeOffset.Now.ToUnixTimeSeconds()));
        env.Define("format_time", new BuiltinFunction(2, args =>
        {
            var input = args[0]?.ToString() ?? "";
            var fmt = args[1]?.ToString() ?? "o";
            if (!DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)) return input;
            return dt.ToString(fmt, CultureInfo.InvariantCulture);
        }));
    }

    private static void RegisterNetwork(BabaEnvironment env)
    {
        env.Define("http_get", new BuiltinFunction(1, args =>
        {
            try
            {
                var url = args[0]?.ToString() ?? "";
                var body = Http.GetStringAsync(url).GetAwaiter().GetResult();
                return ParseJsonOrText(body);
            }
            catch (Exception ex)
            {
                ErrorReporter.Warning($"http_get failed: {ex.Message}");
                return null;
            }
        }));

        env.Define("http_post_json", new BuiltinFunction(2, args =>
        {
            try
            {
                var url = args[0]?.ToString() ?? "";
                var payload = ToJson(args[1]);
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var res = Http.PostAsync(url, content).GetAwaiter().GetResult();
                var text = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return ParseJsonOrText(text);
            }
            catch (Exception ex)
            {
                ErrorReporter.Warning($"http_post_json failed: {ex.Message}");
                return null;
            }
        }));

        env.Define("json_parse", new BuiltinFunction(1, args =>
        {
            var raw = args[0]?.ToString() ?? "";
            try
            {
                using var doc = JsonDocument.Parse(raw);
                return JsonToObject(doc.RootElement);
            }
            catch
            {
                return null;
            }
        }));

        env.Define("json_stringify", new BuiltinFunction(1, args => ToJson(args[0])));

        env.Define("discord_webhook_send", new BuiltinFunction(-1, args =>
        {
            if (args.Count < 2) return false;
            var webhookUrl = args[0]?.ToString() ?? "";
            var contentText = args[1]?.ToString() ?? "";
            var username = args.Count > 2 ? args[2]?.ToString() : null;
            var avatarUrl = args.Count > 3 ? args[3]?.ToString() : null;

            var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["content"] = contentText
            };
            if (!string.IsNullOrWhiteSpace(username)) payload["username"] = username;
            if (!string.IsNullOrWhiteSpace(avatarUrl)) payload["avatar_url"] = avatarUrl;

            var json = JsonSerializer.Serialize(payload);
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, webhookUrl);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = Http.Send(req);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                ErrorReporter.Warning($"discord_webhook_send failed: {ex.Message}");
                return false;
            }
        }));
    }

    private static void RegisterCrypto(BabaEnvironment env)
    {
        env.Define("hash_sha256", new BuiltinFunction(1, args =>
        {
            var bytes = Encoding.UTF8.GetBytes(args[0]?.ToString() ?? "");
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }));

        env.Define("hash_md5", new BuiltinFunction(1, args =>
        {
            var bytes = Encoding.UTF8.GetBytes(args[0]?.ToString() ?? "");
            var hash = MD5.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }));

        env.Define("base64_encode", new BuiltinFunction(1, args =>
        {
            var bytes = Encoding.UTF8.GetBytes(args[0]?.ToString() ?? "");
            return Convert.ToBase64String(bytes);
        }));

        env.Define("base64_decode", new BuiltinFunction(1, args =>
        {
            var raw = args[0]?.ToString() ?? "";
            var bytes = Convert.FromBase64String(raw);
            return Encoding.UTF8.GetString(bytes);
        }));
    }

    private static void RegisterColor(BabaEnvironment env)
    {
        env.Define("red", new BuiltinFunction(1, args => { WriteColor(ConsoleColor.Red, args[0]); return null; }));
        env.Define("green", new BuiltinFunction(1, args => { WriteColor(ConsoleColor.Green, args[0]); return null; }));
        env.Define("yellow", new BuiltinFunction(1, args => { WriteColor(ConsoleColor.Yellow, args[0]); return null; }));
        env.Define("blue", new BuiltinFunction(1, args => { WriteColor(ConsoleColor.Cyan, args[0]); return null; }));
    }

    private static void RegisterModules(BabaEnvironment env)
    {
        env.Define("math", CreateMathModule());
        env.Define("str", CreateStrModule());
        env.Define("arr", CreateArrayModule());
        env.Define("obj", CreateObjectModule());
        env.Define("json", CreateJsonModule());
        env.Define("net", CreateNetModule());
        env.Define("bot", CreateBotModule());
        env.Define("crypto", CreateCryptoModule());
    }

    public static Dictionary<string, object?> CreateMathModule()
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
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
            ["min"] = new BuiltinFunction(2, args => Math.Min(ToNumber(args[0]), ToNumber(args[1]))),
            ["max"] = new BuiltinFunction(2, args => Math.Max(ToNumber(args[0]), ToNumber(args[1]))),
            ["random"] = new BuiltinFunction(0, _ => Random.Shared.NextDouble())
        };
    }

    private static Dictionary<string, object?> CreateStrModule()
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["lower"] = new BuiltinFunction(1, args => (args[0]?.ToString() ?? "").ToLowerInvariant()),
            ["upper"] = new BuiltinFunction(1, args => (args[0]?.ToString() ?? "").ToUpperInvariant()),
            ["trim"] = new BuiltinFunction(1, args => (args[0]?.ToString() ?? "").Trim()),
            ["contains"] = new BuiltinFunction(2, args => (args[0]?.ToString() ?? "").Contains(args[1]?.ToString() ?? "", StringComparison.OrdinalIgnoreCase)),
            ["replace"] = new BuiltinFunction(3, args => (args[0]?.ToString() ?? "").Replace(args[1]?.ToString() ?? "", args[2]?.ToString() ?? "", StringComparison.OrdinalIgnoreCase)),
            ["split"] = new BuiltinFunction(2, args => ((args[0]?.ToString() ?? "").Split(new[] { args[1]?.ToString() ?? "" }, StringSplitOptions.None)).Cast<object?>().ToList())
        };
    }

    private static Dictionary<string, object?> CreateArrayModule()
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["push"] = new BuiltinFunction(2, args => { var l = ToList(args[0]); l.Add(args[1]); return (double)l.Count; }),
            ["pop"] = new BuiltinFunction(1, args => { var l = ToList(args[0]); if (l.Count == 0) return null; var v = l[^1]; l.RemoveAt(l.Count - 1); return v; }),
            ["len"] = new BuiltinFunction(1, args => (double)ToList(args[0]).Count)
        };
    }

    private static Dictionary<string, object?> CreateObjectModule()
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["keys"] = new BuiltinFunction(1, args => ToMap(args[0]).Keys.Cast<object?>().ToList()),
            ["values"] = new BuiltinFunction(1, args => ToMap(args[0]).Values.Cast<object?>().ToList()),
            ["has"] = new BuiltinFunction(2, args => ToMap(args[0]).ContainsKey(args[1]?.ToString() ?? ""))
        };
    }

    private static Dictionary<string, object?> CreateJsonModule()
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["parse"] = new BuiltinFunction(1, args =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(args[0]?.ToString() ?? "");
                    return JsonToObject(doc.RootElement);
                }
                catch { return null; }
            }),
            ["stringify"] = new BuiltinFunction(1, args => ToJson(args[0]))
        };
    }

    private static Dictionary<string, object?> CreateNetModule()
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["get"] = new BuiltinFunction(1, args => ParseJsonOrText(Http.GetStringAsync(args[0]?.ToString() ?? "").GetAwaiter().GetResult())),
            ["post_json"] = new BuiltinFunction(2, args =>
            {
                try
                {
                    var url = args[0]?.ToString() ?? "";
                    var payload = ToJson(args[1]);
                    using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                    var res = Http.PostAsync(url, content).GetAwaiter().GetResult();
                    return ParseJsonOrText(res.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                }
                catch
                {
                    return null;
                }
            })
        };
    }

    private static Dictionary<string, object?> CreateBotModule()
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["discord_webhook_send"] = new BuiltinFunction(-1, args =>
            {
                if (args.Count < 2) return false;
                var webhookUrl = args[0]?.ToString() ?? "";
                var contentText = args[1]?.ToString() ?? "";
                var username = args.Count > 2 ? args[2]?.ToString() : null;
                var avatarUrl = args.Count > 3 ? args[3]?.ToString() : null;
                var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["content"] = contentText
                };
                if (!string.IsNullOrWhiteSpace(username)) payload["username"] = username;
                if (!string.IsNullOrWhiteSpace(avatarUrl)) payload["avatar_url"] = avatarUrl;

                var json = JsonSerializer.Serialize(payload);
                using var req = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                try
                {
                    var res = Http.Send(req);
                    return res.IsSuccessStatusCode;
                }
                catch
                {
                    return false;
                }
            })
        };
    }

    private static Dictionary<string, object?> CreateCryptoModule()
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["sha256"] = new BuiltinFunction(1, args =>
            {
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(args[0]?.ToString() ?? ""));
                return Convert.ToHexString(hash).ToLowerInvariant();
            }),
            ["md5"] = new BuiltinFunction(1, args =>
            {
                var hash = MD5.HashData(Encoding.UTF8.GetBytes(args[0]?.ToString() ?? ""));
                return Convert.ToHexString(hash).ToLowerInvariant();
            }),
            ["base64_encode"] = new BuiltinFunction(1, args => Convert.ToBase64String(Encoding.UTF8.GetBytes(args[0]?.ToString() ?? ""))),
            ["base64_decode"] = new BuiltinFunction(1, args => Encoding.UTF8.GetString(Convert.FromBase64String(args[0]?.ToString() ?? "")))
        };
    }

    private static int CountOf(object? value)
    {
        return value switch
        {
            null => 0,
            string s => s.Length,
            List<object?> list => list.Count,
            Dictionary<string, object?> map => map.Count,
            _ => 0
        };
    }

    private static List<object?> ToList(object? value)
    {
        if (value is List<object?> list) return list;
        ErrorReporter.Runtime("Value must be an array.");
        return new List<object?>();
    }

    private static Dictionary<string, object?> ToMap(object? value)
    {
        if (value is Dictionary<string, object?> map) return map;
        ErrorReporter.Runtime("Value must be a map.");
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    private static object? ParseJsonOrText(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return JsonToObject(doc.RootElement);
        }
        catch
        {
            return text;
        }
    }

    private static object? JsonToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => JsonToObject(p.Value), StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonToObject).Cast<object?>().ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var i) ? (double)i : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    private static string ToJson(object? value)
    {
        return JsonSerializer.Serialize(value);
    }

    private static string Stringify(object? value)
    {
        return value switch
        {
            null => "null",
            bool b => b ? "true" : "false",
            double d => d.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? ""
        };
    }

    private static void WriteColor(ConsoleColor color, object? value)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(Stringify(value));
        Console.ForegroundColor = prev;
    }

    private static double ToNumber(object? value)
    {
        if (value is double d) return d;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is float f) return f;
        if (value is decimal m) return (double)m;
        if (value is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return 0;
    }
}
