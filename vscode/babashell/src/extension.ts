import * as vscode from "vscode";
import * as fs from "fs";
import * as path from "path";
import { execFile } from "child_process";

const KEYWORDS = [
  "emit",
  "store",
  "increase",
  "decrease",
  "by",
  "if",
  "try",
  "catch",
  "throw",
  "while",
  "break",
  "continue",
  "when",
  "use",
  "from",
  "set",
  "ui",
  "clicked",
  "hover",
  "repeat",
  "times",
  "for",
  "in",
  "wait",
  "fetch",
  "as",
  "call",
  "else",
  "loop",
  "func",
  "return",
  "import",
  "true",
  "false",
  "null",
  "and",
  "or",
  "map"
];

const BUILTINS = [
  "serve",
  "export",
  "build",
  "help",
  "red",
  "green",
  "yellow",
  "blue",
  "read",
  "input",
  "confirm",
  "ask_number",
  "choose",
  "clear",
  "size",
  "lower",
  "upper",
  "trim",
  "contains",
  "split",
  "join",
  "slice",
  "file_read",
  "file_write",
  "file_append",
  "file_exists",
  "file_delete",
  "file_copy",
  "file_move",
  "dir_exists",
  "dir_make",
  "dir_delete",
  "dir_list",
  "now",
  "unix_time",
  "format_time"
  ,"type_of","parse_number","to_string","starts_with","ends_with","replace","regex_is_match"
  ,"push","pop","shift","unshift","keys","values","has_key"
  ,"http_get","http_post_json","json_parse","json_stringify","discord_webhook_send"
  ,"hash_sha256","hash_md5","base64_encode","base64_decode"
];

const CSS_PROPERTIES = [
  "color",
  "background",
  "background-color",
  "display",
  "position",
  "top",
  "right",
  "bottom",
  "left",
  "width",
  "height",
  "max-width",
  "min-width",
  "max-height",
  "min-height",
  "margin",
  "margin-top",
  "margin-right",
  "margin-bottom",
  "margin-left",
  "padding",
  "padding-top",
  "padding-right",
  "padding-bottom",
  "padding-left",
  "border",
  "border-radius",
  "font-size",
  "font-weight",
  "line-height",
  "text-align",
  "text-decoration",
  "opacity",
  "z-index",
  "overflow",
  "cursor",
  "gap",
  "flex",
  "flex-direction",
  "justify-content",
  "align-items",
  "grid-template-columns",
  "grid-template-rows"
  ,"box-shadow","transform","transition","animation","visibility","user-select","pointer-events"
  ,"white-space","word-break","letter-spacing","backdrop-filter","filter","outline","object-fit"
  ,"background-image","background-size","background-repeat","background-position","border-color","border-width"
];

const CSS_VALUES_BY_PROPERTY: Record<string, string[]> = {
  display: ["block", "inline", "inline-block", "none", "flex", "grid"],
  position: ["static", "relative", "absolute", "fixed", "sticky"],
  "text-align": ["left", "center", "right", "justify"],
  "font-weight": ["normal", "bold", "500", "600", "700"],
  "text-decoration": ["none", "underline", "line-through"],
  overflow: ["visible", "hidden", "auto", "scroll"],
  cursor: ["pointer", "default", "text", "move", "not-allowed"],
  "flex-direction": ["row", "row-reverse", "column", "column-reverse"],
  "justify-content": ["flex-start", "center", "flex-end", "space-between", "space-around"],
  "align-items": ["flex-start", "center", "flex-end", "stretch"],
  color: ["#000000", "#ffffff", "red", "blue", "green", "transparent"],
  "background-color": ["#000000", "#ffffff", "transparent"],
  width: ["100%", "auto", "fit-content"],
  height: ["100%", "auto", "fit-content"]
  ,"visibility": ["visible","hidden","collapse"]
  ,"object-fit": ["contain","cover","fill","none","scale-down"]
  ,"background-repeat": ["no-repeat","repeat","repeat-x","repeat-y"]
  ,"background-size": ["cover","contain","auto"]
  ,"user-select": ["none","auto","text","all"]
};

const LIBRARY_STATE_KEY = "babashell.library.v1";

type LibrarySnapshot = {
  selectors: string[];
  properties: string[];
  values: string[];
  symbols: string[];
};

function createEmptyLibrary(): LibrarySnapshot {
  return { selectors: [], properties: [], values: [], symbols: [] };
}

function unique(values: Iterable<string>): string[] {
  const set = new Set<string>();
  for (const raw of values) {
    const value = raw.trim();
    if (value.length > 0) set.add(value);
  }
  return [...set];
}

function parseLibraryFromText(text: string): LibrarySnapshot {
  const selectors: string[] = [];
  const properties: string[] = [];
  const values: string[] = [];
  const symbols: string[] = [];

  const selectorMatches = text.match(/[#.][A-Za-z_][A-Za-z0-9_-]*/g);
  if (selectorMatches) selectors.push(...selectorMatches);

  for (const line of text.split(/\r?\n/)) {
    const setMatch = line.match(/^\s*set\s+("[^"]+"|#[A-Za-z0-9_-]+|\.[A-Za-z0-9_-]+|[A-Za-z_][A-Za-z0-9_-]*)\s+([A-Za-z][A-Za-z0-9-]*)\s+(.+)\s*$/);
    if (setMatch) {
      properties.push(setMatch[2].toLowerCase());
      const rawValue = setMatch[3].trim().replace(/;$/, "");
      if (rawValue.length > 0) values.push(rawValue.replace(/^"|"$/g, ""));
    }

    const symbolMatch = line.match(/^\s*(?:store|func)\s+([A-Za-z_][A-Za-z0-9_]*)/);
    if (symbolMatch) symbols.push(symbolMatch[1]);
  }

  return {
    selectors: unique(selectors).slice(0, 500),
    properties: unique(properties).slice(0, 500),
    values: unique(values).slice(0, 500),
    symbols: unique(symbols).slice(0, 500)
  };
}

function mergeLibrary(base: LibrarySnapshot, next: LibrarySnapshot): LibrarySnapshot {
  return {
    selectors: unique([...base.selectors, ...next.selectors]).slice(0, 1000),
    properties: unique([...base.properties, ...next.properties]).slice(0, 1000),
    values: unique([...base.values, ...next.values]).slice(0, 1000),
    symbols: unique([...base.symbols, ...next.symbols]).slice(0, 1000)
  };
}

async function learnFromDocument(context: vscode.ExtensionContext, doc: vscode.TextDocument): Promise<void> {
  if (doc.languageId !== "babashell") return;
  const existing = context.globalState.get<LibrarySnapshot>(LIBRARY_STATE_KEY) ?? createEmptyLibrary();
  const parsed = parseLibraryFromText(doc.getText());
  const merged = mergeLibrary(existing, parsed);
  if (JSON.stringify(existing) === JSON.stringify(merged)) return;
  await context.globalState.update(LIBRARY_STATE_KEY, merged);
}

export function activate(context: vscode.ExtensionContext) {
  const diagnostics = vscode.languages.createDiagnosticCollection("babashell");

  const runCmd = vscode.commands.registerCommand("babashell.runFile", async () => {
    const editor = vscode.window.activeTextEditor;
    if (!editor) {
      vscode.window.showWarningMessage("No active .babashell file found.");
      return;
    }

    const doc = editor.document;
    if (doc.languageId !== "babashell") {
      vscode.window.showWarningMessage("This command only works for .babashell files.");
      return;
    }

    await doc.save();

    const filePath = doc.fileName;
    const content = doc.getText();
    const config = vscode.workspace.getConfiguration("babashell");
    const configuredExe = (config.get<string>("executablePath") || "").trim();
    let exePath = configuredExe || "babashell";

    if (!configuredExe) {
      const defaultInstallPath = "C:\\Program Files\\BabaShell\\babaSHELL.exe";
      if (fs.existsSync(defaultInstallPath)) {
        exePath = defaultInstallPath;
      }
      const workspaceFolder = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
      if (workspaceFolder) {
        const candidate = path.join(workspaceFolder, "dist", "cli", "babashell.exe");
        if (fs.existsSync(candidate)) {
          exePath = candidate;
        }
      }
    }
    const terminal = vscode.window.createTerminal({ name: "BabaShell" });
    terminal.show(true);
    const hasUseFrom = /^\s*use\s+from\s+/im.test(content);
    if (hasUseFrom) {
      const port = 3000;
      terminal.sendText(`& "${exePath}" serve "${filePath}" ${port}`);
      vscode.env.openExternal(vscode.Uri.parse(`http://127.0.0.1:${port}/`));
    } else {
      terminal.sendText(`& "${exePath}" "${filePath}"`);
    }
  });

  const runCheck = (doc: vscode.TextDocument) => {
    if (doc.languageId !== "babashell") return;
    const config = vscode.workspace.getConfiguration("babashell");
    const configuredExe = (config.get<string>("executablePath") || "").trim();
    let exePath = configuredExe || "babashell";
    if (!configuredExe) {
      const defaultInstallPath = "C:\\Program Files\\BabaShell\\babaSHELL.exe";
      if (fs.existsSync(defaultInstallPath)) {
        exePath = defaultInstallPath;
      }
      const workspaceFolder = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
      if (workspaceFolder) {
        const candidate = path.join(workspaceFolder, "dist", "cli", "babashell.exe");
        if (fs.existsSync(candidate)) {
          exePath = candidate;
        }
      }
    }

    execFile(exePath, ["--check", doc.fileName], (err, stdout, stderr) => {
      const output = `${stdout ?? ""}\n${stderr ?? ""}`;
      const diags: vscode.Diagnostic[] = [];
      const regexLegacy = /Syntax error \((\d+):(\d+)\):\s*(.+)/g;
      const regexV1 = /\[Error\]\s+.*?:(\d+):(\d+)\s+(.+)/g;
      let match: RegExpExecArray | null;
      while ((match = regexLegacy.exec(output)) !== null) {
        const line = Math.max(0, parseInt(match[1], 10) - 1);
        const col = Math.max(0, parseInt(match[2], 10) - 1);
        const msg = match[3] || "Syntax error";
        const range = new vscode.Range(line, col, line, col + 1);
        diags.push(new vscode.Diagnostic(range, msg, vscode.DiagnosticSeverity.Error));
      }
      while ((match = regexV1.exec(output)) !== null) {
        const line = Math.max(0, parseInt(match[1], 10) - 1);
        const col = Math.max(0, parseInt(match[2], 10) - 1);
        const msg = match[3] || "Syntax error";
        const range = new vscode.Range(line, col, line, col + 1);
        diags.push(new vscode.Diagnostic(range, msg, vscode.DiagnosticSeverity.Error));
      }
      diagnostics.set(doc.uri, diags);
    });
  };

  context.subscriptions.push(
    vscode.workspace.onDidOpenTextDocument((doc) => {
      runCheck(doc);
      void learnFromDocument(context, doc);
    }),
    vscode.workspace.onDidSaveTextDocument((doc) => {
      runCheck(doc);
      void learnFromDocument(context, doc);
    }),
    vscode.window.onDidChangeActiveTextEditor((ed) => {
      if (!ed) return;
      runCheck(ed.document);
      void learnFromDocument(context, ed.document);
    }),
    diagnostics
  );

  const initialDoc = vscode.window.activeTextEditor?.document;
  if (initialDoc) {
    runCheck(initialDoc);
    void learnFromDocument(context, initialDoc);
  }

  const completionProvider = vscode.languages.registerCompletionItemProvider(
    "babashell",
    {
      provideCompletionItems(document, position) {
        const items: vscode.CompletionItem[] = [];
        const dedupe = new Set<string>();
        const library = context.globalState.get<LibrarySnapshot>(LIBRARY_STATE_KEY) ?? createEmptyLibrary();

        const pushItem = (item: vscode.CompletionItem) => {
          const key = `${item.label.toString()}::${item.kind ?? 0}`;
          if (dedupe.has(key)) return;
          dedupe.add(key);
          items.push(item);
        };

        const linePrefix = document.lineAt(position.line).text.slice(0, position.character);
        const setSelectorContext = /^\s*set\s+([#.\w-]*)$/.test(linePrefix);
        const propertyContext = /^\s*set\s+("[^"]*"|#[A-Za-z0-9_-]+|\.[A-Za-z0-9_-]+|[A-Za-z_][A-Za-z0-9_-]*)\s+([A-Za-z-]*)$/.exec(linePrefix);
        const valueContext = /^\s*set\s+("[^"]*"|#[A-Za-z0-9_-]+|\.[A-Za-z0-9_-]+|[A-Za-z_][A-Za-z0-9_-]*)\s+([A-Za-z][A-Za-z0-9-]*)\s+["']?([A-Za-z0-9#().,%\s-]*)$/.exec(linePrefix);

        for (const kw of KEYWORDS) {
          const item = new vscode.CompletionItem(kw, vscode.CompletionItemKind.Keyword);
          pushItem(item);
        }

        for (const fn of BUILTINS) {
          const item = new vscode.CompletionItem(fn, vscode.CompletionItemKind.Function);
          pushItem(item);
        }

        for (const symbol of library.symbols) {
          const item = new vscode.CompletionItem(symbol, vscode.CompletionItemKind.Variable);
          item.detail = "Learned from your BabaShell files";
          pushItem(item);
        }

        if (setSelectorContext) {
          for (const selector of library.selectors) {
            const item = new vscode.CompletionItem(selector, vscode.CompletionItemKind.Reference);
            item.detail = "Learned selector";
            pushItem(item);
          }
        }

        if (propertyContext) {
          for (const prop of unique([...CSS_PROPERTIES, ...library.properties])) {
            const item = new vscode.CompletionItem(prop, vscode.CompletionItemKind.Property);
            item.detail = "CSS property";
            pushItem(item);
          }
        }

        if (valueContext) {
          const propName = valueContext[2].toLowerCase();
          const values = unique([...(CSS_VALUES_BY_PROPERTY[propName] ?? []), ...library.values]);
          for (const value of values) {
            const item = new vscode.CompletionItem(value, vscode.CompletionItemKind.Value);
            item.detail = `CSS value for ${propName}`;
            pushItem(item);
          }
        }

        const snippets: Array<{ label: string; body: vscode.SnippetString; detail: string }> = [
          {
            label: "func",
            detail: "Function snippet",
            body: new vscode.SnippetString("func ${1:name}(${2:param}) {\n    $0\n}")
          },
          {
            label: "if else",
            detail: "If/Else snippet",
            body: new vscode.SnippetString("if ${1:condition} {\n    $0\n} else {\n    \n}")
          },
          {
            label: "while",
            detail: "While loop",
            body: new vscode.SnippetString("while ${1:condition} {\n    $0\n}")
          },
          {
            label: "break",
            detail: "Break out of a loop",
            body: new vscode.SnippetString("break")
          },
          {
            label: "continue",
            detail: "Skip to the next loop iteration",
            body: new vscode.SnippetString("continue")
          },
          {
            label: "else if",
            detail: "Else-if branch",
            body: new vscode.SnippetString("else if ${1:condition} {\n    $0\n}")
          },
          {
            label: "try catch",
            detail: "Structured error handling",
            body: new vscode.SnippetString("try {\n    $1\n} catch ${2:err} {\n    $0\n}")
          },
          {
            label: "throw",
            detail: "Throw a script value",
            body: new vscode.SnippetString("throw ${1:\"Something went wrong\"}")
          },
          {
            label: "use from",
            detail: "Bind HTML file",
            body: new vscode.SnippetString("use from {./${1:index.html}}")
          },
          {
            label: "when clicked",
            detail: "Element click handler",
            body: new vscode.SnippetString("when #${1:id} clicked {\n    emit \"${2:Clicked}\"\n    $0\n}")
          },
          {
            label: "when hover",
            detail: "Element hover handler",
            body: new vscode.SnippetString("when #${1:id} hover {\n    set #${1:id} class \"${2:hovered}\"\n    $0\n}")
          },
          {
            label: "loop",
            detail: "Loop snippet",
            body: new vscode.SnippetString("loop ${1:i} = ${2:1}..${3:10} {\n    $0\n}")
          },
          {
            label: "set text",
            detail: "Set textContent",
            body: new vscode.SnippetString("set #${1:id} text \"${2:text}\"")
          },
          {
            label: "set attr",
            detail: "Set HTML attribute",
            body: new vscode.SnippetString("set #${1:id} ${2:src} \"${3:value}\"")
          },
          {
            label: "input",
            detail: "Ask the user for text input",
            body: new vscode.SnippetString("store ${1:name} = input(\"${2:What is your name? }\")")
          },
          {
            label: "math assign",
            detail: "Compound math assignment",
            body: new vscode.SnippetString("${1:score} ${2|+=,-=,*=,/=,%=|} ${3:1}")
          },
          {
            label: "confirm",
            detail: "Ask the user for yes/no confirmation",
            body: new vscode.SnippetString("if confirm(\"${1:Continue?}\") {\n    $0\n}")
          },
          {
            label: "ui.button",
            detail: "Create button from script",
            body: new vscode.SnippetString("ui.button \"#${1:id}\" \"${2:text}\"")
          },
          {
            label: "ui.div",
            detail: "Create div from script",
            body: new vscode.SnippetString("ui.div \"#${1:id}\"")
          }
        ];

        for (const sn of snippets) {
          const item = new vscode.CompletionItem(sn.label, vscode.CompletionItemKind.Snippet);
          item.insertText = sn.body;
          item.detail = sn.detail;
          pushItem(item);
        }

        return items;
      }
    },
    ...[".", "(", "#", "{", " "]
  );

  context.subscriptions.push(runCmd, completionProvider);
}

export function deactivate() {}
