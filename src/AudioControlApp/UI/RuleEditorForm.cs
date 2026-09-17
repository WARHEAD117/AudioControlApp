using AudioControlApp.Audio;
using AudioControlApp.Config;
using AudioControlApp.Localization;

namespace AudioControlApp.UI;

/// <summary>Edits a single auto-switch rule.</summary>
public sealed class RuleEditorForm : Form
{
    private const int EditorWidth = 330;

    private readonly TextBox _process;
    private readonly ComboBox _trigger;
    private readonly ComboBox _layout;
    private readonly TextBox _comment;
    private readonly CheckBox _enabled;
    private readonly List<uint> _layoutMasks;

    public SwitchRule Rule { get; }

    public RuleEditorForm(SwitchRule rule, IEnumerable<uint> availableLayouts)
    {
        Rule = rule;
        SuspendLayout();
        Text = L.T("Rule_Title");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        Font = SystemFonts.MessageBoxFont ?? Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        _layoutMasks = availableLayouts.Distinct().ToList();
        if (!_layoutMasks.Contains(rule.Layout))
        {
            _layoutMasks.Add(rule.Layout);
        }

        var grid = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Padding = new Padding(12),
            Dock = DockStyle.Top,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // Process
        _process = new TextBox { Width = EditorWidth, Text = rule.ProcessName };
        var browse = Ui.Button(L.T("Rule_Browse"));
        browse.Click += (_, _) => BrowseExe();
        var running = Ui.Button(L.T("Rule_PickRunning"));
        running.Click += (_, _) => PickRunning();
        AddRow(grid, 0, L.T("Rule_Process"), _process, browse, running);

        var hint = Ui.Hint(L.T("Rule_ProcessHint"), new Padding(3, 0, 3, 8));
        hint.MaximumSize = new Size(EditorWidth + 200, 0);
        grid.Controls.Add(hint, 1, 1);

        // Trigger
        _trigger = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = EditorWidth };
        _trigger.Items.Add(L.T("Trigger_Running"));
        _trigger.Items.Add(L.T("Trigger_Foreground"));
        _trigger.SelectedIndex = rule.Trigger == RuleTrigger.Foreground ? 1 : 0;
        AddRow(grid, 2, L.T("Rule_Trigger"), _trigger);

        // Layout
        _layout = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = EditorWidth };
        foreach (uint mask in _layoutMasks)
        {
            _layout.Items.Add(SpeakerLayout.Describe(mask));
        }

        _layout.SelectedIndex = _layoutMasks.IndexOf(rule.Layout);
        AddRow(grid, 3, L.T("Rule_Layout"), _layout);

        // Comment
        _comment = new TextBox { Width = EditorWidth, Text = rule.Comment ?? string.Empty };
        AddRow(grid, 4, L.T("Rule_Comment"), _comment);

        // Enabled
        _enabled = new CheckBox { Text = L.T("Rule_Enabled"), AutoSize = true, Checked = rule.Enabled, Margin = new Padding(3, 8, 3, 3) };
        grid.Controls.Add(_enabled, 1, 5);

        // Buttons
        var ok = Ui.Button(L.T("Btn_OK"), 88);
        ok.Click += (_, _) => Accept();
        var cancel = Ui.Button(L.T("Btn_Cancel"), 88);
        cancel.DialogResult = DialogResult.Cancel;
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        Controls.Add(buttons);
        Controls.Add(grid);
        AcceptButton = ok;
        CancelButton = cancel;

        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ResumeLayout(false);
        PerformLayout();
    }

    /// <summary>Adds a label and a flow of editor controls; fixed widths inside a flow panel scale reliably.</summary>
    private static void AddRow(TableLayoutPanel grid, int row, string label, params Control[] editors)
    {
        var lbl = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 8, 3) };
        grid.Controls.Add(lbl, 0, row);
        var flow = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0) };
        flow.Controls.AddRange(editors);
        grid.Controls.Add(flow, 1, row);
    }

    private void BrowseExe()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Programs (*.exe)|*.exe|All files (*.*)|*.*",
            Title = L.T("Rule_Browse"),
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _process.Text = Path.GetFileName(dialog.FileName);
        }
    }

    private void PickRunning()
    {
        using var picker = new ProcessPickerForm();
        if (picker.ShowDialog(this) == DialogResult.OK && picker.SelectedProcessName is not null)
        {
            _process.Text = picker.SelectedProcessName;
        }
    }

    private void Accept()
    {
        string process = _process.Text.Trim();
        if (process.Length == 0)
        {
            MessageBox.Show(this, L.T("Rule_ProcessRequired"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _process.Focus();
            return;
        }

        Rule.ProcessName = process;
        Rule.Trigger = _trigger.SelectedIndex == 1 ? RuleTrigger.Foreground : RuleTrigger.Running;
        Rule.Layout = _layoutMasks[Math.Max(0, _layout.SelectedIndex)];
        Rule.Comment = string.IsNullOrWhiteSpace(_comment.Text) ? null : _comment.Text.Trim();
        Rule.Enabled = _enabled.Checked;
        DialogResult = DialogResult.OK;
        Close();
    }
}
