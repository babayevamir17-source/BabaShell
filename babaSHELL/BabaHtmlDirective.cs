using System;
using System.IO;

namespace BabaShell;

public static class BabaHtmlDirective
{
    public static (string? htmlPath, string script) Parse(string scriptSource, string baseDir)
    {
        using var reader = new StringReader(scriptSource);
        string? line;
        var consumed = false;
        var output = new System.Text.StringBuilder();
        string? htmlPath = null;

        while ((line = reader.ReadLine()) != null)
        {
            if (!consumed)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//"))
                {
                    output.AppendLine(line);
                    continue;
                }

                if (TryParseUseFrom(trimmed, baseDir, out var resolved))
                {
                    htmlPath = resolved;
                    consumed = true;
                    continue; // drop directive line
                }
                consumed = true;
            }

            output.AppendLine(line);
        }

        return (htmlPath, output.ToString());
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
        if ((path.StartsWith("\"") && path.EndsWith("\"")) || (path.StartsWith("'") && path.EndsWith("'")))
        {
            path = path[1..^1];
        }
        var full = Path.GetFullPath(Path.Combine(baseDir, path));
        resolved = full;
        return true;
    }
}
