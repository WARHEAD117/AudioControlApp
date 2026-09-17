using System.Text.Json;
using System.Text.Json.Serialization;
using AudioControlApp.App;
using AudioControlApp.Audio;

namespace AudioControlApp.Config;

public enum RuleTrigger
{
    /// <summary>The rule applies while a matching process is running.</summary>
    Running,

    /// <summary>The rule applies while a matching process owns the foreground window.</summary>
    Foreground,
}

public sealed class SwitchRule
{
    public bool Enabled { get; set; } = true;

    /// <summary>Executable name, e.g. "cyberpunk2077.exe". Wildcards * and ? are allowed.</summary>
    public string ProcessName { get; set; } = string.Empty;

    public RuleTrigger Trigger { get; set; } = RuleTrigger.Running;

    /// <summary>Channel mask to apply while the rule matches.</summary>
    public uint Layout { get; set; } = SpeakerLayout.FivePointOne;

    public string? Comment { get; set; }

    public SwitchRule Clone() => (SwitchRule)MemberwiseClone();
}

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>"auto", "en", "zh-Hans" or "zh-Hant".</summary>
    public string Language { get; set; } = "auto";

    /// <summary>Endpoint ID to control; null follows the Windows default output device.</summary>
    public string? DeviceId { get; set; }

    /// <summary>Last known friendly name of <see cref="DeviceId"/>, for display when the device is absent.</summary>
    public string? DeviceName { get; set; }

    /// <summary>Layouts (channel masks) shown in the tray menu.</summary>
    public List<uint> MenuLayouts { get; set; } = new() { SpeakerLayout.Stereo, SpeakerLayout.FivePointOne };

    public uint QuickToggleA { get; set; } = SpeakerLayout.Stereo;

    public uint QuickToggleB { get; set; } = SpeakerLayout.FivePointOne;

    public bool LeftClickToggles { get; set; } = true;

    public bool NotifyOnSwitch { get; set; } = true;

    public bool AutoSwitchEnabled { get; set; }

    public List<SwitchRule> Rules { get; set; } = new();

    /// <summary>Layout applied when no rule matches; null leaves the current layout alone.</summary>
    public uint? FallbackLayout { get; set; } = SpeakerLayout.Stereo;

    public int PollIntervalMs { get; set; } = 2000;

    /// <summary>Global hotkey that toggles between the quick-toggle layouts, e.g. "Ctrl+Alt+S".</summary>
    public string? ToggleHotkey { get; set; }

    public bool LogEnabled { get; set; } = true;

    [JsonIgnore]
    public static string SettingsDirectory => AppPaths.ConfigDirectory;

    [JsonIgnore]
    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings is not null)
                {
                    settings.Normalize();
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load settings, using defaults: " + ex.Message);
        }

        var defaults = new AppSettings();
        defaults.Normalize();
        return defaults;
    }

    public void Save()
    {
        try
        {
            Normalize();
            Directory.CreateDirectory(SettingsDirectory);
            string tmp = SettingsPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOptions));
            File.Move(tmp, SettingsPath, overwrite: true);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to save settings: " + ex.Message);
        }
    }

    public void Normalize()
    {
        if (MenuLayouts.Count == 0)
        {
            MenuLayouts = new List<uint> { SpeakerLayout.Stereo, SpeakerLayout.FivePointOne };
        }

        MenuLayouts = MenuLayouts.Where(m => SpeakerLayout.ChannelCount(m) > 0).Distinct().ToList();

        if (SpeakerLayout.ChannelCount(QuickToggleA) == 0)
        {
            QuickToggleA = SpeakerLayout.Stereo;
        }

        if (SpeakerLayout.ChannelCount(QuickToggleB) == 0 || QuickToggleB == QuickToggleA)
        {
            QuickToggleB = QuickToggleA == SpeakerLayout.FivePointOne ? SpeakerLayout.Stereo : SpeakerLayout.FivePointOne;
        }

        PollIntervalMs = Math.Clamp(PollIntervalMs, 500, 60000);
        Rules ??= new List<SwitchRule>();
        Rules.RemoveAll(r => r is null);
        foreach (var rule in Rules)
        {
            rule.ProcessName = (rule.ProcessName ?? string.Empty).Trim();
        }

        Language = Language switch
        {
            "en" or "zh-Hans" or "zh-Hant" => Language,
            _ => "auto",
        };
    }

    public AppSettings Clone()
    {
        string json = JsonSerializer.Serialize(this, JsonOptions);
        var copy = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)!;
        copy.Normalize();
        return copy;
    }
}
