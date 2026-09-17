using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using AudioControlApp.Audio;
using Microsoft.Win32;

namespace AudioControlApp.App;

/// <summary>
/// Draws the tray icon at runtime: the layout label ("2.0", "5.1", "7.1" …) in the colour that matches the
/// taskbar theme, with a small accent bar for surround layouts. No icon files are needed.
/// </summary>
public static class TrayIconRenderer
{
    private static readonly Color SurroundAccent = Color.FromArgb(0x3C, 0xC3, 0x8A);
    private static readonly Color ErrorAccent = Color.FromArgb(0xE5, 0x48, 0x4D);
    private static string? _fontFamily;

    public static bool IsTaskbarLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int v && v != 0;
        }
        catch
        {
            return false;
        }
    }

    public static Icon Render(uint? mask, bool error = false)
    {
        string label = mask is null ? "–" : SpeakerLayout.ShortLabel(mask.Value);
        bool surround = mask is not null && SpeakerLayout.ChannelCount(mask.Value) > 2;
        int size = Math.Max(16, SystemInformation.SmallIconSize.Width);
        bool light = IsTaskbarLight();
        Color fore = light ? Color.FromArgb(0x1F, 0x1F, 0x1F) : Color.White;

        using var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            int barHeight = Math.Max(2, size / 8);
            int textArea = surround || error ? size - barHeight - 1 : size;

            using var font = FitFont(g, label, size, textArea);
            var format = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip,
            };

            using var brush = new SolidBrush(fore);
            var rect = new RectangleF(0, -size * 0.02f, size, textArea);
            g.DrawString(label, font, brush, rect, format);

            if (surround || error)
            {
                using var accent = new SolidBrush(error ? ErrorAccent : SurroundAccent);
                int inset = size / 5;
                g.FillRectangle(accent, inset, size - barHeight, size - inset * 2, barHeight);
            }
        }

        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static Font FitFont(Graphics g, string text, int size, int heightBudget)
    {
        string family = ResolveFontFamily();
        float emSize = size * 0.95f;
        while (emSize > 4)
        {
            var font = new Font(family, emSize, FontStyle.Bold, GraphicsUnit.Pixel);
            SizeF measured = g.MeasureString(text, font, int.MaxValue, StringFormat.GenericTypographic);
            if (measured.Width <= size + 0.5f && measured.Height <= heightBudget + 1.5f)
            {
                return font;
            }

            font.Dispose();
            emSize -= 0.5f;
        }

        return new Font(family, 4, FontStyle.Bold, GraphicsUnit.Pixel);
    }

    private static string ResolveFontFamily()
    {
        if (_fontFamily is not null)
        {
            return _fontFamily;
        }

        foreach (string candidate in new[] { "Bahnschrift SemiBold Condensed", "Bahnschrift Condensed", "Arial Narrow", "Segoe UI" })
        {
            try
            {
                using var family = new FontFamily(candidate);
                if (family.IsStyleAvailable(FontStyle.Bold) || family.IsStyleAvailable(FontStyle.Regular))
                {
                    _fontFamily = candidate;
                    return candidate;
                }
            }
            catch
            {
                // not installed
            }
        }

        _fontFamily = FontFamily.GenericSansSerif.Name;
        return _fontFamily;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
