using System.Net.NetworkInformation;

namespace NetStrata.Core.Probes;

public sealed record PingSendResult(IPStatus Status, long RoundtripTime);

public interface IPingSender
{
    Task<PingSendResult> SendAsync(string target, int timeoutMs, CancellationToken ct);
}

public sealed class SystemPingSender : IPingSender
{
    public async Task<PingSendResult> SendAsync(string target, int timeoutMs, CancellationToken ct)
    {
        using var ping = new System.Net.NetworkInformation.Ping();
        var reply = await ping.SendPingAsync(target, timeoutMs).WaitAsync(ct);
        return new PingSendResult(reply.Status, reply.RoundtripTime);
    }
}
