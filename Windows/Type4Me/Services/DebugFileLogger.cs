using System.IO;

namespace Type4Me.Services;

/// <summary>
/// Simple append-only file logger with 256 KB rotation.
/// Thread-safe via lock. Writes to %AppData%\Type4Me\debug.log.
/// </summary>
public static class DebugFileLogger
{
    private static readonly object _lock = new();

    private static readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Type4Me");

    public static string LogFilePath => Path.Combine(_dir, "debug.log");

    private const int MaxSizeBytes = 256 * 1024;

    static DebugFileLogger()
    {
        Directory.CreateDirectory(_dir);
    }

    public static void StartSession()
    {
        lock (_lock)
        {
            RotateIfNeeded();
            Append($"--- session {Timestamp()} ---");
        }
    }

    public static void Log(string message)
    {
        lock (_lock)
        {
            Append($"[{Timestamp()}] {message}");
        }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            var info = new FileInfo(LogFilePath);
            if (info.Exists && info.Length > MaxSizeBytes)
                info.Delete();
        }
        catch { /* ignore */ }
    }

    private static void Append(string line)
    {
        try
        {
            File.AppendAllText(LogFilePath, line + Environment.NewLine);
        }
        catch { /* ignore */ }
    }

    private static string Timestamp() =>
        DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
}
