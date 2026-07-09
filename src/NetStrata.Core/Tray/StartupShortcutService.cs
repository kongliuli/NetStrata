namespace NetStrata.Core.Tray;

public interface IStartupLinkWriter
{
    bool Exists(string shortcutPath);
    void Create(string shortcutPath, string targetPath, string workingDirectory);
    void Delete(string shortcutPath);
}

public sealed class StartupShortcutService
{
    public const string ShortcutFileName = "NetStrata Tray.lnk";

    private readonly IStartupLinkWriter _writer;
    private readonly Func<string> _startupFolder;

    public StartupShortcutService(
        IStartupLinkWriter writer,
        Func<string>? startupFolder = null)
    {
        _writer = writer;
        _startupFolder = startupFolder ?? (() =>
            Environment.GetFolderPath(Environment.SpecialFolder.Startup));
    }

    public string ShortcutPath => Path.Combine(_startupFolder(), ShortcutFileName);

    public bool IsEnabled() => _writer.Exists(ShortcutPath);

    public void Enable(string trayExecutablePath)
    {
        var dir = Path.GetDirectoryName(trayExecutablePath);
        if (string.IsNullOrEmpty(dir))
            throw new InvalidOperationException("invalid tray executable path");

        _writer.Create(ShortcutPath, trayExecutablePath, dir);
    }

    public void Disable()
    {
        if (_writer.Exists(ShortcutPath))
            _writer.Delete(ShortcutPath);
    }

    public void SetEnabled(bool enabled, string trayExecutablePath)
    {
        if (enabled)
            Enable(trayExecutablePath);
        else
            Disable();
    }
}
