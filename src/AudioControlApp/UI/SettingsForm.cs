using System.Diagnostics;
using AudioControlApp.App;
using AudioControlApp.Audio;
using AudioControlApp.Config;
using AudioControlApp.Localization;

namespace AudioControlApp.UI;

/// <summary>The settings window. Works on a copy of the settings and hands the result back through <see cref="SettingsApplied"/>.</summary>
public sealed class SettingsForm : Form
{
    private sealed class LayoutItem
    {
        public uint Mask { get; }
        public bool? Supported { get; set; }
        public LayoutItem(uint mask) => Mask = mask;

        public override string ToString()
        {
            string state = Supported switch
            {
                true => L.T("Layouts_Supported"),
                false => L.T("Layouts_Unsupported"),
                null => string.Empty,
            };
            return state.Length == 0 ? SpeakerLayout.Describe(Mask) : $"{SpeakerLayout.Describe(Mask)}   {state}";
        }
    }

    private readonly AppSettings _settings;
    private readonly AudioDeviceService _audio;
    private readonly List<uint> _layoutMasks = new();
    private List<AudioDeviceInfo> _devices = new();

    // General
    private readonly ComboBox _device = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Label _deviceStatus = new() { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(3, 6, 3, 3) };
    private readonly CheckBox _leftClick = new() { AutoSize = true };
    private readonly ComboBox _toggleA = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
    private readonly ComboBox _toggleB = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
    private readonly CheckBox _notify = new() { AutoSize = true };
    private readonly HotkeyTextBox _hotkey = new() { Width = 220 };
    private readonly CheckBox _startup = new() { AutoSize = true };
    private readonly Label _startupState = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly CheckBox _log = new() { AutoSize = true };

    // Auto switch
    private readonly CheckBox _autoEnabled = new() { AutoSize = true };
    private readonly ListView _rules = new() { View = View.Details, FullRowSelect = true, CheckBoxes = true, HideSelection = false, MultiSelect = false, Dock = DockStyle.Fill };
    private readonly ComboBox _fallback = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly NumericUpDown _interval = new() { Minimum = 0.5m, Maximum = 60, DecimalPlaces = 1, Increment = 0.5m, Width = 80 };
    private Button _editRule = null!, _removeRule = null!, _upRule = null!, _downRule = null!;

    // Layouts
    private readonly CheckedListBox _layouts = new() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
    private readonly Label _probeState = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly TextBox _customMask = new() { Width = 120 };

    public event EventHandler<AppSettings>? SettingsApplied;

    public SettingsForm(AppSettings settings, AudioDeviceService audio)
    {
        _settings = settings;
        _audio = audio;

        SuspendLayout();
        Text = L.T("Settings_Title");
        StartPosition = FormStartPosition.CenterScreen;
        Font = SystemFonts.MessageBoxFont ?? Font;
        MinimizeBox = false;
        ClientSize = new Size(660, 640);
        MinimumSize = new Size(600, 520);
        try
        {
            Icon = Icon.ExtractAssociatedIcon(AppPaths.ExecutablePath);
        }
        catch
        {
        }

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 6) };
        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildAutoTab());
        tabs.TabPages.Add(BuildLayoutsTab());
        _tabs = tabs;

        var ok = Ui.Button(L.T("Btn_OK"), 88);
        ok.Click += (_, _) => { if (Apply()) Close(); };
        var cancel = Ui.Button(L.T("Btn_Cancel"), 88);
        cancel.DialogResult = DialogResult.Cancel;
        cancel.Click += (_, _) => Close();
        var apply = Ui.Button(L.T("Btn_Apply"), 88);
        apply.Click += (_, _) => Apply();

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8, 6, 8, 8), WrapContents = false };
        buttons.Controls.Add(apply);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 8, 8, 0) };
        host.Controls.Add(tabs);

        Controls.Add(host);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;

        Load += (_, _) => LoadFromSettings();
        Shown += (_, _) =>
        {
            Logger.Info($"Settings window: DeviceDpi={DeviceDpi}, size={Size}");
            if (_requestedTab is int requested)
            {
                _tabs.SelectedIndex = requested;
                _requestedTab = null;
            }

            _tabs.SelectedTab?.Focus();
            BeginInvoke(new Action(ProbeSupport));
        };

        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ResumeLayout(false);
        PerformLayout();
    }

    private readonly TabControl _tabs;
    private int? _requestedTab;

    /// <summary>Selects a tab (0 = General, 1 = Automatic switching, 2 = Layouts).</summary>
    public void SelectTab(int? index)
    {
        if (index is null)
        {
            return;
        }

        int clamped = Math.Clamp(index.Value, 0, _tabs.TabCount - 1);
        if (Visible)
        {
            _tabs.SelectedIndex = clamped;
        }
        else
        {
            _requestedTab = clamped;
        }
    }

    // ------------------------------------------------------------------ tabs

    private TabPage BuildGeneralTab()
    {
        var page = new TabPage(L.T("Tab_General")) { AutoScroll = true, Padding = new Padding(8) };
        var stack = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Device
        var deviceGroup = NewGroup(L.T("Group_Device"));
        var deviceGrid = NewGrid(2);
        deviceGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        deviceGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var refresh = Ui.Button(L.T("Btn_Refresh"));
        refresh.Click += (_, _) => { PopulateDevices(); UpdateDeviceStatus(); ProbeSupport(); };
        deviceGrid.Controls.Add(_device, 0, 0);
        deviceGrid.Controls.Add(refresh, 1, 0);
        deviceGrid.Controls.Add(_deviceStatus, 0, 1);
        deviceGrid.SetColumnSpan(_deviceStatus, 2);
        _device.SelectedIndexChanged += (_, _) => { UpdateDeviceStatus(); ProbeSupport(); };
        deviceGroup.Controls.Add(deviceGrid);
        stack.Controls.Add(deviceGroup);

        // Tray
        var trayGroup = NewGroup(L.T("Group_Tray"));
        var trayGrid = NewGrid(1);
        trayGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _leftClick.Text = L.T("Tray_LeftClick");
        trayGrid.Controls.Add(_leftClick);
        var toggleRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(20, 0, 3, 3) };
        toggleRow.Controls.Add(_toggleA);
        toggleRow.Controls.Add(new Label { Text = L.T("Tray_And"), AutoSize = true, Margin = new Padding(6, 6, 6, 0) });
        toggleRow.Controls.Add(_toggleB);
        trayGrid.Controls.Add(toggleRow);
        _notify.Text = L.T("Tray_Notify");
        trayGrid.Controls.Add(_notify);
        var hotkeyRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        hotkeyRow.Controls.Add(new Label { Text = L.T("Tray_Hotkey"), AutoSize = true, Margin = new Padding(3, 6, 6, 0) });
        hotkeyRow.Controls.Add(_hotkey);
        trayGrid.Controls.Add(hotkeyRow);
        trayGrid.Controls.Add(new Label { Text = L.T("Tray_HotkeyHint"), AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(20, 0, 3, 3) });
        trayGroup.Controls.Add(trayGrid);
        stack.Controls.Add(trayGroup);

        // Startup
        var startupGroup = NewGroup(L.T("Group_Startup"));
        var startupGrid = NewGrid(1);
        startupGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _startup.Text = L.T("Startup_Enable");
        startupGrid.Controls.Add(_startup);
        _startupState.Margin = new Padding(20, 0, 3, 3);
        startupGrid.Controls.Add(_startupState);
        startupGroup.Controls.Add(startupGrid);
        stack.Controls.Add(startupGroup);

        // Misc
        var miscGroup = NewGroup(L.T("Group_Misc"));
        var miscGrid = NewGrid(1);
        miscGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var langRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        langRow.Controls.Add(new Label { Text = L.T("Misc_Language"), AutoSize = true, Margin = new Padding(3, 6, 6, 0) });
        foreach (var (_, name) in L.Languages)
        {
            _language.Items.Add(name);
        }

        langRow.Controls.Add(_language);
        miscGrid.Controls.Add(langRow);
        _log.Text = L.T("Misc_Log");
        var logRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        logRow.Controls.Add(_log);
        var openLog = new LinkLabel { Text = L.T("Misc_OpenLog"), AutoSize = true, Margin = new Padding(12, 4, 3, 3) };
        openLog.LinkClicked += (_, _) => OpenFolder(AppPaths.LogDirectory);
        logRow.Controls.Add(openLog);
        var openConfig = new LinkLabel { Text = L.T("Misc_OpenConfig"), AutoSize = true, Margin = new Padding(12, 4, 3, 3) };
        openConfig.LinkClicked += (_, _) => OpenFolder(AppPaths.ConfigDirectory);
        logRow.Controls.Add(openConfig);
        miscGrid.Controls.Add(logRow);
        miscGroup.Controls.Add(miscGrid);
        stack.Controls.Add(miscGroup);

        page.Controls.Add(stack);
        return page;
    }

    private TabPage BuildAutoTab()
    {
        var page = new TabPage(L.T("Tab_Auto")) { Padding = new Padding(8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _autoEnabled.Text = L.T("Auto_Enable");
        _autoEnabled.Font = new Font(Font, FontStyle.Bold);
        layout.Controls.Add(_autoEnabled, 0, 0);
        layout.SetColumnSpan(_autoEnabled, 2);

        var explain = new Label { Text = L.T("Auto_Explain"), AutoSize = true, ForeColor = SystemColors.GrayText, Dock = DockStyle.Fill, Margin = new Padding(3, 0, 3, 8) };
        layout.Controls.Add(explain, 0, 1);
        layout.SetColumnSpan(explain, 2);

        _rules.Columns.Add(L.T("Auto_ColProcess"), 200);
        _rules.Columns.Add(L.T("Auto_ColTrigger"), 130);
        _rules.Columns.Add(L.T("Auto_ColLayout"), 140);
        _rules.Columns.Add(L.T("Auto_ColComment"), 150);
        _rules.DoubleClick += (_, _) => EditSelectedRule();
        _rules.SelectedIndexChanged += (_, _) => UpdateRuleButtons();
        _rules.ItemChecked += (_, e) => { if (e.Item.Tag is SwitchRule r) r.Enabled = e.Item.Checked; };
        layout.Controls.Add(_rules, 0, 2);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(8, 0, 0, 0) };
        var add = NewSideButton(L.T("Btn_Add"));
        add.Click += (_, _) => AddRule();
        _editRule = NewSideButton(L.T("Btn_Edit"));
        _editRule.Click += (_, _) => EditSelectedRule();
        _removeRule = NewSideButton(L.T("Btn_Remove"));
        _removeRule.Click += (_, _) => RemoveSelectedRule();
        _upRule = NewSideButton(L.T("Btn_Up"));
        _upRule.Click += (_, _) => MoveSelectedRule(-1);
        _downRule = NewSideButton(L.T("Btn_Down"));
        _downRule.Click += (_, _) => MoveSelectedRule(1);
        buttons.Controls.AddRange(new Control[] { add, _editRule, _removeRule, _upRule, _downRule });
        layout.Controls.Add(buttons, 1, 2);

        var bottom = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill, Margin = new Padding(3, 8, 3, 3) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.Controls.Add(new Label { Text = L.T("Auto_Fallback"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 8, 3) }, 0, 0);
        var fallbackRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        fallbackRow.Controls.Add(_fallback);
        bottom.Controls.Add(fallbackRow, 1, 0);
        var intervalRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        intervalRow.Controls.Add(_interval);
        intervalRow.Controls.Add(new Label { Text = L.T("Auto_IntervalUnit"), AutoSize = true, Margin = new Padding(6, 6, 3, 0) });
        bottom.Controls.Add(new Label { Text = L.T("Auto_Interval"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 8, 3) }, 0, 1);
        bottom.Controls.Add(intervalRow, 1, 1);
        layout.Controls.Add(bottom, 0, 3);
        layout.SetColumnSpan(bottom, 2);

        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildLayoutsTab()
    {
        var page = new TabPage(L.T("Tab_Layouts")) { Padding = new Padding(8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var explain = new Label { Text = L.T("Layouts_Explain"), AutoSize = true, ForeColor = SystemColors.GrayText, Dock = DockStyle.Fill, Margin = new Padding(3, 0, 3, 8) };
        layout.Controls.Add(explain, 0, 0);
        layout.Controls.Add(_layouts, 0, 1);

        var probeRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
        var probe = Ui.Button(L.T("Layouts_Probe"));
        probe.Click += (_, _) => ProbeSupport();
        probeRow.Controls.Add(probe);
        _probeState.Margin = new Padding(8, 6, 3, 3);
        probeRow.Controls.Add(_probeState);
        layout.Controls.Add(probeRow, 0, 2);

        var customRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
        customRow.Controls.Add(new Label { Text = L.T("Layouts_CustomLabel"), AutoSize = true, Margin = new Padding(3, 6, 6, 0) });
        _customMask.PlaceholderText = "0x63F";
        customRow.Controls.Add(_customMask);
        var addCustom = Ui.Button(L.T("Layouts_AddCustom"));
        addCustom.Click += (_, _) => AddCustomLayout();
        customRow.Controls.Add(addCustom);
        layout.Controls.Add(customRow, 0, 3);

        page.Controls.Add(layout);
        return page;
    }

    // ------------------------------------------------------------------ helpers for building

    private static GroupBox NewGroup(string title) => new()
    {
        Text = title,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Dock = DockStyle.Top,
        Padding = new Padding(10, 6, 10, 8),
        Margin = new Padding(0, 0, 0, 10),
    };

    private static TableLayoutPanel NewGrid(int columns) => new()
    {
        ColumnCount = columns,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Dock = DockStyle.Top,
    };

    private static Button NewSideButton(string text)
    {
        var button = Ui.Button(text, 96);
        button.Margin = new Padding(0, 0, 0, 4);
        return button;
    }

    // ------------------------------------------------------------------ load / apply

    private void LoadFromSettings()
    {
        RebuildLayoutMasks();
        PopulateDevices();
        UpdateDeviceStatus();

        _leftClick.Checked = _settings.LeftClickToggles;
        PopulateLayoutCombo(_toggleA, _settings.QuickToggleA, allowNone: false);
        PopulateLayoutCombo(_toggleB, _settings.QuickToggleB, allowNone: false);
        _notify.Checked = _settings.NotifyOnSwitch;
        _hotkey.Spec = HotkeySpec.Parse(_settings.ToggleHotkey);

        StartupState state = StartupManager.GetState();
        _startup.Checked = state == StartupState.Enabled;
        _startupState.Text = state switch
        {
            StartupState.Enabled => L.T("Startup_State_Enabled"),
            StartupState.BlockedByUser => L.T("Startup_State_Blocked"),
            StartupState.StaleEntry => L.T("Startup_State_Stale"),
            _ => L.T("Startup_State_Disabled"),
        };

        _language.SelectedIndex = Math.Max(0, Array.FindIndex(L.Languages, l => l.Code == _settings.Language));
        _log.Checked = _settings.LogEnabled;

        _autoEnabled.Checked = _settings.AutoSwitchEnabled;
        _rules.Items.Clear();
        foreach (var rule in _settings.Rules)
        {
            _rules.Items.Add(CreateRuleItem(rule.Clone()));
        }

        UpdateRuleButtons();
        PopulateLayoutCombo(_fallback, _settings.FallbackLayout, allowNone: true);
        _interval.Value = Math.Clamp(_settings.PollIntervalMs / 1000m, _interval.Minimum, _interval.Maximum);

        PopulateLayoutList();
    }

    private bool Apply()
    {
        var checkedMasks = _layouts.CheckedItems.Cast<LayoutItem>().Select(i => i.Mask).ToList();
        if (checkedMasks.Count == 0)
        {
            MessageBox.Show(this, L.T("Layouts_NeedOne"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        _settings.DeviceId = _device.SelectedIndex <= 0 ? null : _devices[_device.SelectedIndex - 1].Id;
        _settings.DeviceName = _device.SelectedIndex <= 0 ? null : _devices[_device.SelectedIndex - 1].Name;
        _settings.LeftClickToggles = _leftClick.Checked;
        _settings.QuickToggleA = SelectedMask(_toggleA, allowNone: false) ?? SpeakerLayout.Stereo;
        _settings.QuickToggleB = SelectedMask(_toggleB, allowNone: false) ?? SpeakerLayout.FivePointOne;
        _settings.NotifyOnSwitch = _notify.Checked;
        _settings.ToggleHotkey = _hotkey.Spec?.ToString();
        _settings.Language = L.Languages[Math.Max(0, _language.SelectedIndex)].Code;
        _settings.LogEnabled = _log.Checked;

        _settings.AutoSwitchEnabled = _autoEnabled.Checked;
        _settings.Rules = _rules.Items.Cast<ListViewItem>().Select(i => (SwitchRule)i.Tag!).Select(r => { return r; }).ToList();
        _settings.FallbackLayout = SelectedMask(_fallback, allowNone: true);
        _settings.PollIntervalMs = (int)(_interval.Value * 1000);
        _settings.MenuLayouts = checkedMasks;

        try
        {
            if (_startup.Checked != StartupManager.IsEnabled)
            {
                StartupManager.SetEnabled(_startup.Checked);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, L.T("Msg_StartupFailed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        _settings.Normalize();
        SettingsApplied?.Invoke(this, _settings.Clone());
        LoadFromSettings();
        return true;
    }

    // ------------------------------------------------------------------ devices

    private void PopulateDevices()
    {
        _devices = _audio.GetRenderDevices().ToList();
        _device.BeginUpdate();
        _device.Items.Clear();
        var def = _devices.FirstOrDefault(d => d.IsDefault);
        _device.Items.Add(def is null ? L.T("Device_FollowDefault") : $"{L.T("Device_FollowDefault")}  ({def.Name})");
        foreach (var d in _devices)
        {
            _device.Items.Add(d.IsDefault ? $"{d.Name}  ★" : d.Name);
        }

        int index = 0;
        if (_settings.DeviceId is not null)
        {
            int found = _devices.FindIndex(d => d.Id == _settings.DeviceId);
            if (found >= 0)
            {
                index = found + 1;
            }
            else
            {
                _device.Items.Add($"{_settings.DeviceName ?? _settings.DeviceId}  ({L.T("Device_Missing")})");
                _devices.Add(new AudioDeviceInfo(_settings.DeviceId, _settings.DeviceName ?? _settings.DeviceId, false));
                index = _devices.Count;
            }
        }

        _device.SelectedIndex = index;
        _device.EndUpdate();
    }

    private string? SelectedDeviceId() => _device.SelectedIndex <= 0 ? null : _devices[_device.SelectedIndex - 1].Id;

    private void UpdateDeviceStatus()
    {
        DeviceStatus? status = _audio.GetStatus(SelectedDeviceId());
        if (status is null)
        {
            _deviceStatus.Text = L.T("Status_NoDevice");
            return;
        }

        string fallback = status.IsFallback ? "\n" + L.T("Status_FallbackSuffix") : string.Empty;
        _deviceStatus.Text = string.Format(L.T("Device_StatusFormat"), status.Name, status.Format.DescribeFormat(), SpeakerLayout.Describe(status.Mask))
                             + "\n" + string.Format(L.T("Status_Physical"), SpeakerLayout.Describe(status.PhysicalSpeakers)) + fallback;
    }

    // ------------------------------------------------------------------ layouts

    private void RebuildLayoutMasks()
    {
        _layoutMasks.Clear();
        foreach (var known in SpeakerLayout.Known)
        {
            _layoutMasks.Add(known.Mask);
        }

        foreach (uint mask in _settings.MenuLayouts.Concat(_settings.Rules.Select(r => r.Layout)).Append(_settings.QuickToggleA).Append(_settings.QuickToggleB))
        {
            if (SpeakerLayout.ChannelCount(mask) > 0 && !_layoutMasks.Contains(mask))
            {
                _layoutMasks.Add(mask);
            }
        }

        if (_settings.FallbackLayout is uint fb && SpeakerLayout.ChannelCount(fb) > 0 && !_layoutMasks.Contains(fb))
        {
            _layoutMasks.Add(fb);
        }
    }

    private void PopulateLayoutList()
    {
        var previous = _layouts.Items.Cast<LayoutItem>().ToDictionary(i => i.Mask, i => i.Supported);
        _layouts.BeginUpdate();
        _layouts.Items.Clear();
        foreach (uint mask in _layoutMasks)
        {
            var item = new LayoutItem(mask) { Supported = previous.TryGetValue(mask, out bool? s) ? s : null };
            _layouts.Items.Add(item, _settings.MenuLayouts.Contains(mask));
        }

        _layouts.EndUpdate();
    }

    private void PopulateLayoutCombo(ComboBox combo, uint? selected, bool allowNone)
    {
        combo.BeginUpdate();
        combo.Items.Clear();
        if (allowNone)
        {
            combo.Items.Add(L.T("Auto_FallbackNone"));
        }

        foreach (uint mask in _layoutMasks)
        {
            combo.Items.Add(SpeakerLayout.Describe(mask));
        }

        int index = selected is null ? -1 : _layoutMasks.IndexOf(selected.Value);
        combo.SelectedIndex = allowNone ? index + 1 : Math.Max(0, index);
        combo.EndUpdate();
    }

    private uint? SelectedMask(ComboBox combo, bool allowNone)
    {
        int index = combo.SelectedIndex;
        if (allowNone)
        {
            index--;
        }

        return index >= 0 && index < _layoutMasks.Count ? _layoutMasks[index] : null;
    }

    private void ProbeSupport()
    {
        if (IsDisposed)
        {
            return;
        }

        string? id = SelectedDeviceId() ?? _audio.GetStatus(null)?.Id;
        if (id is null)
        {
            _probeState.Text = L.T("Status_NoDevice");
            return;
        }

        _probeState.Text = L.T("Layouts_Probing");
        _probeState.Refresh();
        UseWaitCursor = true;
        try
        {
            var support = _audio.ProbeLayouts(id, _layoutMasks);
            for (int i = 0; i < _layouts.Items.Count; i++)
            {
                if (_layouts.Items[i] is LayoutItem item)
                {
                    item.Supported = support.TryGetValue(item.Mask, out bool ok) ? ok : null;
                    bool wasChecked = _layouts.GetItemChecked(i);
                    _layouts.Items[i] = item; // forces the text to refresh
                    _layouts.SetItemChecked(i, wasChecked);
                }
            }

            _probeState.Text = L.T("Layouts_ProbeDone");
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void AddCustomLayout()
    {
        uint? mask = SpeakerLayout.Parse(_customMask.Text);
        if (mask is null || SpeakerLayout.ChannelCount(mask.Value) == 0)
        {
            MessageBox.Show(this, L.T("Layouts_InvalidMask"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_layoutMasks.Contains(mask.Value))
        {
            _layoutMasks.Add(mask.Value);
            _layouts.Items.Add(new LayoutItem(mask.Value), true);
            PopulateLayoutCombo(_toggleA, SelectedMask(_toggleA, false), false);
            PopulateLayoutCombo(_toggleB, SelectedMask(_toggleB, false), false);
            PopulateLayoutCombo(_fallback, SelectedMask(_fallback, true), true);
        }

        _customMask.Clear();
    }

    // ------------------------------------------------------------------ rules

    private ListViewItem CreateRuleItem(SwitchRule rule)
    {
        var item = new ListViewItem(rule.ProcessName) { Tag = rule, Checked = rule.Enabled };
        item.SubItems.Add(L.T(rule.Trigger == RuleTrigger.Foreground ? "Trigger_Foreground" : "Trigger_Running"));
        item.SubItems.Add(SpeakerLayout.DisplayName(rule.Layout));
        item.SubItems.Add(rule.Comment ?? string.Empty);
        return item;
    }

    private void RefreshRuleItem(ListViewItem item)
    {
        var rule = (SwitchRule)item.Tag!;
        item.Text = rule.ProcessName;
        item.SubItems[1].Text = L.T(rule.Trigger == RuleTrigger.Foreground ? "Trigger_Foreground" : "Trigger_Running");
        item.SubItems[2].Text = SpeakerLayout.DisplayName(rule.Layout);
        item.SubItems[3].Text = rule.Comment ?? string.Empty;
        item.Checked = rule.Enabled;
    }

    private void AddRule()
    {
        var rule = new SwitchRule { Layout = _layoutMasks.Contains(SpeakerLayout.FivePointOne) ? SpeakerLayout.FivePointOne : _layoutMasks[0] };
        using var editor = new RuleEditorForm(rule, _layoutMasks);
        if (editor.ShowDialog(this) == DialogResult.OK)
        {
            var item = CreateRuleItem(rule);
            _rules.Items.Add(item);
            item.Selected = true;
            if (!_autoEnabled.Checked)
            {
                _autoEnabled.Checked = true;
            }
        }

        UpdateRuleButtons();
    }

    private void EditSelectedRule()
    {
        if (_rules.SelectedItems.Count == 0)
        {
            return;
        }

        var item = _rules.SelectedItems[0];
        var rule = (SwitchRule)item.Tag!;
        using var editor = new RuleEditorForm(rule, _layoutMasks);
        if (editor.ShowDialog(this) == DialogResult.OK)
        {
            RefreshRuleItem(item);
        }
    }

    private void RemoveSelectedRule()
    {
        if (_rules.SelectedItems.Count == 0)
        {
            return;
        }

        _rules.Items.Remove(_rules.SelectedItems[0]);
        UpdateRuleButtons();
    }

    private void MoveSelectedRule(int delta)
    {
        if (_rules.SelectedItems.Count == 0)
        {
            return;
        }

        var item = _rules.SelectedItems[0];
        int index = item.Index;
        int target = index + delta;
        if (target < 0 || target >= _rules.Items.Count)
        {
            return;
        }

        _rules.Items.RemoveAt(index);
        _rules.Items.Insert(target, item);
        item.Selected = true;
        item.Focused = true;
        UpdateRuleButtons();
    }

    private void UpdateRuleButtons()
    {
        bool has = _rules.SelectedItems.Count > 0;
        int index = has ? _rules.SelectedItems[0].Index : -1;
        _editRule.Enabled = has;
        _removeRule.Enabled = has;
        _upRule.Enabled = has && index > 0;
        _downRule.Enabled = has && index < _rules.Items.Count - 1;
    }

    private static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Warn("Could not open folder: " + ex.Message);
        }
    }
}
