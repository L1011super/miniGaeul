namespace GaeulDesktopPet.Services;

public static class LogService
{
    private static readonly object Gate = new();
    public static string LogDirectory { get; private set; } = "";
    private static string CurrentFile => Path.Combine(LogDirectory, $"gaeul-{DateTime.Now:yyyyMMdd}.log");

    public static void Initialize()
    {
        LogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GaeulDesktopPet", "logs");
        try
        {
            Directory.CreateDirectory(LogDirectory);
        }
        catch
        {
            LogDirectory = Path.Combine(Path.GetTempPath(), "GaeulDesktopPet", "logs");
            Directory.CreateDirectory(LogDirectory);
        }
        foreach (var file in Directory.GetFiles(LogDirectory, "gaeul-*.log").OrderByDescending(File.GetCreationTimeUtc).Skip(10))
        {
            Try(() => File.Delete(file));
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        if (string.IsNullOrWhiteSpace(LogDirectory)) Initialize();
        lock (Gate)
        {
            Try(() => File.AppendAllText(CurrentFile, $"{DateTimeOffset.Now:u} [{level}] {message}{Environment.NewLine}"));
        }
    }

    private static void Try(Action action)
    {
        try { action(); } catch { }
    }
}
