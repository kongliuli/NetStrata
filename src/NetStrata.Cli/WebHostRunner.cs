using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetStrata.Core.Config;
using NetStrata.Core.Collector;
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
        app.MapGet("/api/conclusions", () => Results.Text("_(no conclusions yet)_\n", "text/markdown"));

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
