namespace NetStrata.Core.Tray;

/// <summary>Named event so a second NetStrata.exe can ask the first instance to show its window.</summary>
public static class ShowMainSignal
{
    public const string DefaultName = @"Local\NetStrata.ShowMain";

    public static EventWaitHandle CreateListener(string? name = null) =>
        new(false, EventResetMode.AutoReset, name ?? DefaultName);

    public static bool TrySignalExisting(string? name = null)
    {
        try
        {
            using var ev = EventWaitHandle.OpenExisting(name ?? DefaultName);
            return ev.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }
}
