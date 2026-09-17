using System.Numerics;

namespace AudioControlApp.Audio;

/// <summary>
/// Well-known speaker channel masks (KSAUDIO_SPEAKER_*) and helpers to describe an arbitrary mask.
/// </summary>
public static class SpeakerLayout
{
    public const uint FrontLeft = 0x1;
    public const uint FrontRight = 0x2;
    public const uint FrontCenter = 0x4;
    public const uint LowFrequency = 0x8;
    public const uint BackLeft = 0x10;
    public const uint BackRight = 0x20;
    public const uint FrontLeftOfCenter = 0x40;
    public const uint FrontRightOfCenter = 0x80;
    public const uint BackCenter = 0x100;
    public const uint SideLeft = 0x200;
    public const uint SideRight = 0x400;

    public const uint Mono = FrontCenter;                                                   // 0x004
    public const uint Stereo = FrontLeft | FrontRight;                                      // 0x003
    public const uint Quad = Stereo | BackLeft | BackRight;                                 // 0x033
    public const uint Surround = Stereo | FrontCenter | BackCenter;                         // 0x107
    public const uint FivePointOne = Stereo | FrontCenter | LowFrequency | BackLeft | BackRight;   // 0x03F
    public const uint FivePointOneSurround = Stereo | FrontCenter | LowFrequency | SideLeft | SideRight; // 0x60F
    public const uint SevenPointOne = FivePointOne | SideLeft | SideRight;                  // 0x63F
    public const uint SevenPointOneWide = FivePointOne | FrontLeftOfCenter | FrontRightOfCenter; // 0x0FF

    public sealed record KnownLayout(uint Mask, string Key, string ShortLabel);

    /// <summary>Layouts offered in the UI, in display order.</summary>
    public static readonly IReadOnlyList<KnownLayout> Known = new[]
    {
        new KnownLayout(Mono, "Layout_Mono", "1.0"),
        new KnownLayout(Stereo, "Layout_Stereo", "2.0"),
        new KnownLayout(Quad, "Layout_Quad", "4.0"),
        new KnownLayout(Surround, "Layout_Surround", "4.0"),
        new KnownLayout(FivePointOne, "Layout_51", "5.1"),
        new KnownLayout(FivePointOneSurround, "Layout_51Side", "5.1"),
        new KnownLayout(SevenPointOne, "Layout_71", "7.1"),
        new KnownLayout(SevenPointOneWide, "Layout_71Wide", "7.1"),
    };

    public static int ChannelCount(uint mask) => BitOperations.PopCount(mask);

    public static bool HasLfe(uint mask) => (mask & LowFrequency) != 0;

    /// <summary>"2.0", "5.1", "7.1" … derived from the mask.</summary>
    public static string ShortLabel(uint mask)
    {
        var known = Known.FirstOrDefault(k => k.Mask == mask);
        if (known is not null)
        {
            return known.ShortLabel;
        }

        int channels = ChannelCount(mask);
        if (channels == 0)
        {
            return "—";
        }

        return HasLfe(mask) ? $"{channels - 1}.1" : $"{channels}.0";
    }

    /// <summary>Human readable name, localised through the string table.</summary>
    public static string DisplayName(uint mask)
    {
        var known = Known.FirstOrDefault(k => k.Mask == mask);
        if (known is not null)
        {
            return Localization.L.T(known.Key);
        }

        return string.Format(Localization.L.T("Layout_Custom"), ShortLabel(mask), mask);
    }

    public static string Describe(uint mask) => $"{DisplayName(mask)} (0x{mask:X})";

    /// <summary>Parses "5.1", "stereo", "0x3f", "63" … Returns null when it cannot be understood.</summary>
    public static uint? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string s = text.Trim().ToLowerInvariant();
        switch (s)
        {
            case "mono":
            case "1.0":
                return Mono;
            case "stereo":
            case "2.0":
            case "2":
                return Stereo;
            case "quad":
            case "4.0":
                return Quad;
            case "5.1":
            case "51":
                return FivePointOne;
            case "5.1side":
            case "5.1-side":
                return FivePointOneSurround;
            case "7.1":
            case "71":
                return SevenPointOne;
        }

        if (s.StartsWith("0x", StringComparison.Ordinal) &&
            uint.TryParse(s.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out uint hex))
        {
            return hex;
        }

        if (uint.TryParse(s, out uint dec))
        {
            return dec;
        }

        return null;
    }
}
