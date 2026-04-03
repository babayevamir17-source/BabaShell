using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
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
        using var htmlWatcher = new FileSystemWatcher(baseDir, "*.html");
        using var cssWatcher = new FileSystemWatcher(baseDir, "*.css");

        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
        watcher.Changed += (_, __) => Interlocked.Increment(ref version);
        watcher.Renamed += (_, __) => Interlocked.Increment(ref version);
        watcher.EnableRaisingEvents = true;

        htmlWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
        htmlWatcher.Changed += (_, __) => Interlocked.Increment(ref version);
        htmlWatcher.Renamed += (_, __) => Interlocked.Increment(ref version);
        htmlWatcher.EnableRaisingEvents = true;

        cssWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
        cssWatcher.Changed += (_, __) => Interlocked.Increment(ref version);
        cssWatcher.Renamed += (_, __) => Interlocked.Increment(ref version);
        cssWatcher.EnableRaisingEvents = true;

        TcpListener? listener = null;
        var port = portOverride ?? DefaultPort;
        while (listener == null && port <= DefaultPort + 50)
        {
            try
            {
                var candidate = new TcpListener(IPAddress.Any, port);
                candidate.Start();
                listener = candidate;
            }
            catch
            {
                port++;
            }
        }

        if (listener == null)
        {
            ErrorReporter.Runtime("Unable to start server on localhost.");
            return 1;
        }

        Console.WriteLine("BabaShell dev server running:");
        Console.WriteLine($"  http://127.0.0.1:{port}/");
        Console.WriteLine($"  http://localhost:{port}/");
        foreach (var ip in GetLanIps())
        {
            Console.WriteLine($"  http://{ip}:{port}/");
        }
        Console.WriteLine($"  watching: {absPath}");
        Console.WriteLine("Press Ctrl+C to stop.");

        try
        {
            while (true)
            {
                var client = listener.AcceptTcpClient();
                _ = Task.Run(() => HandleClient(client, absPath, baseDir, () => Volatile.Read(ref version)));
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private static void HandleClient(TcpClient client, string scriptPath, string baseDir, Func<int> getVersion)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true))
        {
            try
            {
                var requestLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(requestLine)) return;

                var parts = requestLine.Split(' ');
                if (parts.Length < 2) return;

                // read and ignore headers
                string? header;
                do { header = reader.ReadLine(); } while (!string.IsNullOrEmpty(header));

                var path = parts[1];
                var q = path.IndexOf('?');
                if (q >= 0) path = path[..q];

                if (path == "/")
                {
                    var scriptSource = File.ReadAllText(scriptPath);
                    var directives = BabaHtmlDirective.ParseAll(scriptSource, baseDir);
                    var indexPath = directives.HtmlPath ?? Path.Combine(baseDir, "index.html");
                    var html = File.Exists(indexPath) ? File.ReadAllText(indexPath) : HtmlPage(scriptPath);
                    var body = InjectRuntime(html, directives.CssPaths, baseDir);
                    WriteResponse(stream, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(body));
                    return;
                }

                if (path == "/app.babashell")
                {
                    var scriptSource = File.ReadAllText(scriptPath);
                    var stripped = BabaHtmlDirective.ParseAll(scriptSource, baseDir).Script;
                    WriteResponse(stream, 200, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(stripped));
                    return;
                }

                if (path == "/babashell.bundle.js")
                {
                    WriteResponse(stream, 200, "application/javascript; charset=utf-8", Encoding.UTF8.GetBytes(BundleJs));
                    return;
                }

                if (path == "/__version")
                {
                    WriteResponse(stream, 200, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(getVersion().ToString()));
                    return;
                }

                if (path == "/__reload.js")
                {
                    WriteResponse(stream, 200, "application/javascript; charset=utf-8", Encoding.UTF8.GetBytes(ReloadJs));
                    return;
                }

                if (TryServeStatic(stream, baseDir, path))
                {
                    return;
                }

                WriteResponse(stream, 404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Not found"));
            }
            catch (Exception ex)
            {
                WriteResponse(stream, 500, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Server error: " + ex.Message));
            }
        }
    }

    private static void WriteResponse(Stream stream, int statusCode, string contentType, byte[] body)
    {
        var reason = statusCode switch
        {
            200 => "OK",
            404 => "Not Found",
            500 => "Internal Server Error",
            _ => "OK"
        };

        var headers =
            $"HTTP/1.1 {statusCode} {reason}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Connection: close\r\n" +
            "\r\n";
        var headerBytes = Encoding.UTF8.GetBytes(headers);
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write(body, 0, body.Length);
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

    private static string InjectRuntime(string html, List<string> cssPaths, string baseDir)
    {
        var cssInject = new StringBuilder();
        foreach (var cssPath in cssPaths)
        {
            if (!File.Exists(cssPath)) continue;
            var rel = Path.GetRelativePath(baseDir, cssPath).Replace("\\", "/");
            cssInject.AppendLine($"<link rel=\"stylesheet\" href=\"/{rel}\">");
        }

        var inject = cssInject +
                     "<script src=\"/__reload.js\"></script>\n" +
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

    private static bool TryServeStatic(Stream stream, string baseDir, string urlPath)
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
            ".html" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".svg" => "image/svg+xml",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        var bytes = File.ReadAllBytes(full);
        WriteResponse(stream, 200, contentType, bytes);
        return true;
    }

    private const string ReloadJs = BabaRuntime.ReloadJs;
    private const string BundleJs = BabaRuntime.BundleJs;

    private static List<string> GetLanIps()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Select(a => a.Address)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }
}
