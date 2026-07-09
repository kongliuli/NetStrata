using NetStrata.Core.Models;
using NetStrata.Core.Storage;

namespace NetStrata.Core.Tests.Storage;

public sealed class ExportTests
{
    private readonly ReportExporter _exporter = new();
    private static readonly DateTime Now = new(2026, 7, 9, 5, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Export_LastHour_IncludesSamples()
    {
        var samples = Enumerable.Range(0, 5).Select(i => SampleAt(Now.AddMinutes(-i * 10))).ToList();
        var report = _exporter.Build(samples, [], null, 60, Now);

        Assert.True(report.SampleCount > 0);
        var md = _exporter.ToMarkdown(report);
        Assert.Contains("Overall", md);
        Assert.False(string.IsNullOrWhiteSpace(_exporter.ToJson(report)));
    }

    [Fact]
    public void Export_CustomPings_Included()
    {
        var sample = SampleAt(Now.AddMinutes(-5)) with
        {
            Pings =
            [
                new PingResult { Target = "192.168.1.50", Label = "nas", Custom = true, Ok = false, AvgMs = 12 },
                new PingResult { Target = "192.168.1.50", Label = "nas", Custom = true, Ok = true, AvgMs = 8 }
            ]
        };

        var report = _exporter.Build([sample], [], null, 60, Now);
        Assert.Single(report.CustomPings);
        Assert.Equal("nas", report.CustomPings[0].Label);
        Assert.Contains("nas", _exporter.ToMarkdown(report));
    }

    private static Sample SampleAt(DateTime t) => new()
    {
        T = t.ToString("o"),
        CycleMs = 100,
        ProxyConfig = new ProxyConfig(),
        Verdict = new Verdict
        {
            Overall = "ok",
            Headline = "all good",
            Ai = new AiVerdict { State = "ok", Headline = "ok" },
            Layers =
            [
                new LayerVerdict { Layer = "wifi", State = "ok" },
                new LayerVerdict { Layer = "proxy", State = "ok" }
            ]
        }
    };
}
