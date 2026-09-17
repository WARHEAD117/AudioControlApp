using Microsoft.Win32;

namespace AudioControlApp.App;

public enum StartupState
{
    Disabled,
    Enabled,

    /// <summary>The Run entry exists but the user turned it off in Task Manager / Settings › Startup.</summary>
    BlockedByUser,

    /// <summary>The Run entry exists but points at another location.</summary>
    StaleEntry,
}

/// <summary>
/// Registers the application under HKCU\...\Run so it starts at logon. Because the new implementation does
/// not need elevation, a plain Run entry works (UAC silently drops Run entries that require elevation,
/// which is why the old version could not autostart reliably).
/// </summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = AppPaths.AppName;

    public static string ExpectedCommand => $"\"{AppPaths.ExecutablePath}\" --autostart";

    public static StartupState GetState()
    {
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            string? command = run?.GetValue(ValueName) as string;
            if (string.IsNullOrEmpty(command))
            {
                return StartupState.Disabled;
            }

            if (!CommandMatches(command))
            {
                return StartupState.StaleEntry;
            }

            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: false);
            if (approved?.GetValue(ValueName) is byte[] flags && flags.Length > 0 && (flags[0] & 0x01) != 0)
            {
                return StartupState.BlockedByUser;
            }

            return StartupState.Enabled;
        }
        catch (Exception ex)
        {
            Logger.Warn("Reading startup state failed: " + ex.Message);
            return StartupState.Disabled;
        }
    }

    public static bool IsEnabled => GetState() == StartupState.Enabled;

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var run = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (run is null)
            {
                return;
            }

            if (enabled)
            {
                run.SetValue(ValueName, ExpectedCommand, RegistryValueKind.String);

                // Re-enable if the user disabled it in Task Manager earlier.
                using var approved = Registry.CurrentUser.CreateSubKey(ApprovedKey, writable: true);
                if (approved?.GetValue(ValueName) is byte[] flags && flags.Length > 0 && (flags[0] & 0x01) != 0)
                {
                    var enabledFlags = new byte[12];
                    enabledFlags[0] = 0x02;
                    approved.SetValue(ValueName, enabledFlags, RegistryValueKind.Binary);
                }
            }
            else
            {
                run.DeleteValue(ValueName, throwOnMissingValue: false);
                using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
                approved?.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            Logger.Info($"Startup {(enabled ? "enabled" : "disabled")}.");
        }
        catch (Exception ex)
        {
            Logger.Error("Changing startup state failed: " + ex.Message);
            throw;
        }
    }

    private static bool CommandMatches(string command)
    {
        string exe = AppPaths.ExecutablePath;
        return command.Contains(exe, StringComparison.OrdinalIgnoreCase);
    }
}
