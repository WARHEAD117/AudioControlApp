namespace AudioControlApp.UI;

/// <summary>Small factory helpers so code-built forms scale consistently under per-monitor DPI.</summary>
internal static class Ui
{
    /// <summary>
    /// A button sized from its text. Default control sizes are already DPI-scaled when the control is created,
    /// and the form's auto-scale pass would scale them again, so buttons must shrink to their preferred size.
    /// </summary>
    public static Button Button(string text, int minWidth = 0) => new()
    {
        Text = text,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(10, 3, 10, 3),
        MinimumSize = new Size(minWidth, 0),
        UseVisualStyleBackColor = true,
    };

    public static Label Hint(string text, Padding? margin = null) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
        Margin = margin ?? new Padding(3, 0, 3, 3),
    };
}
