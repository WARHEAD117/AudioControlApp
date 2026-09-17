namespace AudioControlApp.App;

/// <summary>
/// Locations for settings and logs. Drop an empty file named <c>portable</c> next to the executable to keep
/// everything inside the application folder instead of the user profile.
/// </summary>
public static class AppPaths
{
    public const string AppName = "AudioControlApp";

    public static string ExecutablePath => Environment.ProcessPath ?? Application.ExecutablePath;

    public static string ExecutableDirectory => Path.GetDirectoryName(ExecutablePath) ?? AppContext.BaseDirectory;

    public static bool IsPortable => File.Exists(Path.Combine(ExecutableDirectory, "portable"));

    public static string ConfigDirectory => IsPortable
        ? ExecutableDirectory
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

    public static string LogDirectory => IsPortable
        ? ExecutableDirectory
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

    public static string LogPath => Path.Combine(LogDirectory, "app.log");
}
