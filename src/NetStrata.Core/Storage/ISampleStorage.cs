using NetStrata.Core.Models;

namespace NetStrata.Core.Storage;

public interface ISampleStorage
{
    Task AppendAsync(Sample sample, CancellationToken ct);
    Task<IReadOnlyList<Sample>> ReadTailAsync(int limit, CancellationToken ct);
    Task WriteStateAsync(DaemonState state, CancellationToken ct);
    Task<DaemonState?> ReadStateAsync(CancellationToken ct);
    Task WriteConclusionsAsync(string markdown, CancellationToken ct);
    Task<string?> ReadConclusionsAsync(CancellationToken ct);
}
