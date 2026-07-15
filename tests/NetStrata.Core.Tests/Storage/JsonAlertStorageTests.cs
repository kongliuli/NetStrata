using NetStrata.Core.Models;
using NetStrata.Core.Storage;

namespace NetStrata.Core.Tests.Storage;

public sealed class JsonAlertStorageTests
{
    [Fact]
    public async Task Append_Then_ReadTail_RoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netstrata-alerts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new JsonAlertStorage(dir, maxLines: 50);
            await store.AppendAsync(
            [
                new Alert
                {
                    T = "2026-07-15T06:00:00Z",
                    Type = "egress_changed",
                    Message = "proxy egress a → b",
                    Prev = "1.1.1.1",
                    Curr = "2.2.2.2"
                }
            ]);

            var tail = await store.ReadTailAsync(10);
            Assert.Single(tail);
            Assert.Equal("egress_changed", tail[0].Type);
            Assert.Equal("1.1.1.1", tail[0].Prev);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }
}
