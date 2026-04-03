using System.Text.RegularExpressions;

namespace BabaShell;

public static class BabaScriptPreprocessor
{
    private static readonly Regex CssWithSelector = new(
        @"^\s*[A-Za-z_][A-Za-z0-9_-]*\.[#.]?[A-Za-z_][A-Za-z0-9_-]*\.[A-Za-z-]+\s*:\s*.+$",
        RegexOptions.Compiled);

    private static readonly Regex CssRoot = new(
        @"^\s*[A-Za-z_][A-Za-z0-9_-]*\.[A-Za-z-]+\s*:\s*.+$",
        RegexOptions.Compiled);

    public static string StripWebOnlyLines(string source)
    {
        var lines = source.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//")) continue;

            if (CssWithSelector.IsMatch(line) || CssRoot.IsMatch(line))
            {
                lines[i] = "";
            }
        }
        return string.Join("\n", lines);
    }
}

