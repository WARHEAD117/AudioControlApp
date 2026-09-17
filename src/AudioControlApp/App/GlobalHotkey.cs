using System.Runtime.InteropServices;
using System.Text;

namespace AudioControlApp.App;

/// <summary>A key combination such as Ctrl+Alt+S, parsed from / formatted to a string.</summary>
public readonly record struct HotkeySpec(Keys Key, bool Ctrl, bool Alt, bool Shift, bool Win)
{
    public bool IsValid => Key != Keys.None && Key != Keys.ControlKey && Key != Keys.ShiftKey && Key != Keys.Menu && Key != Keys.LWin && Key != Keys.RWin;

    public uint NativeModifiers =>
        (Alt ? 0x0001u : 0) | (Ctrl ? 0x0002u : 0) | (Shift ? 0x0004u : 0) | (Win ? 0x0008u : 0) | 0x4000u /* MOD_NOREPEAT */;

    public static HotkeySpec? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        bool ctrl = false, alt = false, shift = false, win = false;
        Keys key = Keys.None;
        foreach (string part in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    ctrl = true;
                    break;
                case "alt":
                    alt = true;
                    break;
                case "shift":
                    shift = true;
                    break;
                case "win":
                case "windows":
                    win = true;
                    break;
                default:
                    if (Enum.TryParse(part, ignoreCase: true, out Keys parsed))
                    {
                        key = parsed;
                    }
                    else if (part.Length == 1 && char.IsDigit(part[0]))
                    {
                        key = Keys.D0 + (part[0] - '0');
                    }

                    break;
            }
        }

        var spec = new HotkeySpec(key, ctrl, alt, shift, win);
        return spec.IsValid ? spec : null;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        if (Ctrl) sb.Append("Ctrl+");
        if (Alt) sb.Append("Alt+");
        if (Shift) sb.Append("Shift+");
        if (Win) sb.Append("Win+");
        sb.Append(KeyName(Key));
        return sb.ToString();
    }

    public static string KeyName(Keys key) => key switch
    {
        >= Keys.D0 and <= Keys.D9 => ((char)('0' + (key - Keys.D0))).ToString(),
        _ => key.ToString(),
    };
}

/// <summary>Registers a system-wide hotkey against a message-only window.</summary>
public sealed class GlobalHotkey : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 0x4143; // "AC"
    private bool _registered;

    public event EventHandler? Pressed;

    public GlobalHotkey()
    {
        CreateHandle(new CreateParams { Parent = new IntPtr(-3) /* HWND_MESSAGE */ });
    }

    public bool Register(HotkeySpec? spec)
    {
        Unregister();
        if (spec is null || !spec.Value.IsValid)
        {
            return true;
        }

        _registered = RegisterHotKey(Handle, HotkeyId, spec.Value.NativeModifiers, (uint)spec.Value.Key);
        if (!_registered)
        {
            Logger.Warn($"RegisterHotKey({spec}) failed: {Marshal.GetLastWin32Error()}");
        }

        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(Handle, HotkeyId);
            _registered = false;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && (int)m.WParam == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            return;
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        Unregister();
        DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
