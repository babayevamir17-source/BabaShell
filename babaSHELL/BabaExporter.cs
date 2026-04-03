using System;
using System.Collections.Generic;
using System.IO;

namespace BabaShell;

public static class BabaExporter
{
    public static int Export(string scriptPath, string? outputPath = null)
    {
        if (!File.Exists(scriptPath))
        {
            ErrorReporter.Runtime($"File not found: {scriptPath}");
            return 1;
        }

        var absScript = Path.GetFullPath(scriptPath);
        var baseDir = Path.GetDirectoryName(absScript) ?? Directory.GetCurrentDirectory();
        var rawScript = File.ReadAllText(absScript);
        var directives = BabaHtmlDirective.ParseAll(rawScript, baseDir);
        var script = directives.Script;
        var indexPath = directives.HtmlPath ?? Path.Combine(baseDir, "index.html");
        var html = File.Exists(indexPath)
            ? File.ReadAllText(indexPath)
            : DefaultHtml(Path.GetFileName(absScript));

        var injected = InjectInline(html, script, directives.CssPaths);

        var outPath = outputPath;
        if (string.IsNullOrWhiteSpace(outPath))
        {
            var name = Path.GetFileNameWithoutExtension(absScript) + ".html";
            outPath = Path.Combine(baseDir, name);
        }
        else if (!Path.IsPathRooted(outPath))
        {
            outPath = Path.GetFullPath(Path.Combine(baseDir, outPath));
        }

        File.WriteAllText(outPath!, injected);
        Console.WriteLine($"Exported: {outPath}");
        return 0;
    }

    private static string InjectInline(string html, string script, List<string> cssPaths)
    {
        var cssBuilder = new System.Text.StringBuilder();
        foreach (var cssPath in cssPaths)
        {
            if (!File.Exists(cssPath)) continue;
            cssBuilder.AppendLine("<style>");
            cssBuilder.AppendLine(File.ReadAllText(cssPath));
            cssBuilder.AppendLine("</style>");
        }

        var inject =
            cssBuilder.ToString() +
            "<script>\n" + BabaRuntime.BundleJs + "\n</script>\n" +
            "<script type=\"text/babashell\">\n" + script + "\n</script>\n";

        if (html.Contains("<!-- BABASHELL -->", StringComparison.OrdinalIgnoreCase))
        {
            return html.Replace("<!-- BABASHELL -->", inject, StringComparison.OrdinalIgnoreCase);
        }

        var idx = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            return html.Insert(idx, inject);
        }
        return html + "\n" + inject;
    }

    private static string DefaultHtml(string title)
    {
        return $@"<!doctype html>
<html lang=""en"">
  <head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
    <title>{title}</title>
  </head>
  <body>
    <div id=""app""></div>
    <!-- BABASHELL -->
  </body>
</html>";
    }
}
