using System.Text;

namespace AudioControlApp.App;

/// <summary>Tiny append-only file logger with size-based truncation.</summary>
public static class Logger
{
    private const long MaxBytes = 1024 * 1024;
    private static readonly object Gate = new();
    private static bool _enabled = true;

    public static bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public static void Info(string message) => Write("INFO ", message);

    public static void Warn(string message) => Write("WARN ", message);

    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        if (!_enabled)
        {
            return;
        }

        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.LogDirectory);
                var info = new FileInfo(AppPaths.LogPath);
                if (info.Exists && info.Length > MaxBytes)
                {
                    string old = AppPaths.LogPath + ".old";
                    File.Move(AppPaths.LogPath, old, overwrite: true);
                }

                File.AppendAllText(AppPaths.LogPath, line, Encoding.UTF8);
            }
            catch
            {
                // Logging must never take the application down.
            }
        }
    }
}
