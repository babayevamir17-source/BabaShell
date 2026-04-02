using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Reflection;

namespace BabaShell;

public static class BabaUpdater
{
    private const string Owner = "babayevamir17-source";
    private const string Repo  = "babashell";
    private const string ApiUrl = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";

    public static string CurrentVersion
    {
        get
        {
            var attr = typeof(BabaUpdater).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return attr?.InformationalVersion?.Split('+')[0] ?? "0.0.0";
        }
    }

    public static async Task<int> RunAsync()
    {
        WriteColored("Checking for updates...", ConsoleColor.Cyan);
        Console.WriteLine();

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("babashell-updater", CurrentVersion));

            var json = await http.GetStringAsync(ApiUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var latestVersion = tagName.TrimStart('v');
            var releaseUrl = root.GetProperty("html_url").GetString() ?? "";

            WriteColored($"Current version : v{CurrentVersion}", ConsoleColor.White);
            Console.WriteLine();
            WriteColored($"Latest version  : v{latestVersion}", ConsoleColor.White);
            Console.WriteLine();

            if (!IsNewer(latestVersion, CurrentVersion))
            {
                WriteColored("You are already up to date!", ConsoleColor.Green);
                Console.WriteLine();
                return 0;
            }

            WriteColored($"New version available: v{latestVersion}", ConsoleColor.Yellow);
            Console.WriteLine();

            // Find portable CLI exe asset (avoid installer)
            string? downloadUrl = null;
            string? assetName = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    var isExe = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
                    var isCli = name.Equals("babashell-win-x64.exe", StringComparison.OrdinalIgnoreCase) ||
                                name.Equals("babashell.exe", StringComparison.OrdinalIgnoreCase);
                    var isInstaller = name.Contains("setup", StringComparison.OrdinalIgnoreCase);
                    if (isExe && isCli && !isInstaller)
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        assetName = name;
                        break;
                    }
                }
            }

            if (downloadUrl == null)
            {
                WriteColored("No .exe asset found in the latest release.", ConsoleColor.Red);
                Console.WriteLine();
                WriteColored($"Visit: {releaseUrl}", ConsoleColor.Cyan);
                Console.WriteLine();
                return 1;
            }

            WriteColored($"Downloading: {assetName}", ConsoleColor.Cyan);
            Console.WriteLine();

            var exePath = Process.GetCurrentProcess().MainModule?.FileName
                ?? Path.Combine(AppContext.BaseDirectory, "babashell.exe");
            var backupPath = exePath + ".bak";
            var tempPath   = exePath + ".new";

            // Download to temp file
            var bytes = await http.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(tempPath, bytes);

            // Backup old exe, replace with new
            if (File.Exists(backupPath)) File.Delete(backupPath);
            File.Move(exePath, backupPath);
            File.Move(tempPath, exePath);

            WriteColored($"Updated to v{latestVersion} successfully!", ConsoleColor.Green);
            Console.WriteLine();
            WriteColored("Please restart babashell.", ConsoleColor.Cyan);
            Console.WriteLine();
            return 0;
        }
        catch (Exception ex)
        {
            WriteColored($"Update failed: {ex.Message}", ConsoleColor.Red);
            Console.WriteLine();
            return 1;
        }
    }

    private static bool IsNewer(string latest, string current)
    {
        if (Version.TryParse(latest, out var l) && Version.TryParse(current, out var c))
            return l > c;
        return string.Compare(latest, current, StringComparison.Ordinal) > 0;
    }

    private static void WriteColored(string text, ConsoleColor color)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = prev;
    }
}
