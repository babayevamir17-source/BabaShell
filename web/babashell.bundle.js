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

      const replaced = transformEmit(line);
      out.push(replaced);

      const openCount = (line.match(/\{/g) || []).length;
      const closeCount = (line.match(/\}/g) || []).length;
      depth += openCount - closeCount;
    }

    return out.join("\n");
  }

  function emit(msg) {
    alert(msg);
  }

  function $(selector) {
    return document.querySelector(selector);
  }

  function $$(selector) {
    return Array.from(document.querySelectorAll(selector));
  }

  function on(selector, event, handler) {
    const el = $(selector);
    if (el) el.addEventListener(event, handler);
  }

  async function runScriptTag(tag) {
    let source = "";
    if (tag.src) {
      const res = await fetch(tag.src);
      source = await res.text();
    } else {
      source = tag.textContent || "";
    }
    const js = compileBabaShell(source);
    try {
      // eslint-disable-next-line no-new-func
      new Function(js)();
    } catch (err) {
      console.error("BabaShell runtime error:", err);
    }
  }

  async function boot() {
    const tags = Array.from(document.querySelectorAll('script[type="text/babashell"]'));
    for (const tag of tags) {
      await runScriptTag(tag);
    }
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
  window.babashell = { compile: compileBabaShell, boot, emit, $, $$, on };

  autoLoadFromScriptTag();

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", boot);
  } else {
    boot();
  }
})();
