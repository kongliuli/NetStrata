using System.Diagnostics;
using System.IO;
using NetStrata.Core.Tray;

namespace NetStrata.Tray.Services;

internal sealed class WindowsStartupLinkWriter : IStartupLinkWriter
{
    public bool Exists(string shortcutPath) => File.Exists(shortcutPath);

    public void Create(string shortcutPath, string targetPath, string workingDirectory)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"netstrata-startup-{Guid.NewGuid():N}.ps1");
        var script = $"""
            $link = (New-Object -ComObject WScript.Shell).CreateShortcut('{Escape(shortcutPath)}')
            $link.TargetPath = '{Escape(targetPath)}'
            $link.WorkingDirectory = '{Escape(workingDirectory)}'
            $link.Save()
            """;
        try
        {
            File.WriteAllText(scriptPath, script);
            using var process = Process.Start(new ProcessStartInfo(
                "powershell",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            })!;
            process.WaitForExit(5000);
            if (process.ExitCode != 0 || !File.Exists(shortcutPath))
                throw new InvalidOperationException("failed to create startup shortcut");
        }
        finally
        {
            if (File.Exists(scriptPath))
                File.Delete(scriptPath);
        }
    }

    public void Delete(string shortcutPath)
    {
        if (File.Exists(shortcutPath))
            File.Delete(shortcutPath);
    }

    private static string Escape(string value) => value.Replace("'", "''");
}
