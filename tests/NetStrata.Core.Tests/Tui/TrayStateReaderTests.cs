using NetStrata.Core.Models;
using NetStrata.Core.Storage;
using NetStrata.Core.Tui;

namespace NetStrata.Core.Tests.Tui;

public sealed class TrayStateReaderTests
{
    [Fact]
    public async Task ReadIconState_UsesHeadlineAsTooltip()
    {
        var storage = new FakeStorage(new DaemonState
        {
            StartedAt = "t",
            Cycle = 1,
            Latest = new Sample
            {
                T = "t",
                CycleMs = 1,
                ProxyConfig = new ProxyConfig(),
                Verdict = new Verdict
                {
                    Overall = "healthy",
                    Headline = "all green",
                    Ai = new AiVerdict { State = "ok", Headline = "ok" }
                }
            }
        });

        var reader = new TrayStateReader(storage);
        var icon = await reader.ReadIconStateAsync();
        Assert.Equal("green", icon.Color);
        Assert.Equal("all green", icon.Tooltip);
    }

    private sealed class FakeStorage(DaemonState? state) : ISampleStorage
    {
        public Task AppendAsync(Sample sample, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<Sample>> ReadTailAsync(int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Sample>>([]);
        public Task WriteStateAsync(DaemonState state, CancellationToken ct) => Task.CompletedTask;
        public Task<DaemonState?> ReadStateAsync(CancellationToken ct) => Task.FromResult(state);
        public Task WriteConclusionsAsync(string markdown, CancellationToken ct) => Task.CompletedTask;
        public Task<string?> ReadConclusionsAsync(CancellationToken ct) => Task.FromResult<string?>(null);
    }
}
