using NetStrata.Core.Models;
using NetStrata.Core.Storage;
using NetStrata.Core.Tui;

namespace NetStrata.Core.Tui;

public sealed class TrayStateReader
{
    private readonly ISampleStorage _storage;

    public TrayStateReader(ISampleStorage? storage = null) =>
        _storage = storage ?? new JsonSampleStorage();

    public async Task<TrayIconState> ReadIconStateAsync(CancellationToken ct = default)
    {
        var state = await _storage.ReadStateAsync(ct);
        var icon = TrayStatusMapper.MapFromState(state);
        if (state?.Latest?.Verdict?.Headline is { } headline)
            return icon with { Tooltip = headline };
        return icon;
    }

    public async Task<DaemonState?> ReadStateAsync(CancellationToken ct = default) =>
        await _storage.ReadStateAsync(ct);
}
