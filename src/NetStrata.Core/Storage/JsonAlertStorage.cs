using System.Text.Json;
using NetStrata.Core.Models;

namespace NetStrata.Core.Storage;

/// <summary>Append-only alert history under %APPDATA%\NetStrata\data\alerts.jsonl.</summary>
public sealed class JsonAlertStorage
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly string _path;
    private readonly int _maxLines;

    public JsonAlertStorage(string? dataDir = null, int maxLines = 500)
    {
        DataDirectory.EnsureExists();
        var root = dataDir ?? DataDirectory.DataPath;
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "alerts.jsonl");
        _maxLines = Math.Clamp(maxLines, 50, 5000);
    }

    public string PathName => _path;

    public async Task AppendAsync(IReadOnlyList<Alert> alerts, CancellationToken ct = default)
    {
        if (alerts.Count == 0)
            return;

        {
            await using var fs = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
            await using var writer = new StreamWriter(fs);
            foreach (var alert in alerts)
            {
                ct.ThrowIfCancellationRequested();
                await writer.WriteLineAsync(JsonSerializer.Serialize(alert, JsonOpts));
            }

            await writer.FlushAsync(ct);
        }

        // dispose append handle before trim re-opens the same path
        await TrimIfNeededAsync(ct);
    }

    public async Task<IReadOnlyList<Alert>> ReadTailAsync(int limit, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, _maxLines);
        if (!File.Exists(_path))
            return [];

        // ponytail: small file — read all lines; trim keeps it bounded
        var lines = await File.ReadAllLinesAsync(_path, ct);
        var list = new List<Alert>();
        foreach (var line in lines.TakeLast(limit))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            try
            {
                var a = JsonSerializer.Deserialize<Alert>(line, JsonOpts);
                if (a is not null)
                    list.Add(a);
            }
            catch (JsonException)
            {
                // skip corrupt line
            }
        }

        return list;
    }

    private async Task TrimIfNeededAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
            return;
        var lines = await File.ReadAllLinesAsync(_path, ct);
        if (lines.Length <= _maxLines)
            return;
        var keep = lines.TakeLast(_maxLines).ToArray();
        await File.WriteAllLinesAsync(_path, keep, ct);
    }
}
