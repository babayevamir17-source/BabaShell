import * as vscode from "vscode";
import * as fs from "fs";
import * as path from "path";
import { execFile } from "child_process";

const KEYWORDS = [
  "emit",
  "when",
  "use",
  "from",
  "set",
  "ui",
  "clicked",
  "hover",
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
];

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
      vscode.env.openExternal(vscode.Uri.parse(`http://localhost:${port}/`));
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
      const regex = /Syntax error \((\d+):(\d+)\):\s*(.+)/g;
      let match: RegExpExecArray | null;
      while ((match = regex.exec(output)) !== null) {
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
    vscode.workspace.onDidOpenTextDocument(runCheck),
    vscode.workspace.onDidSaveTextDocument(runCheck),
    vscode.window.onDidChangeActiveTextEditor((ed) => {
      if (ed) runCheck(ed.document);
    }),
    diagnostics
  );

  const completionProvider = vscode.languages.registerCompletionItemProvider(
    "babashell",
    {
      provideCompletionItems() {
        const items: vscode.CompletionItem[] = [];

        for (const kw of KEYWORDS) {
          const item = new vscode.CompletionItem(kw, vscode.CompletionItemKind.Keyword);
          items.push(item);
        }

        for (const fn of BUILTINS) {
          const item = new vscode.CompletionItem(fn, vscode.CompletionItemKind.Function);
          items.push(item);
        }

        const snippets: Array<{ label: string; body: vscode.SnippetString; detail: string }> = [
          {
            label: "func",
            detail: "Function snippet",
            body: new vscode.SnippetString("func ${1:name}(${2:param}) {\n    $0\n}")
          },
          {
            label: "when",
            detail: "When/Else snippet",
            body: new vscode.SnippetString("when ${1:condition} {\n    $0\n} else {\n    \n}")
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
          items.push(item);
        }

        return items;
      }
    },
    ...[".", "(", "#", "{", " "]
  );

  context.subscriptions.push(runCmd, completionProvider);
}

export function deactivate() {}
