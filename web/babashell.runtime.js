/* BabaShell browser runtime (JS-like with small sugar) */
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

      const whenBlock = line.match(/^(\s*)when\s+(.+?)\s+clicked\s*\{\s*$/);
      if (whenBlock) {
        const indent = whenBlock[1] ?? "";
        const sel = toSelectorExpr(whenBlock[2]);
        out.push(`${indent}document.querySelector(${sel}).addEventListener("click", ()=>{`);
        depth += 1;
        whenStack.push(depth);
        continue;
      }

      const whenSingle = line.match(/^(\s*)when\s+(.+?)\s+clicked\s+(.+?)\s*$/);
      if (whenSingle) {
        const indent = whenSingle[1] ?? "";
        const sel = toSelectorExpr(whenSingle[2]);
        const rest = transformEmit(whenSingle[3].trim()).trim().replace(/;$/, "");
        out.push(`${indent}document.querySelector(${sel}).addEventListener("click", ()=>{ ${rest}; });`);
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

  window.babashell = { compile: compileBabaShell, boot };

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", boot);
  } else {
    boot();
  }
})();
