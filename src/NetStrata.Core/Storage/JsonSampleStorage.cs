using System.Text.Json;
using NetStrata.Core.Models;

namespace NetStrata.Core.Storage;

public sealed class JsonSampleStorage : ISampleStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _jsonlPath;
    private readonly string _statePath;
    private readonly string _conclusionsPath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonSampleStorage(string? dataDir = null)
    {
        var dir = dataDir ?? DataDirectory.DataPath;
        Directory.CreateDirectory(dir);
        _jsonlPath = Path.Combine(dir, "samples.jsonl");
        _statePath = Path.Combine(dir, "state.json");
        _conclusionsPath = Path.Combine(dir, "conclusions.md");
    }

    public string ConclusionsPath => _conclusionsPath;

    public async Task AppendAsync(Sample sample, CancellationToken ct)
    {
        var line = JsonSerializer.Serialize(sample, JsonOptions);
        await _lock.WaitAsync(ct);
        try
        {
            await File.AppendAllTextAsync(_jsonlPath, line + Environment.NewLine, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<Sample>> ReadTailAsync(int limit, CancellationToken ct)
    {
        if (!File.Exists(_jsonlPath))
            return [];

        await _lock.WaitAsync(ct);
        try
        {
            var lines = await File.ReadAllLinesAsync(_jsonlPath, ct);
            return lines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .TakeLast(limit)
                .Select(l => JsonSerializer.Deserialize<Sample>(l, JsonOptions)!)
                .Where(s => s is not null)
                .ToList()!;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task WriteStateAsync(DaemonState state, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var json = JsonSerializer.Serialize(state, JsonOptions);
            await File.WriteAllTextAsync(_statePath, json, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<DaemonState?> ReadStateAsync(CancellationToken ct)
    {
        if (!File.Exists(_statePath))
            return null;

        await _lock.WaitAsync(ct);
        try
        {
            var json = await File.ReadAllTextAsync(_statePath, ct);
            return JsonSerializer.Deserialize<DaemonState>(json, JsonOptions);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task WriteConclusionsAsync(string markdown, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await File.WriteAllTextAsync(_conclusionsPath, markdown, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> ReadConclusionsAsync(CancellationToken ct)
    {
        if (!File.Exists(_conclusionsPath))
            return null;

        await _lock.WaitAsync(ct);
        try
        {
            return await File.ReadAllTextAsync(_conclusionsPath, ct);
        }
        finally
        {
            _lock.Release();
        }
    }
}
