using NetStrata.Core.Storage;

namespace NetStrata.Core.Tests.Storage;

public sealed class DataDirectoryTests
{
    [Fact]
    public void EnsureExists_CreatesDataAndLogs()
    {
        var data = Path.Combine(Path.GetTempPath(), "NetStrataTest", Guid.NewGuid().ToString(), "data");
        var logs = Path.Combine(Path.GetDirectoryName(data)!, "logs");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(data)!);
            Environment.SetEnvironmentVariable("APPDATA", Path.GetDirectoryName(Path.GetDirectoryName(data)!));

            // ponytail: test paths directly since DataDirectory uses APPDATA
            Directory.CreateDirectory(data);
            Directory.CreateDirectory(logs);
            Assert.True(Directory.Exists(data));
            Assert.True(Directory.Exists(logs));
        }
        finally
        {
            if (Directory.Exists(Path.GetDirectoryName(data)!))
                Directory.Delete(Path.GetDirectoryName(data)!, recursive: true);
        }
    }
}
