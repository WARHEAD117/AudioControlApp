using System.Runtime.InteropServices;
using AudioControlApp.App;
using AudioControlApp.Audio;
using AudioControlApp.Config;
using AudioControlApp.Localization;

namespace AudioControlApp;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var settings = AppSettings.Load();
        Logger.Enabled = settings.LogEnabled;
        L.Configure(settings.Language);

        if (HasFlag(args, "--help") || HasFlag(args, "-h") || HasFlag(args, "/?"))
        {
            WriteConsole(HelpText);
            return 0;
        }

        if (HasFlag(args, "--status"))
        {
            return PrintStatus(settings);
        }

        int previewIndex = Array.FindIndex(args, a => a.Equals("--icon-preview", StringComparison.OrdinalIgnoreCase));
        if (previewIndex >= 0 && previewIndex + 1 < args.Length)
        {
            return RenderIconPreview(args[previewIndex + 1]);
        }

        using var instance = new SingleInstance();
        if (!instance.IsFirstInstance)
        {
            string[] forwarded = args.Length == 0 ? new[] { "--show" } : args;
            bool delivered = SingleInstance.SendToRunningInstance(forwarded);
            if (!delivered)
            {
                MessageBox.Show(L.T("Msg_AlreadyRunning"), AppPaths.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return delivered ? 0 : 1;
        }

        Logger.Info($"Starting {AppPaths.AppName} {typeof(Program).Assembly.GetName().Version} ({string.Join(' ', args)})");

        Application.ThreadException += (_, e) => Logger.Error("Unhandled UI exception: " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Logger.Error("Unhandled exception: " + e.ExceptionObject);

        instance.StartServer();
        using var context = new TrayApplicationContext(settings, instance, args);
        Application.Run(context);
        Logger.Info("Exited.");
        return 0;
    }

    private static int PrintStatus(AppSettings settings)
    {
        using var audio = new AudioDeviceService();
        DeviceStatus? status = audio.GetStatus(settings.DeviceId);
        if (status is null)
        {
            WriteConsole("No active render device.");
            return 2;
        }

        var lines = new List<string>
        {
            $"Device:            {status.Name}",
            $"Device ID:         {status.Id}",
            $"Default device:    {status.IsDefault}",
            $"Engine format:     {status.Format}",
            $"Layout:            {SpeakerLayout.Describe(status.Mask)}",
            $"Physical speakers: 0x{status.PhysicalSpeakers:X} ({SpeakerLayout.ShortLabel(status.PhysicalSpeakers)})",
            $"Settings file:     {AppSettings.SettingsPath}",
            $"Log file:          {AppPaths.LogPath}",
        };

        WriteConsole(string.Join(Environment.NewLine, lines));
        return 0;
    }

    /// <summary>Debug helper: writes the tray icons for the known layouts (and the error icon) into a PNG strip.</summary>
    private static int RenderIconPreview(string path)
    {
        var masks = SpeakerLayout.Known.Select(k => (uint?)k.Mask).Append(null).ToList();
        int size = Math.Max(16, SystemInformation.SmallIconSize.Width);
        int scale = 4;
        using var strip = new Bitmap(masks.Count * (size + 4) * scale, (size + 4) * scale);
        using (var g = Graphics.FromImage(strip))
        {
            g.Clear(TrayIconRenderer.IsTaskbarLight() ? Color.FromArgb(0xF3, 0xF3, 0xF3) : Color.FromArgb(0x20, 0x20, 0x20));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            for (int i = 0; i < masks.Count; i++)
            {
                using Icon icon = TrayIconRenderer.Render(masks[i], error: masks[i] is null);
                using Bitmap bmp = icon.ToBitmap();
                g.DrawImage(bmp, new Rectangle((i * (size + 4) + 2) * scale, 2 * scale, size * scale, size * scale));
            }
        }

        strip.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        WriteConsole($"Icon preview ({size}px, x{scale}) written to {path}");
        return 0;
    }

    private static bool HasFlag(string[] args, string flag) => args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    private const string HelpText = """
        AudioControlApp – switch the Windows speaker layout from the tray.

        Usage: AudioControlApp.exe [option]

          (no option)        Start in the tray (or bring the running instance's settings window up).
          --set <layout>     Switch to a layout: 2.0 | 5.1 | 5.1side | 7.1 | 0x<mask>
          --toggle           Toggle between the two quick-toggle layouts.
          --auto on|off      Enable or disable automatic switching.
          --show             Open the settings window.
          --status           Print the current device and layout.
          --exit             Close the running instance.
          --autostart        Used by the Run registry entry; starts silently.
        """;

    private static void WriteConsole(string text)
    {
        if (AttachConsole(-1))
        {
            using var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            stdout.WriteLine();
            stdout.WriteLine(text);
            FreeConsole();
        }
        else
        {
            MessageBox.Show(text, AppPaths.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();
}
