using NetStrata.Core.Tray;

namespace NetStrata.Core.Tests.Tray;

public sealed class StartupShortcutServiceTests
{
    [Fact]
    public void IsEnabled_ReflectsWriter()
    {
        var writer = new FakeWriter { ExistsResult = true };
        var svc = new StartupShortcutService(writer, () => @"C:\Startup");
        Assert.True(svc.IsEnabled());
        Assert.EndsWith("NetStrata Tray.lnk", svc.ShortcutPath);
    }

    [Fact]
    public void Enable_CreatesShortcut()
    {
        var writer = new FakeWriter();
        var svc = new StartupShortcutService(writer, () => @"C:\Startup");
        svc.Enable(@"C:\Apps\NetStrata.exe");
        Assert.Equal(@"C:\Startup\NetStrata Tray.lnk", writer.CreatedPath);
        Assert.Equal(@"C:\Apps\NetStrata.exe", writer.TargetPath);
    }

    [Fact]
    public void Disable_RemovesShortcut()
    {
        var writer = new FakeWriter { ExistsResult = true };
        var svc = new StartupShortcutService(writer, () => @"C:\Startup");
        svc.Disable();
        Assert.Equal(@"C:\Startup\NetStrata Tray.lnk", writer.DeletedPath);
    }

    private sealed class FakeWriter : IStartupLinkWriter
    {
        public bool ExistsResult;
        public string? CreatedPath;
        public string? TargetPath;
        public string? DeletedPath;

        public bool Exists(string shortcutPath) => ExistsResult;

        public void Create(string shortcutPath, string targetPath, string workingDirectory)
        {
            CreatedPath = shortcutPath;
            TargetPath = targetPath;
        }

        public void Delete(string shortcutPath) => DeletedPath = shortcutPath;
    }
}
