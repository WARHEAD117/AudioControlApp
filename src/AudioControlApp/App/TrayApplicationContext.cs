using System.Diagnostics;
using AudioControlApp.Audio;
using AudioControlApp.Automation;
using AudioControlApp.Config;
using AudioControlApp.Localization;
using AudioControlApp.UI;
using Microsoft.Win32;

namespace AudioControlApp.App;

/// <summary>Owns the tray icon, its menu and the glue between settings, audio service and automation.</summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SingleInstance _instance;
    private readonly AudioDeviceService _audio;
    private readonly AutoSwitchEngine _engine;
    private readonly GlobalHotkey _hotkey;
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly System.Windows.Forms.Timer _clickTimer;
    private readonly System.Windows.Forms.Timer _debounceTimer;

    private readonly SynchronizationContext _sync;

    private AppSettings _settings;
    private DeviceStatus? _status;
    private SettingsForm? _settingsForm;
    private Icon? _icon;
    private bool _exiting;

    public TrayApplicationContext(AppSettings settings, SingleInstance instance, string[] args)
    {
        _settings = settings;
        _instance = instance;

        // Make sure a WinForms synchronization context exists before anything captures it.
        _sync = SynchronizationContext.Current as WindowsFormsSynchronizationContext ?? new WindowsFormsSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(_sync);

        _debounceTimer = new System.Windows.Forms.Timer { Interval = 400 };
        _debounceTimer.Tick += (_, _) => { _debounceTimer.Stop(); RefreshStatus(); };

        _audio = new AudioDeviceService();
        _audio.Changed += (_, _) => _debounceTimer.Start();

        _engine = new AutoSwitchEngine(_sync);
        _engine.DecisionChanged += OnAutoDecision;

        _hotkey = new GlobalHotkey();
        _hotkey.Pressed += (_, _) => ToggleQuick();

        _menu = new ContextMenuStrip();
        _menu.Opening += (_, _) => { RefreshStatus(); BuildMenu(); };

        _tray = new NotifyIcon
        {
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _tray.MouseClick += OnTrayMouseClick;
        _tray.MouseDoubleClick += OnTrayMouseDoubleClick;

        _clickTimer = new System.Windows.Forms.Timer { Interval = SystemInformation.DoubleClickTime };
        _clickTimer.Tick += (_, _) =>
        {
            _clickTimer.Stop();
            if (_settings.LeftClickToggles)
            {
                ToggleQuick();
            }
            else
            {
                ShowSettings();
            }
        };

        _pollTimer = new System.Windows.Forms.Timer { Interval = 10_000 };
        _pollTimer.Tick += (_, _) => RefreshStatus();
        _pollTimer.Start();

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _instance.CommandReceived += a => _sync.Post(_ => HandleCommand(a), null);

        RefreshStatus();
        ApplySettings(_settings, save: false);
        HandleCommand(args);
    }

    // ---------------------------------------------------------------- status & icon

    private void RefreshStatus()
    {
        if (_exiting)
        {
            return;
        }

        try
        {
            _status = _audio.GetStatus(_settings.DeviceId);
            if (_status is not null && !_status.IsFallback && _settings.DeviceId is not null && _settings.DeviceName != _status.Name)
            {
                _settings.DeviceName = _status.Name;
                _settings.Save();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("RefreshStatus failed: " + ex.Message);
            _status = null;
        }

        UpdateIcon();
    }

    private void UpdateIcon()
    {
        bool error = _status is null;
        Icon? old = _icon;
        _icon = TrayIconRenderer.Render(_status?.Mask, error);
        _tray.Icon = _icon;
        old?.Dispose();

        string text = _status is null
            ? $"{AppPaths.AppName}\n{L.T("Status_NoDevice")}"
            : $"{AppPaths.AppName}\n{_status.Name}\n{_status.Format.DescribeFormat()}";
        _tray.Text = text.Length > 127 ? text[..127] : text;
    }

    // ---------------------------------------------------------------- menu

    private void BuildMenu()
    {
        _menu.SuspendLayout();
        _menu.Items.Clear();

        var header = new ToolStripMenuItem(_status?.Name ?? L.T("Status_NoDevice")) { Enabled = false };
        _menu.Items.Add(header);
        if (_status is not null)
        {
            string fallback = _status.IsFallback ? " " + L.T("Status_FallbackSuffix") : string.Empty;
            _menu.Items.Add(new ToolStripMenuItem(string.Format(L.T("Menu_Current"), _status.Format.DescribeFormat()) + fallback) { Enabled = false });
        }

        _menu.Items.Add(new ToolStripSeparator());

        var layouts = new List<uint>(_settings.MenuLayouts);
        if (_status is not null && !layouts.Contains(_status.Mask))
        {
            layouts.Add(_status.Mask);
        }

        foreach (uint mask in layouts)
        {
            uint captured = mask;
            var item = new ToolStripMenuItem(SpeakerLayout.DisplayName(mask))
            {
                Checked = _status is not null && _status.Mask == mask,
                Enabled = _status is not null,
            };
            item.Click += (_, _) => ApplyLayout(captured, L.T("Reason_Manual"), notify: false);
            _menu.Items.Add(item);
        }

        _menu.Items.Add(new ToolStripSeparator());

        var auto = new ToolStripMenuItem(L.T("Menu_AutoSwitch")) { Checked = _settings.AutoSwitchEnabled, CheckOnClick = true };
        auto.Click += (_, _) =>
        {
            _settings.AutoSwitchEnabled = auto.Checked;
            _settings.Save();
            _engine.Configure(_settings);
        };
        _menu.Items.Add(auto);

        var startup = new ToolStripMenuItem(L.T("Menu_Startup")) { Checked = StartupManager.IsEnabled, CheckOnClick = true };
        startup.Click += (_, _) =>
        {
            try
            {
                StartupManager.SetEnabled(startup.Checked);
            }
            catch (Exception ex)
            {
                ShowBalloon(L.T("Msg_StartupFailed"), ex.Message, ToolTipIcon.Error);
            }
        };
        _menu.Items.Add(startup);

        var settingsItem = new ToolStripMenuItem(L.T("Menu_Settings"));
        settingsItem.Font = new Font(settingsItem.Font, FontStyle.Bold);
        settingsItem.Click += (_, _) => ShowSettings();
        _menu.Items.Add(settingsItem);

        var soundPanel = new ToolStripMenuItem(L.T("Menu_SoundPanel"));
        soundPanel.Click += (_, _) => OpenSoundControlPanel();
        _menu.Items.Add(soundPanel);

        _menu.Items.Add(new ToolStripSeparator());

        var exit = new ToolStripMenuItem(L.T("Menu_Exit"));
        exit.Click += (_, _) => ExitApplication();
        _menu.Items.Add(exit);

        _menu.ResumeLayout();
    }

    // ---------------------------------------------------------------- actions

    public void ApplyLayout(uint mask, string reason, bool notify)
    {
        RefreshStatus();
        if (_status is null)
        {
            ShowBalloon(L.T("Msg_SwitchFailed"), L.T("Status_NoDevice"), ToolTipIcon.Error);
            return;
        }

        if (_status.Mask == mask && _status.PhysicalSpeakers == mask)
        {
            Logger.Info($"Layout 0x{mask:X} already active ({reason}).");
            return;
        }

        SetLayoutResult result = _audio.SetLayout(_status.Id, mask);
        RefreshStatus();

        if (result.Success)
        {
            Logger.Info($"Switched to {SpeakerLayout.Describe(mask)} – {reason}");
            if (notify && _settings.NotifyOnSwitch)
            {
                ShowBalloon(string.Format(L.T("Msg_SwitchedTitle"), SpeakerLayout.DisplayName(mask)), $"{_status?.Name}\n{reason}", ToolTipIcon.Info);
            }
        }
        else
        {
            ShowBalloon(L.T("Msg_SwitchFailed"), result.Message, ToolTipIcon.Error);
        }
    }

    public void ToggleQuick()
    {
        RefreshStatus();
        uint target = _status is not null && _status.Mask == _settings.QuickToggleA ? _settings.QuickToggleB : _settings.QuickToggleA;
        ApplyLayout(target, L.T("Reason_Manual"), notify: true);
    }

    private void OnAutoDecision(AutoSwitchDecision decision)
    {
        if (_exiting)
        {
            return;
        }

        string reason = decision.Rule is null
            ? L.T("Reason_Fallback")
            : string.Format(L.T(decision.Rule.Trigger == RuleTrigger.Foreground ? "Reason_RuleForeground" : "Reason_RuleRunning"), decision.MatchedProcess ?? decision.Rule.ProcessName);
        ApplyLayout(decision.Layout, reason, notify: true);
    }

    public void ShowSettings(int? tab = null)
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.SelectTab(tab);
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(_settings.Clone(), _audio);
        _settingsForm.SettingsApplied += (_, s) => ApplySettings(s, save: true);
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
        _settingsForm.SelectTab(tab);
        _settingsForm.Activate();
    }

    private void ApplySettings(AppSettings settings, bool save)
    {
        bool languageChanged = settings.Language != _settings.Language;
        _settings = settings;
        Logger.Enabled = _settings.LogEnabled;
        if (save)
        {
            _settings.Save();
        }

        if (languageChanged)
        {
            L.Configure(_settings.Language);
        }

        _engine.Configure(_settings);

        HotkeySpec? spec = HotkeySpec.Parse(_settings.ToggleHotkey);
        if (!_hotkey.Register(spec) && spec is not null)
        {
            ShowBalloon(AppPaths.AppName, string.Format(L.T("Msg_HotkeyFailed"), spec), ToolTipIcon.Warning);
        }

        RefreshStatus();
    }

    private void HandleCommand(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i].ToLowerInvariant();
            switch (a)
            {
                case "--set" when i + 1 < args.Length:
                    uint? mask = SpeakerLayout.Parse(args[++i]);
                    if (mask is null)
                    {
                        ShowBalloon(AppPaths.AppName, string.Format(L.T("Msg_UnknownLayout"), args[i]), ToolTipIcon.Warning);
                    }
                    else
                    {
                        ApplyLayout(mask.Value, L.T("Reason_CommandLine"), notify: true);
                    }

                    break;
                case "--toggle":
                    ToggleQuick();
                    break;
                case "--auto" when i + 1 < args.Length:
                    bool on = args[++i].Equals("on", StringComparison.OrdinalIgnoreCase) || args[i] == "1";
                    _settings.AutoSwitchEnabled = on;
                    _settings.Save();
                    _engine.Configure(_settings);
                    break;
                case "--show":
                case "--settings":
                    int? tab = i + 1 < args.Length && int.TryParse(args[i + 1], out int t) ? t : null;
                    if (tab is not null)
                    {
                        i++;
                    }

                    ShowSettings(tab);
                    break;
                case "--exit":
                    ExitApplication();
                    return;
                case "--autostart":
                    Logger.Info("Started by the autostart entry.");
                    break;
            }
        }
    }

    private static void OpenSoundControlPanel()
    {
        try
        {
            Process.Start(new ProcessStartInfo("control.exe", "mmsys.cpl") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Warn("Could not open mmsys.cpl: " + ex.Message);
        }
    }

    private void ShowBalloon(string title, string text, ToolTipIcon icon)
    {
        try
        {
            _tray.BalloonTipTitle = title;
            _tray.BalloonTipText = string.IsNullOrWhiteSpace(text) ? " " : text;
            _tray.BalloonTipIcon = icon;
            _tray.ShowBalloonTip(3000);
        }
        catch (Exception ex)
        {
            Logger.Warn("Balloon failed: " + ex.Message);
        }
    }

    // ---------------------------------------------------------------- events

    private void OnTrayMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _clickTimer.Start();
        }
        else if (e.Button == MouseButtons.Middle)
        {
            ShowSettings();
        }
    }

    private void OnTrayMouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _clickTimer.Stop();
            ShowSettings();
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Color)
        {
            UpdateIcon();
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => UpdateIcon();

    private void ExitApplication()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        _settingsForm?.Close();
        _tray.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _pollTimer.Dispose();
            _clickTimer.Dispose();
            _debounceTimer.Dispose();
            _engine.Dispose();
            _hotkey.Dispose();
            _tray.Dispose();
            _menu.Dispose();
            _icon?.Dispose();
            _audio.Dispose();
        }

        base.Dispose(disposing);
    }
}
