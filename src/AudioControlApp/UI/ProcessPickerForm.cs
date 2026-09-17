using System.Diagnostics;
using AudioControlApp.Localization;

namespace AudioControlApp.UI;

/// <summary>Lets the user pick a running process; processes with a visible window are listed first.</summary>
public sealed class ProcessPickerForm : Form
{
    private readonly ListView _list;
    private readonly CheckBox _showAll;
    private readonly Button _ok;

    public string? SelectedProcessName { get; private set; }

    public ProcessPickerForm()
    {
        SuspendLayout();
        Text = L.T("Picker_Title");
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        Font = SystemFonts.MessageBoxFont ?? Font;
        ClientSize = new Size(560, 420);
        MinimumSize = new Size(420, 300);

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
        };
        _list.Columns.Add(L.T("Picker_ColProcess"), 180);
        _list.Columns.Add(L.T("Picker_ColWindow"), 300);
        _list.Columns.Add("PID", 70);
        _list.DoubleClick += (_, _) => Accept();

        _showAll = new CheckBox { Text = L.T("Picker_ShowAll"), AutoSize = true, Anchor = AnchorStyles.Left };
        _showAll.CheckedChanged += (_, _) => Populate();

        var refresh = Ui.Button(L.T("Btn_Refresh"));
        refresh.Click += (_, _) => Populate();

        _ok = Ui.Button(L.T("Btn_OK"), 88);
        _ok.Enabled = false;
        _ok.Click += (_, _) => Accept();
        _list.SelectedIndexChanged += (_, _) => _ok.Enabled = _list.SelectedItems.Count > 0;
        var cancel = Ui.Button(L.T("Btn_Cancel"), 88);
        cancel.DialogResult = DialogResult.Cancel;

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(_ok);
        buttons.Controls.Add(refresh);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 2, Padding = new Padding(8, 4, 8, 8) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(_showAll, 0, 0);
        bottom.Controls.Add(buttons, 1, 0);

        var listHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 8, 8, 0) };
        listHost.Controls.Add(_list);

        Controls.Add(listHost);
        Controls.Add(bottom);
        AcceptButton = _ok;
        CancelButton = cancel;

        Load += (_, _) => Populate();

        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ResumeLayout(false);
        PerformLayout();
    }

    private void Populate()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        var withWindow = new List<ListViewItem>();
        var background = new List<ListViewItem>();

        foreach (Process p in Process.GetProcesses().OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase))
        {
            using (p)
            {
                string title;
                try
                {
                    title = p.MainWindowTitle;
                }
                catch
                {
                    title = string.Empty;
                }

                var item = new ListViewItem(p.ProcessName + ".exe");
                item.SubItems.Add(title);
                item.SubItems.Add(p.Id.ToString());
                if (!string.IsNullOrEmpty(title))
                {
                    withWindow.Add(item);
                }
                else if (_showAll.Checked)
                {
                    item.ForeColor = SystemColors.GrayText;
                    background.Add(item);
                }
            }
        }

        _list.Items.AddRange(withWindow.ToArray());
        _list.Items.AddRange(background.ToArray());
        _list.EndUpdate();
        _ok.Enabled = false;
    }

    private void Accept()
    {
        if (_list.SelectedItems.Count == 0)
        {
            return;
        }

        SelectedProcessName = _list.SelectedItems[0].Text;
        DialogResult = DialogResult.OK;
        Close();
    }
}
