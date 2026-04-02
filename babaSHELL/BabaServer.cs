using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BabaShell;

public static class BabaServer
{
    private const int DefaultPort = 3000;

    public static int Serve(string scriptPath, int? portOverride = null)
    {
        if (!File.Exists(scriptPath))
        {
            ErrorReporter.Runtime($"File not found: {scriptPath}");
            return 1;
        }

        var absPath = Path.GetFullPath(scriptPath);
        var baseDir = Path.GetDirectoryName(absPath) ?? Directory.GetCurrentDirectory();
        var version = 1;
        using var watcher = new FileSystemWatcher(baseDir, Path.GetFileName(absPath));
        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
        watcher.Changed += (_, __) => Interlocked.Increment(ref version);
        watcher.Renamed += (_, __) => Interlocked.Increment(ref version);
        watcher.EnableRaisingEvents = true;

        var listener = new HttpListener();
        var port = portOverride ?? DefaultPort;
        while (true)
        {
            var prefix = $"http://localhost:{port}/";
            try
            {
                listener.Prefixes.Clear();
                listener.Prefixes.Add(prefix);
                listener.Start();
                break;
            }
            catch
            {
                port++;
                if (port > DefaultPort + 50)
                {
                    ErrorReporter.Runtime("Unable to start server on localhost.");
                    return 1;
                }
            }
        }

        Console.WriteLine($"BabaShell dev server running:");
        Console.WriteLine($"  http://localhost:{port}/");
        Console.WriteLine($"  watching: {absPath}");
        Console.WriteLine("Press Ctrl+C to stop.");

        while (listener.IsListening)
        {
            var ctx = listener.GetContext();
            _ = Task.Run(() => Handle(ctx, absPath, baseDir, port, () => Volatile.Read(ref version)));
        }

        return 0;
    }

    private static void Handle(HttpListenerContext ctx, string scriptPath, string baseDir, int port, Func<int> getVersion)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath ?? "/";
            if (path == "/")
            {
                var indexPath = Path.Combine(baseDir, "index.html");
                if (File.Exists(indexPath))
                {
                    var html = File.ReadAllText(indexPath);
                    WriteText(ctx, InjectRuntime(html), "text/html");
                }
                else
                {
                    WriteText(ctx, HtmlPage(scriptPath));
                }
                return;
            }
            if (path == "/app.babashell")
            {
                WriteText(ctx, File.ReadAllText(scriptPath), "text/plain");
                return;
            }
            if (path == "/babashell.bundle.js")
            {
                WriteText(ctx, BundleJs, "application/javascript");
                return;
            }
            if (path == "/__version")
            {
                WriteText(ctx, getVersion().ToString(), "text/plain");
                return;
            }
            if (path == "/__reload.js")
            {
                WriteText(ctx, ReloadJs, "application/javascript");
                return;
            }

            if (TryServeStatic(ctx, baseDir, path))
            {
                return;
            }

            ctx.Response.StatusCode = 404;
            WriteText(ctx, "Not found", "text/plain");
        }
        catch (Exception ex)
        {
            try
            {
                ctx.Response.StatusCode = 500;
                WriteText(ctx, "Server error: " + ex.Message, "text/plain");
            }
            catch
            {
                // ignore
            }
        }
    }

    private static void WriteText(HttpListenerContext ctx, string text, string contentType = "text/html")
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        ctx.Response.ContentType = contentType + "; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.OutputStream.Close();
    }

    private static string HtmlPage(string scriptPath)
    {
        var title = Path.GetFileName(scriptPath);
        return $@"<!doctype html>
<html lang=""en"">
  <head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <title>{title}</title>
  </head>
  <body>
    <div id=""app""></div>
    <script src=""/__reload.js""></script>
    <script src=""/babashell.bundle.js"" data-src=""/app.babashell""></script>
  </body>
</html>";
    }

    private static string InjectRuntime(string html)
    {
        var inject = "<script src=\"/__reload.js\"></script>\n" +
                     "<script src=\"/babashell.bundle.js\" data-src=\"/app.babashell\"></script>\n";
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

    private static bool TryServeStatic(HttpListenerContext ctx, string baseDir, string urlPath)
    {
        var rel = Uri.UnescapeDataString(urlPath.TrimStart('/'));
        if (string.IsNullOrWhiteSpace(rel)) return false;
        if (rel.Contains("..")) return false;
        var full = Path.GetFullPath(Path.Combine(baseDir, rel));
        if (!full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase)) return false;
        if (!File.Exists(full)) return false;

        var ext = Path.GetExtension(full).ToLowerInvariant();
        var contentType = ext switch
        {
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".svg" => "image/svg+xml",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };

        var bytes = File.ReadAllBytes(full);
        ctx.Response.ContentType = contentType;
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.OutputStream.Close();
        return true;
    }

    private const string ReloadJs = BabaRuntime.ReloadJs;
    private const string BundleJs = BabaRuntime.BundleJs;
}
