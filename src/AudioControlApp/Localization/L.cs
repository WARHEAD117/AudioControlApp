using System.Globalization;

namespace AudioControlApp.Localization;

/// <summary>Very small string table: English, Simplified Chinese and Traditional Chinese.</summary>
public static class L
{
    private static IReadOnlyDictionary<string, string> _table = Strings.English;

    public static string CurrentLanguage { get; private set; } = "en";

    public static readonly (string Code, string Name)[] Languages =
    {
        ("auto", "Auto / 自动 / 自動"),
        ("en", "English"),
        ("zh-Hans", "简体中文"),
        ("zh-Hant", "繁體中文"),
    };

    public static void Configure(string? language)
    {
        string code = language is "en" or "zh-Hans" or "zh-Hant" ? language : DetectSystemLanguage();
        CurrentLanguage = code;
        _table = code switch
        {
            "zh-Hans" => Strings.SimplifiedChinese,
            "zh-Hant" => Strings.TraditionalChinese,
            _ => Strings.English,
        };
    }

    public static string DetectSystemLanguage()
    {
        CultureInfo ui = CultureInfo.CurrentUICulture;
        if (!ui.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return "en";
        }

        string name = ui.Name;
        bool traditional = name.Contains("Hant", StringComparison.OrdinalIgnoreCase)
                           || name.EndsWith("-TW", StringComparison.OrdinalIgnoreCase)
                           || name.EndsWith("-HK", StringComparison.OrdinalIgnoreCase)
                           || name.EndsWith("-MO", StringComparison.OrdinalIgnoreCase);
        return traditional ? "zh-Hant" : "zh-Hans";
    }

    /// <summary>Returns the translation, falling back to English and finally to the key itself.</summary>
    public static string T(string key)
    {
        if (_table.TryGetValue(key, out string? value))
        {
            return value;
        }

        return Strings.English.TryGetValue(key, out string? english) ? english : key;
    }
}
