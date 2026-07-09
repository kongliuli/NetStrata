using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetStrata.Core.Config;
using NetStrata.Core.Collector;
using NetStrata.Core.Judge;
using NetStrata.Core.Storage;
using NetStrata.Daemon;

namespace NetStrata.Cli;

public static class WebHostRunner
{
    public static async Task RunAsync(NetStrataOptions options, string[] args)
    {
        DataDirectory.EnsureExists();

        var storage = new JsonSampleStorage(options.DataDir);
        var seriesBuilder = new SeriesBuilder();
        var collector = new SampleCollector();

        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls($"http://localhost:{options.Port}");

        builder.Services.AddSingleton<ISampleStorage>(storage);
        builder.Services.AddSingleton(storage);
        builder.Services.AddSingleton(seriesBuilder);
        builder.Services.AddSingleton(collector);
        builder.Services.AddSingleton(new ConclusionEngine());
        builder.Services.AddSingleton(new ReportExporter());
        builder.Services.AddSingleton(options);
        builder.Services.AddHostedService(sp => new ProbeDaemon(
            sp.GetRequiredService<SampleCollector>(),
            sp.GetRequiredService<ISampleStorage>(),
            sp.GetRequiredService<NetStrataOptions>()));

        var app = builder.Build();

        var webRoot = ResolveWebRoot();
        if (Directory.Exists(webRoot))
            app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot) });

        app.MapGet("/", () => Results.Redirect("/index.html"));
        app.MapGet("/api/state", async (ISampleStorage s, CancellationToken ct) =>
        {
            var state = await s.ReadStateAsync(ct);
            return state is null ? Results.Json(new { error = "no state yet" }, statusCode: 503) : Results.Json(state);
        });
        app.MapGet("/api/samples", async (int? limit, ISampleStorage s, CancellationToken ct) =>
        {
            var samples = await s.ReadTailAsync(limit ?? 240, ct);
            return Results.Json(new { count = samples.Count, samples });
        });
        app.MapGet("/api/series", async (int? limit, ISampleStorage s, SeriesBuilder b, CancellationToken ct) =>
        {
            var samples = await s.ReadTailAsync(limit ?? 240, ct);
            return Results.Json(b.Build(samples));
        });
        app.MapGet("/api/conclusions", async (ISampleStorage s, ConclusionEngine engine, CancellationToken ct) =>
        {
            var cached = await s.ReadConclusionsAsync(ct);
            if (!string.IsNullOrWhiteSpace(cached))
                return Results.Text(cached, "text/markdown");

            var samples = await s.ReadTailAsync(60, ct);
            return Results.Text(engine.GenerateMarkdown(samples), "text/markdown");
        });
        app.MapGet("/api/export", async (
            int? minutes,
            string? format,
            ISampleStorage s,
            ReportExporter exporter,
            ConclusionEngine engine,
            CancellationToken ct) =>
        {
            var window = minutes ?? 60;
            var samples = await s.ReadTailAsync(Math.Max(240, window * 2), ct);
            var state = await s.ReadStateAsync(ct);
            var conclusions = await s.ReadConclusionsAsync(ct) ?? engine.GenerateMarkdown(samples);
            var report = exporter.Build(samples, state?.RecentAlerts ?? [], conclusions, window);

            return string.Equals(format, "markdown", StringComparison.OrdinalIgnoreCase)
                ? Results.Text(exporter.ToMarkdown(report), "text/markdown")
                : Results.Text(exporter.ToJson(report), "application/json");
        });

        var url = $"http://localhost:{options.Port}";
        Console.WriteLine($"dashboard: {url}");

        if (!options.NoOpen)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch { /* headless */ }
            });
        }

        await app.RunAsync();
    }

    private static string ResolveWebRoot()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "web"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "web")),
            Path.GetFullPath("web")
        };
        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[^1];
    }
}
