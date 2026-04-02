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

    private const string ReloadJs = """
(() => {
  let v = null;
  async function poll() {
    try {
      const res = await fetch('/__version', { cache: 'no-store' });
      const t = await res.text();
      if (v === null) v = t;
      if (v !== t) location.reload();
    } catch {}
    setTimeout(poll, 1000);
  }
  poll();
})();
""";

    private const string BundleJs = """
/* BabaShell bundle: runtime + auto-load */
(() => {
  function toSelectorExpr(raw) {
    const sel = raw.trim();
    if (sel.startsWith("'") || sel.startsWith("\"")) return sel;
    if (sel.startsWith("#") || sel.startsWith(".") || sel.startsWith("[")) {
      return JSON.stringify(sel);
    }
    return JSON.stringify("#" + sel);
  }

  function transformEmit(line) {
    const m = line.match(/^(\s*)emit\s+(.+?)\s*;?\s*$/);
    if (!m) return line;
    return `${m[1]}alert(${m[2]});`;
  }

  function splitArgs(input) {
    const args = [];
    let cur = "";
    let quote = "";
    for (let i = 0; i < input.length; i++) {
      const ch = input[i];
      if (quote) {
        cur += ch;
        if (ch === quote && input[i - 1] !== "\\") quote = "";
        continue;
      }
      if (ch === "'" || ch === "\"") {
        quote = ch;
        cur += ch;
        continue;
      }
      if (/\s/.test(ch)) {
        if (cur) {
          args.push(cur);
          cur = "";
        }
        continue;
      }
      cur += ch;
    }
    if (cur) args.push(cur);
    return args;
  }

  function transformSet(line) {
    const m = line.match(/^(\s*)set\s+(.+?)\s+(text|html|value|class)\s+(.+?)\s*;?\s*$/);
    if (!m) return line;
    const indent = m[1] ?? "";
    const sel = toSelectorExpr(m[2]);
    const prop = m[3];
    const val = m[4];
    const propMap = { text: "textContent", html: "innerHTML", value: "value", class: "className" };
    const jsProp = propMap[prop] || "textContent";
    return `${indent}document.querySelector(${sel}).${jsProp} = ${val};`;
  }

  function transformUi(line) {
    const m = line.match(/^(\s*)ui\.([A-Za-z_][A-Za-z0-9_]*)\s+(.+?)\s*;?\s*$/);
    if (!m) return line;
    const indent = m[1] ?? "";
    const fn = m[2];
    const args = splitArgs(m[3]).join(", ");
    return `${indent}ui.${fn}(${args});`;
  }

  function compileBabaShell(source) {
    const lines = source.replace(/\r\n/g, "\n").split("\n");
    const out = [];
    let depth = 0;
    const whenStack = [];

    for (let i = 0; i < lines.length; i++) {
      const line = lines[i];
      const trimmed = line.trim();

      const whenBlock = line.match(/^(\s*)when\s+(.+?)\s+([A-Za-z_][A-Za-z0-9_-]*)\s*\{\s*$/);
      if (whenBlock) {
        const indent = whenBlock[1] ?? "";
        const sel = toSelectorExpr(whenBlock[2]);
        const evt = whenBlock[3];
        out.push(`${indent}document.querySelector(${sel}).addEventListener("${evt}", ()=>{`);
        depth += 1;
        whenStack.push(depth);
        continue;
      }

      const whenSingle = line.match(/^(\s*)when\s+(.+?)\s+([A-Za-z_][A-Za-z0-9_-]*)\s+(.+?)\s*$/);
      if (whenSingle) {
        const indent = whenSingle[1] ?? "";
        const sel = toSelectorExpr(whenSingle[2]);
        const evt = whenSingle[3];
        const rest = transformEmit(whenSingle[4].trim()).trim().replace(/;$/, "");
        out.push(`${indent}document.querySelector(${sel}).addEventListener("${evt}", ()=>{ ${rest}; });`);
        continue;
      }

      if (trimmed === "}" && whenStack.length > 0 && whenStack[whenStack.length - 1] === depth) {
        const indent = line.slice(0, line.indexOf("}"));
        out.push(`${indent}});`);
        whenStack.pop();
        depth -= 1;
        continue;
      }

      let replaced = transformEmit(line);
      replaced = transformSet(replaced);
      replaced = transformUi(replaced);
      out.push(replaced);

      const openCount = (line.match(/\{/g) || []).length;
      const closeCount = (line.match(/\}/g) || []).length;
      depth += openCount - closeCount;
    }

    return out.join("\n");
  }

  function emit(msg) { alert(msg); }
  function $(selector) { return document.querySelector(selector); }
  function $$(selector) { return Array.from(document.querySelectorAll(selector)); }
  function on(selector, event, handler) { const el = $(selector); if (el) el.addEventListener(event, handler); }
  function applySelector(el, sel) {
    if (!sel) return;
    if (sel.startsWith("#")) el.id = sel.slice(1);
    else if (sel.startsWith(".")) el.className = sel.slice(1);
  }
  const ui = {
    root() { return document.querySelector("#app") || document.body; },
    title(text) {
      const h = document.createElement("h1");
      h.textContent = text ?? "";
      this.root().appendChild(h);
      return h;
    },
    text(text) {
      const p = document.createElement("p");
      p.textContent = text ?? "";
      this.root().appendChild(p);
      return p;
    },
    button(selector, text) {
      const btn = document.createElement("button");
      if (text === undefined) {
        btn.textContent = selector ?? "";
      } else {
        applySelector(btn, selector);
        btn.textContent = text ?? "";
      }
      this.root().appendChild(btn);
      return btn;
    },
    div(selector) {
      const d = document.createElement("div");
      applySelector(d, selector ?? "");
      this.root().appendChild(d);
      return d;
    }
  };

  async function runScriptTag(tag) {
    let source = "";
    if (tag.src) {
      const res = await fetch(tag.src);
      source = await res.text();
    } else {
      source = tag.textContent || "";
    }
    const js = compileBabaShell(source);
    try { new Function(js)(); } catch (err) { console.error("BabaShell runtime error:", err); }
  }

  async function boot() {
    const tags = Array.from(document.querySelectorAll('script[type="text/babashell"]'));
    for (const tag of tags) { await runScriptTag(tag); }
  }

  function autoLoadFromScriptTag() {
    const current = document.currentScript;
    if (!current) return;
    const dataSrc = current.getAttribute("data-src");
    if (!dataSrc) return;
    const tag = document.createElement("script");
    tag.type = "text/babashell";
    tag.src = dataSrc;
    document.head.appendChild(tag);
  }

  window.emit = emit;
  window.$ = $;
  window.$$ = $$;
  window.on = on;
  window.babashell = { compile: compileBabaShell, boot, emit, $, $$, on, ui };
  window.ui = ui;

  autoLoadFromScriptTag();
  if (document.readyState === "loading") { document.addEventListener("DOMContentLoaded", boot); } else { boot(); }
})();
""";
}
