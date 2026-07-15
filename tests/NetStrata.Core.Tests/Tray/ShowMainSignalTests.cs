using NetStrata.Core.Tray;

namespace NetStrata.Core.Tests.Tray;

public sealed class ShowMainSignalTests
{
    [Fact]
    public void TrySignalExisting_WhenListenerArmed_SetsEvent()
    {
        var name = @"Local\NetStrata.ShowMain.Test." + Guid.NewGuid().ToString("N");
        using var listener = ShowMainSignal.CreateListener(name);
        Assert.True(ShowMainSignal.TrySignalExisting(name));
        Assert.True(listener.WaitOne(0));
    }

    [Fact]
    public void TrySignalExisting_WhenNoListener_ReturnsFalse()
    {
        var name = @"Local\NetStrata.ShowMain.Missing." + Guid.NewGuid().ToString("N");
        Assert.False(ShowMainSignal.TrySignalExisting(name));
    }
}
