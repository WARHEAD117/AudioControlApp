using System.Runtime.InteropServices;

namespace AudioControlApp.Audio;

/// <summary>
/// A managed copy of a WAVEFORMATEX / WAVEFORMATEXTENSIBLE structure with helpers to
/// read it from unmanaged memory and to serialise it back as WAVEFORMATEXTENSIBLE.
/// </summary>
public sealed record WaveFormatInfo(
    ushort FormatTag,
    ushort Channels,
    uint SampleRate,
    ushort BitsPerSample,
    ushort ValidBitsPerSample,
    uint ChannelMask,
    Guid SubFormat)
{
    public const ushort WAVE_FORMAT_PCM = 0x0001;
    public const ushort WAVE_FORMAT_IEEE_FLOAT = 0x0003;
    public const ushort WAVE_FORMAT_EXTENSIBLE = 0xFFFE;
    public const int ExtensibleSize = 40;

    public static readonly Guid SubFormatPcm = new("00000001-0000-0010-8000-00aa00389b71");
    public static readonly Guid SubFormatIeeeFloat = new("00000003-0000-0010-8000-00aa00389b71");

    public ushort BlockAlign => (ushort)(Channels * BitsPerSample / 8);
    public uint AvgBytesPerSec => SampleRate * BlockAlign;
    public bool IsFloat => SubFormat == SubFormatIeeeFloat || FormatTag == WAVE_FORMAT_IEEE_FLOAT;

    public static WaveFormatInfo FromPointer(IntPtr p)
    {
        if (p == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(p));
        }

        ushort tag = (ushort)Marshal.ReadInt16(p, 0);
        ushort channels = (ushort)Marshal.ReadInt16(p, 2);
        uint rate = (uint)Marshal.ReadInt32(p, 4);
        ushort bits = (ushort)Marshal.ReadInt16(p, 14);
        ushort cbSize = (ushort)Marshal.ReadInt16(p, 16);

        if (tag == WAVE_FORMAT_EXTENSIBLE && cbSize >= 22)
        {
            ushort validBits = (ushort)Marshal.ReadInt16(p, 18);
            uint mask = (uint)Marshal.ReadInt32(p, 20);
            var guidBytes = new byte[16];
            Marshal.Copy(p + 24, guidBytes, 0, 16);
            return new WaveFormatInfo(tag, channels, rate, bits, validBits == 0 ? bits : validBits, mask, new Guid(guidBytes));
        }

        Guid sub = tag == WAVE_FORMAT_IEEE_FLOAT ? SubFormatIeeeFloat : SubFormatPcm;
        return new WaveFormatInfo(tag, channels, rate, bits, bits, DefaultMaskForChannels(channels), sub);
    }

    public static uint DefaultMaskForChannels(int channels) => channels switch
    {
        1 => SpeakerLayout.Mono,
        2 => SpeakerLayout.Stereo,
        4 => SpeakerLayout.Quad,
        6 => SpeakerLayout.FivePointOne,
        8 => SpeakerLayout.SevenPointOne,
        _ => 0,
    };

    /// <summary>Returns a copy targeting a new channel mask (and channel count).</summary>
    public WaveFormatInfo WithChannelMask(uint mask)
    {
        int channels = SpeakerLayout.ChannelCount(mask);
        if (channels == 0)
        {
            throw new ArgumentException("Channel mask must contain at least one speaker.", nameof(mask));
        }

        return this with
        {
            FormatTag = WAVE_FORMAT_EXTENSIBLE,
            Channels = (ushort)channels,
            ChannelMask = mask,
            SubFormat = IsFloat ? SubFormatIeeeFloat : SubFormatPcm,
        };
    }

    public WaveFormatInfo WithBits(ushort bits, ushort validBits) => this with
    {
        FormatTag = WAVE_FORMAT_EXTENSIBLE,
        BitsPerSample = bits,
        ValidBitsPerSample = validBits,
        SubFormat = SubFormatPcm,
    };

    public WaveFormatInfo WithSampleRate(uint rate) => this with { FormatTag = WAVE_FORMAT_EXTENSIBLE, SampleRate = rate };

    /// <summary>Serialises as a 40-byte WAVEFORMATEXTENSIBLE.</summary>
    public byte[] ToExtensibleBytes()
    {
        var bytes = new byte[ExtensibleSize];
        var span = bytes.AsSpan();
        BitConverter.TryWriteBytes(span[0..], WAVE_FORMAT_EXTENSIBLE);
        BitConverter.TryWriteBytes(span[2..], Channels);
        BitConverter.TryWriteBytes(span[4..], SampleRate);
        BitConverter.TryWriteBytes(span[8..], AvgBytesPerSec);
        BitConverter.TryWriteBytes(span[12..], BlockAlign);
        BitConverter.TryWriteBytes(span[14..], BitsPerSample);
        BitConverter.TryWriteBytes(span[16..], (ushort)22);
        BitConverter.TryWriteBytes(span[18..], ValidBitsPerSample == 0 ? BitsPerSample : ValidBitsPerSample);
        BitConverter.TryWriteBytes(span[20..], ChannelMask);
        SubFormat.TryWriteBytes(span[24..]);
        return bytes;
    }

    /// <summary>Allocates unmanaged memory holding this format. Caller must free with Marshal.FreeHGlobal.</summary>
    public IntPtr ToUnmanaged()
    {
        byte[] bytes = ToExtensibleBytes();
        IntPtr p = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, p, bytes.Length);
        return p;
    }

    public string DescribeFormat() =>
        $"{SpeakerLayout.ShortLabel(ChannelMask)} · {SampleRate / 1000.0:0.#} kHz · {ValidBitsPerSample}-bit{(IsFloat ? " float" : string.Empty)}";

    public override string ToString() =>
        $"{Channels}ch mask=0x{ChannelMask:X} {SampleRate}Hz {BitsPerSample}/{ValidBitsPerSample}bit {(IsFloat ? "float" : "pcm")}";
}
