using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using Process = System.Diagnostics.Process;

namespace BabaShell.Vsix;

internal sealed class RunBabaShellCommand
{
    private readonly AsyncPackage _package;

    private RunBabaShellCommand(AsyncPackage package, OleMenuCommandService commandService)
    {
        _package = package;
        var cmdId = new CommandID(new Guid(PackageGuids.CommandSetString), PackageIds.RunBabaShellCommandId);
        var cmd = new OleMenuCommand(Execute, cmdId);
        commandService.AddCommand(cmd);
    }

    public static async Task InitializeAsync(AsyncPackage package)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
        if (await package.GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService mcs)
        {
            _ = new RunBabaShellCommand(package, mcs);
        }
    }

    private void Execute(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var dte = _package.GetServiceAsync(typeof(DTE)).Result as DTE2;
        if (dte == null) return;

        var doc = dte.ActiveDocument;
        if (doc == null || string.IsNullOrWhiteSpace(doc.FullName))
        {
            ShowMessage("No active .babashell file found.");
            return;
        }

        doc.Save();

        var filePath = doc.FullName;
        if (!filePath.EndsWith(".babashell", StringComparison.OrdinalIgnoreCase))
        {
            ShowMessage("This command only works for .babashell files.");
            return;
        }

        var solutionPath = dte.Solution?.FullName;
        var solutionDir = string.IsNullOrWhiteSpace(solutionPath)
            ? Path.GetDirectoryName(filePath) ?? ""
            : Path.GetDirectoryName(solutionPath) ?? "";

        var exePath = Path.Combine(solutionDir, "babaSHELL", "bin", "Release", "net8.0", "babaSHELL.exe");
        if (!File.Exists(exePath))
        {
            var debugPath = Path.Combine(solutionDir, "babaSHELL", "bin", "Debug", "net8.0", "babaSHELL.exe");
            exePath = File.Exists(debugPath) ? debugPath : exePath;
        }

        if (!File.Exists(exePath))
        {
            ShowMessage("babaSHELL.exe not found. Build the babaSHELL project in the solution first.");
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = '"' + filePath + '"',
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(filePath) ?? solutionDir
        };

        Process.Start(psi);
    }

    private void ShowMessage(string text)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        VsShellUtilities.ShowMessageBox(
            _package,
            text,
            "BabaShell",
            OLEMSGICON.OLEMSGICON_INFO,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }
}

