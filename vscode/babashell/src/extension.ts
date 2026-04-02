import * as vscode from "vscode";
import * as fs from "fs";
import * as path from "path";

const KEYWORDS = [
  "emit",
  "when",
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
    terminal.sendText(`"${exePath}" "${filePath}"`);
  });

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
            label: "loop",
            detail: "Loop snippet",
            body: new vscode.SnippetString("loop ${1:i} = ${2:1}..${3:10} {\n    $0\n}")
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
    ...[".", "("]
  );

  context.subscriptions.push(runCmd, completionProvider);
}

export function deactivate() {}
