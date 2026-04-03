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

  function normalizeEventName(evtRaw) {
    const evt = (evtRaw || "").trim().toLowerCase();
    const aliases = {
      clicked: "click",
      hover: "mouseenter"
    };
    return aliases[evt] || evt;
  }

  function isDomEventName(evtRaw) {
    const evt = normalizeEventName(evtRaw);
    return [
      "click",
      "mouseenter",
      "mouseleave",
      "mouseover",
      "mouseout",
      "mousedown",
      "mouseup",
      "keydown",
      "keyup",
      "input",
      "change",
      "submit"
    ].includes(evt);
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
    const attrEq = line.match(/^(\s*)set\s+(.+?)\s+([A-Za-z_][A-Za-z0-9_:-]*)\s*=\s*(.+?)\s*;?\s*$/);
    if (attrEq) {
      const indent = attrEq[1] ?? "";
      const sel = toSelectorExpr(attrEq[2]);
      const attr = attrEq[3];
      const val = attrEq[4];
      if (attr.toLowerCase() === "style") {
        return `${indent}{ const __bs_el = document.querySelector(${sel}); if (__bs_el) { __bs_el.setAttribute("style", String(${val})); } else { console.warn("[BabaShell] set target not found:", ${sel}); } }`;
      }
      return `${indent}{ const __bs_el = document.querySelector(${sel}); if (__bs_el) { __bs_el.setAttribute("${attr}", String(${val})); } else { console.warn("[BabaShell] set target not found:", ${sel}); } }`;
    }

    const m = line.match(/^(\s*)set\s+(.+?)\s+([A-Za-z_][A-Za-z0-9_-]*)\s+(.+?)\s*;?\s*$/);
    if (!m) return line;
    const indent = m[1] ?? "";
    const sel = toSelectorExpr(m[2]);
    const prop = m[3];
    const val = m[4];
    const propMap = { text: "textContent", html: "innerHTML", value: "value", class: "className" };
    const jsProp = propMap[prop];
    if (jsProp) {
      return `${indent}{ const __bs_el = document.querySelector(${sel}); if (__bs_el) { __bs_el.${jsProp} = ${val}; } else { console.warn("[BabaShell] set target not found:", ${sel}); } }`;
    }
    if (prop.includes("-")) {
      return `${indent}{ const __bs_el = document.querySelector(${sel}); if (__bs_el) { __bs_el.style.setProperty("${prop}", String(${val})); } else { console.warn("[BabaShell] set target not found:", ${sel}); } }`;
    }
    return `${indent}{ const __bs_el = document.querySelector(${sel}); if (__bs_el) { __bs_el.setAttribute("${prop}", String(${val})); } else { console.warn("[BabaShell] set target not found:", ${sel}); } }`;
  }

  function transformUi(line) {
    const m = line.match(/^(\s*)ui\.([A-Za-z_][A-Za-z0-9_]*)\s+(.+?)\s*;?\s*$/);
    if (!m) return line;
    const indent = m[1] ?? "";
    const fn = m[2];
    const args = splitArgs(m[3]).join(", ");
    return `${indent}ui.${fn}(${args});`;
  }

  function transformStore(line) {
    const m = line.match(/^(\s*)store\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+?)\s*;?\s*$/);
    if (!m) return line;
    return `${m[1]}let ${m[2]} = ${m[3]};`;
  }

  function transformAdjust(line) {
    const inc = line.match(/^(\s*)increase\s+([A-Za-z_][A-Za-z0-9_]*)\s+by\s+(.+?)\s*;?\s*$/);
    if (inc) return `${inc[1]}${inc[2]} = (${inc[2]}) + (${inc[3]});`;
    const dec = line.match(/^(\s*)decrease\s+([A-Za-z_][A-Za-z0-9_]*)\s+by\s+(.+?)\s*;?\s*$/);
    if (dec) return `${dec[1]}${dec[2]} = (${dec[2]}) - (${dec[3]});`;
    return line;
  }

  function transformCall(line) {
    const m = line.match(/^(\s*)call\s+(.+?)\s*;?\s*$/);
    if (!m) return line;
    return `${m[1]}${m[2]};`;
  }

  function transformFunc(line) {
    const m = line.match(/^(\s*)func\s+([A-Za-z_][A-Za-z0-9_]*)\s*\((.*?)\)\s*\{\s*$/);
    if (!m) return line;
    return `${m[1]}function ${m[2]}(${m[3]}) {`;
  }

  function transformIfLine(line) {
    const m = line.match(/^(\s*)if\s+(.+?)\s*\{\s*$/);
    if (!m) return line;
    return `${m[1]}if (${m[2]}) {`;
  }

  function toDurationMsExpr(raw) {
    const s = raw.trim();
    const m = s.match(/^(\d+(?:\.\d+)?)\s*([A-Za-z]+)?$/);
    if (!m) return `(${s})`;
    const num = Number(m[1]);
    const unit = (m[2] || "ms").toLowerCase();
    if (unit === "s" || unit === "sec" || unit === "secs" || unit === "second" || unit === "seconds") return String(Math.round(num * 1000));
    if (unit === "m" || unit === "min" || unit === "mins" || unit === "minute" || unit === "minutes") return String(Math.round(num * 60000));
    return String(Math.round(num));
  }

  function compileBabaShell(source) {
    const lines = source.replace(/\r\n/g, "\n").split("\n");
    const out = [];
    let depth = 0;
    const blockStack = [];

    for (let i = 0; i < lines.length; i++) {
      const line = lines[i];
      const trimmed = line.trim();

      const whenBlock = line.match(/^(\s*)when\s+(.+?)\s+([A-Za-z_][A-Za-z0-9_-]*)\s*\{\s*$/);
      if (whenBlock && isDomEventName(whenBlock[3])) {
        const indent = whenBlock[1] ?? "";
        const sel = toSelectorExpr(whenBlock[2]);
        const evt = normalizeEventName(whenBlock[3]);
        out.push(`${indent}babashell.__on(${sel}, "${evt}", ()=>{`);
        depth += 1;
        blockStack.push({ depth, close: `${indent}});` });
        continue;
      }

      const whenCondBlock = line.match(/^(\s*)when\s+(.+?)\s*\{\s*$/);
      if (whenCondBlock) {
        const indent = whenCondBlock[1] ?? "";
        out.push(`${indent}if (${whenCondBlock[2]}) {`);
        depth += 1;
        blockStack.push({ depth, close: `${indent}}` });
        continue;
      }

      const repeatBlock = line.match(/^(\s*)repeat\s+(.+?)\s+times\s*\{\s*$/);
      if (repeatBlock) {
        const indent = repeatBlock[1] ?? "";
        const countExpr = repeatBlock[2];
        const idx = `__bs_i_${i}`;
        out.push(`${indent}for (let ${idx} = 0; ${idx} < (${countExpr}); ${idx}++) {`);
        depth += 1;
        blockStack.push({ depth, close: `${indent}}` });
        continue;
      }

      const forInBlock = line.match(/^(\s*)for\s+([A-Za-z_][A-Za-z0-9_]*)\s+in\s+(.+?)\s*\{\s*$/);
      if (forInBlock) {
        const indent = forInBlock[1] ?? "";
        out.push(`${indent}for (const ${forInBlock[2]} of (${forInBlock[3]})) {`);
        depth += 1;
        blockStack.push({ depth, close: `${indent}}` });
        continue;
      }

      const waitBlock = line.match(/^(\s*)wait\s+(.+?)\s*\{\s*$/);
      if (waitBlock) {
        const indent = waitBlock[1] ?? "";
        const msExpr = toDurationMsExpr(waitBlock[2]);
        out.push(`${indent}setTimeout(()=>{`);
        depth += 1;
        blockStack.push({ depth, close: `${indent}}, ${msExpr});` });
        continue;
      }

      const fetchBlock = line.match(/^(\s*)fetch\s+(.+?)\s+as\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{\s*$/);
      if (fetchBlock) {
        const indent = fetchBlock[1] ?? "";
        const urlExpr = fetchBlock[2];
        const target = fetchBlock[3];
        out.push(`${indent}fetch(${urlExpr}).then(async (__bs_res)=>{`);
        out.push(`${indent}  let ${target};`);
        out.push(`${indent}  const __bs_txt = await __bs_res.text();`);
        out.push(`${indent}  try { ${target} = JSON.parse(__bs_txt); } catch { ${target} = __bs_txt; }`);
        depth += 1;
        blockStack.push({ depth, close: `${indent}}).catch((err)=>console.error("[BabaShell] fetch error:", err));` });
        continue;
      }

      const whenSingle = line.match(/^(\s*)when\s+(.+?)\s+([A-Za-z_][A-Za-z0-9_-]*)\s+(.+?)\s*$/);
      if (whenSingle && isDomEventName(whenSingle[3])) {
        const indent = whenSingle[1] ?? "";
        const sel = toSelectorExpr(whenSingle[2]);
        const evt = normalizeEventName(whenSingle[3]);
        const rest = transformEmit(whenSingle[4].trim()).trim().replace(/;$/, "");
        out.push(`${indent}babashell.__on(${sel}, "${evt}", ()=>{ ${rest}; });`);
        continue;
      }

      if (trimmed === "}" && blockStack.length > 0 && blockStack[blockStack.length - 1].depth === depth) {
        const state = blockStack.pop();
        out.push(state.close);
        depth -= 1;
        continue;
      }

      let replaced = transformEmit(line);
      replaced = transformFunc(replaced);
      replaced = transformIfLine(replaced);
      replaced = transformStore(replaced);
      replaced = transformAdjust(replaced);
      replaced = transformCall(replaced);
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
  function __on(selector, event, handler) {
    const delegatedEvent = event === "mouseenter" ? "mouseover" : event;
    document.addEventListener(delegatedEvent, (ev) => {
      const origin = ev.target;
      if (!(origin instanceof Element)) return;
      const matched = origin.closest(selector);
      if (!matched) return;
      if (event === "mouseenter") {
        const rel = ev.relatedTarget;
        if (rel instanceof Element && matched.contains(rel)) return;
      }
      handler(ev, matched);
    });
  }
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
  window.babashell = { compile: compileBabaShell, boot, emit, $, $$, on, __on, ui };
  window.ui = ui;

  autoLoadFromScriptTag();
  if (document.readyState === "loading") { document.addEventListener("DOMContentLoaded", boot); } else { boot(); }
})();
