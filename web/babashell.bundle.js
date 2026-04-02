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
