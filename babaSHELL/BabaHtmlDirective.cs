using System;
using System.Collections.Generic;
using System.IO;

namespace BabaShell;

public sealed class BabaUseDirectives
{
    public string? HtmlPath { get; init; }
    public List<string> CssPaths { get; init; } = new();
    public string Script { get; init; } = "";
}

public static class BabaHtmlDirective
{
    public static (string? htmlPath, string script) Parse(string scriptSource, string baseDir)
    {
        var all = ParseAll(scriptSource, baseDir);
        return (all.HtmlPath, all.Script);
    }

    public static BabaUseDirectives ParseAll(string scriptSource, string baseDir)
    {
        using var reader = new StringReader(scriptSource);
        string? line;
        var output = new System.Text.StringBuilder();
        string? htmlPath = null;
        var cssPaths = new List<string>();
        var atTop = true;

        while ((line = reader.ReadLine()) != null)
        {
            if (atTop)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//"))
                {
                    output.AppendLine(line);
                    continue;
                }

                if (TryParseUseFrom(trimmed, baseDir, out var resolved))
                {
                    if (resolved != null)
                    {
                        var ext = Path.GetExtension(resolved).ToLowerInvariant();
                        if (ext is ".html" or ".htm")
                        {
                            htmlPath = resolved;
                        }
                        else if (ext == ".css")
                        {
                            cssPaths.Add(resolved);
                        }
                    }
                    continue; // drop directive lines
                }
                atTop = false;
            }

            output.AppendLine(line);
        }

        return new BabaUseDirectives
        {
            HtmlPath = htmlPath,
            CssPaths = cssPaths,
            Script = output.ToString()
        };
    }

    private static bool TryParseUseFrom(string line, string baseDir, out string? resolved)
    {
        resolved = null;
        if (!line.StartsWith("use", StringComparison.OrdinalIgnoreCase)) return false;
        var rest = line.Substring(3).TrimStart();
        if (!rest.StartsWith("from", StringComparison.OrdinalIgnoreCase)) return false;
        rest = rest.Substring(4).Trim();
        if (string.IsNullOrWhiteSpace(rest)) return false;

        var path = rest;
        if (path.StartsWith("{") && path.EndsWith("}"))
        {
            path = path[1..^1].Trim();
        }
        if ((path.StartsWith("\"") && path.EndsWith("\"")) || (path.StartsWith("'") && path.EndsWith("'")))
        {
            path = path[1..^1];
        }
        var full = Path.GetFullPath(Path.Combine(baseDir, path));
        resolved = full;
        return true;
    }
}
