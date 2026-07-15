using System.Globalization;
using System.Text;
using System.Text.Json;
using NetStrata.Core.Models;

namespace NetStrata.Core.Storage;

public sealed class JsonSampleStorage : ISampleStorage
{
    public const int RetentionDays = 30;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        // PublishTrimmed disables reflection JSON; keep resolver explicit for Debug/Release.
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
    };

    private readonly string _dataDir;
    private readonly string _legacyJsonlPath;
    private readonly string _statePath;
    private readonly string _conclusionsPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _activeDay;

    public JsonSampleStorage(string? dataDir = null)
    {
        _dataDir = dataDir ?? DataDirectory.DataPath;
        Directory.CreateDirectory(_dataDir);
        _legacyJsonlPath = Path.Combine(_dataDir, "samples.jsonl");
        _statePath = Path.Combine(_dataDir, "state.json");
        _conclusionsPath = Path.Combine(_dataDir, "conclusions.md");
    }

    public string ConclusionsPath => _conclusionsPath;

    public async Task AppendAsync(Sample sample, CancellationToken ct)
    {
        var line = JsonSerializer.Serialize(sample, JsonOptions);
        await _lock.WaitAsync(ct);
        try
        {
            EnsureRolloverUnlocked(DateTime.UtcNow);
            await File.AppendAllTextAsync(TodayPathUnlocked(), line + Environment.NewLine, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<Sample>> ReadTailAsync(int limit, CancellationToken ct)
    {
        if (limit <= 0)
            return [];

        await _lock.WaitAsync(ct);
        try
        {
            EnsureRolloverUnlocked(DateTime.UtcNow);
            var files = EnumerateSampleFilesNewestFirstUnlocked().ToList();
            if (files.Count == 0)
                return [];

            var collected = new List<Sample>(Math.Min(limit, 256));
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                var chunk = await ReadLinesFromEndAsync(file, limit - collected.Count, ct);
                // chunk is newest-last within file; prepend so overall stays chronological
                collected.InsertRange(0, chunk);
                if (collected.Count >= limit)
                    break;
            }

            if (collected.Count > limit)
                collected = collected.TakeLast(limit).ToList();
            return collected;
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

    /// <summary>samples-yyyyMMdd.jsonl for UTC day.</summary>
    public static string DayFileName(DateTime utcDay) =>
        $"samples-{utcDay:yyyyMMdd}.jsonl";

    private string TodayPathUnlocked() =>
        Path.Combine(_dataDir, DayFileName(DateTime.UtcNow));

    private void EnsureRolloverUnlocked(DateTime utcNow)
    {
        var day = utcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        if (_activeDay == day)
            return;

        _activeDay = day;
        MigrateLegacyUnlocked();
        PurgeOldUnlocked(utcNow);
    }

    private void MigrateLegacyUnlocked()
    {
        if (!File.Exists(_legacyJsonlPath))
            return;
        try
        {
            var dest = TodayPathUnlocked();
            if (File.Exists(dest))
                File.AppendAllText(dest, File.ReadAllText(_legacyJsonlPath));
            else
                File.Move(_legacyJsonlPath, dest);
            if (File.Exists(_legacyJsonlPath))
                File.Delete(_legacyJsonlPath);
        }
        catch
        {
            // keep legacy file if migration fails
        }
    }

    private void PurgeOldUnlocked(DateTime utcNow)
    {
        var cutoff = utcNow.Date.AddDays(-RetentionDays);
        foreach (var path in Directory.EnumerateFiles(_dataDir, "samples-*.jsonl"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            // samples-yyyyMMdd => length 16
            if (name.Length != 16 || !name.StartsWith("samples-", StringComparison.Ordinal))
                continue;
            var stamp = name["samples-".Length..];
            if (!DateTime.TryParseExact(stamp, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var day))
                continue;
            if (day.Date < cutoff)
            {
                try { File.Delete(path); } catch { /* ignore */ }
            }
        }
    }

    private IEnumerable<string> EnumerateSampleFilesNewestFirstUnlocked()
    {
        var dated = Directory.EnumerateFiles(_dataDir, "samples-*.jsonl")
            .Select(p => (Path: p, Name: Path.GetFileName(p)))
            .OrderByDescending(x => x.Name, StringComparer.Ordinal)
            .Select(x => x.Path)
            .ToList();

        if (dated.Count > 0)
            return dated;

        if (File.Exists(_legacyJsonlPath))
            return [_legacyJsonlPath];
        return [];
    }

    /// <summary>Read up to <paramref name="maxLines"/> non-empty JSONL lines from the end of the file (oldest→newest).</summary>
    private static async Task<List<Sample>> ReadLinesFromEndAsync(string path, int maxLines, CancellationToken ct)
    {
        if (maxLines <= 0 || !File.Exists(path))
            return [];

        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (fs.Length == 0)
            return [];

        const int chunkSize = 64 * 1024;
        var buffer = new byte[chunkSize];
        var collected = new List<string>();
        var leftover = "";
        var pos = fs.Length;

        while (pos > 0 && collected.Count < maxLines)
        {
            ct.ThrowIfCancellationRequested();
            var toRead = (int)Math.Min(chunkSize, pos);
            pos -= toRead;
            fs.Position = pos;
            var read = await fs.ReadAsync(buffer.AsMemory(0, toRead), ct);
            var text = Encoding.UTF8.GetString(buffer, 0, read) + leftover;
            var parts = text.Split('\n');
            // parts[0] may be incomplete (start of chunk) unless pos==0
            leftover = pos > 0 ? parts[0] : "";
            var start = pos > 0 ? 1 : 0;
            for (var i = parts.Length - 1; i >= start; i--)
            {
                var line = parts[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                collected.Add(line);
                if (collected.Count >= maxLines)
                    break;
            }
        }

        if (pos == 0 && !string.IsNullOrWhiteSpace(leftover) && collected.Count < maxLines)
            collected.Add(leftover.TrimEnd('\r'));

        // collected is newest-first; reverse to oldest→newest
        collected.Reverse();
        var samples = new List<Sample>(collected.Count);
        foreach (var line in collected)
        {
            try
            {
                var s = JsonSerializer.Deserialize<Sample>(line, JsonOptions);
                if (s is not null)
                    samples.Add(s);
            }
            catch
            {
                // skip corrupt line
            }
        }
        return samples;
    }
}
