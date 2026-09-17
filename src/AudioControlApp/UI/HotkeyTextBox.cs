using AudioControlApp.App;
using AudioControlApp.Localization;

namespace AudioControlApp.UI;

/// <summary>A read-only text box that records the key combination pressed while it has focus.</summary>
public sealed class HotkeyTextBox : TextBox
{
    private HotkeySpec? _spec;

    public HotkeyTextBox()
    {
        ReadOnly = true;
        Cursor = Cursors.Hand;
        BackColor = SystemColors.Window;
        UpdateText();
    }

    public HotkeySpec? Spec
    {
        get => _spec;
        set
        {
            _spec = value;
            UpdateText();
        }
    }

    protected override bool IsInputKey(Keys keyData) => true;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;

        if (e.KeyCode is Keys.Back or Keys.Delete or Keys.Escape)
        {
            Spec = null;
            return;
        }

        bool win = (ModifierKeys & Keys.LWin) != 0 || (ModifierKeys & Keys.RWin) != 0;
        var spec = new HotkeySpec(e.KeyCode, e.Control, e.Alt, e.Shift, win);
        if (spec.IsValid && (e.Control || e.Alt || e.Shift || win || e.KeyCode is >= Keys.F1 and <= Keys.F24))
        {
            Spec = spec;
        }
    }

    private void UpdateText()
    {
        Text = _spec?.ToString() ?? L.T("Hotkey_None");
    }
}
