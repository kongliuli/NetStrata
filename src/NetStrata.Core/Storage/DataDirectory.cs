namespace NetStrata.Core.Storage;

public static class DataDirectory
{
    public static string DefaultRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetStrata");

    public static string DataPath => Path.Combine(DefaultRoot, "data");
    public static string LogsPath => Path.Combine(DefaultRoot, "logs");
    public static string ConfigPath => Path.Combine(DefaultRoot, "config.json");

    public static void EnsureExists()
    {
        Directory.CreateDirectory(DataPath);
        Directory.CreateDirectory(LogsPath);
    }
}
