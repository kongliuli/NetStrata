namespace NetStrata.Core.Probes;

public interface IProbe<T>
{
    string Name { get; }
    Task<T> ProbeAsync(CancellationToken ct);
}
