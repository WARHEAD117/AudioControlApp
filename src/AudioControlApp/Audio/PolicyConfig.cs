using System.Runtime.InteropServices;

namespace AudioControlApp.Audio;

/// <summary>
/// Interop for the undocumented <c>IPolicyConfig</c> interface that the Windows Sound control panel
/// (mmsys.cpl) uses to change an endpoint's default format and speaker configuration.
/// It runs as the current user, so no elevation and no audio-service restart is required.
/// </summary>
[ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
internal class PolicyConfigClient
{
}

[ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, out IntPtr ppFormat);
    [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int bDefault, out IntPtr ppFormat);
    [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
    [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr pEndpointFormat, IntPtr pMixFormat);
    [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int bDefault, out long pmftDefault, out long pmftMinimum);
    [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ref long pmftPeriod);
    [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr pMode);
    [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr pMode);
    [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int bFxStore, ref PropKey key, out PropVariant value);
    [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int bFxStore, ref PropKey key, ref PropVariant value);
    [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int role);
    [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int visible);
}

/// <summary>PROPERTYKEY (named PropKey to avoid clashing with NAudio's PropertyKey).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PropKey
{
    public Guid FormatId;
    public int PropertyId;

    public PropKey(Guid formatId, int propertyId)
    {
        FormatId = formatId;
        PropertyId = propertyId;
    }

    public static readonly Guid AudioEndpointGuid = new("1da5d803-d492-4edd-8c23-e0c0ffee7f0e");
    public static readonly Guid AudioEngineDeviceFormatGuid = new("f19f064d-082c-4e27-bc73-6882a1bb8e4c");

    /// <summary>PKEY_AudioEndpoint_PhysicalSpeakers – the speaker configuration shown by the control panel.</summary>
    public static readonly PropKey AudioEndpoint_PhysicalSpeakers = new(AudioEndpointGuid, 3);

    /// <summary>PKEY_AudioEndpoint_FullRangeSpeakers – speakers that receive a full-range signal.</summary>
    public static readonly PropKey AudioEndpoint_FullRangeSpeakers = new(AudioEndpointGuid, 6);

    /// <summary>PKEY_AudioEngine_DeviceFormat – the shared-mode format the audio engine renders in.</summary>
    public static readonly PropKey AudioEngine_DeviceFormat = new(AudioEngineDeviceFormatGuid, 0);
}

/// <summary>
/// Minimal PROPVARIANT. Only the VT_UI4 member is used for writing; the struct is sized for x64
/// (24 bytes) which is also large enough for x86 (16 bytes).
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 24)]
internal struct PropVariant
{
    private const ushort VT_EMPTY = 0;
    private const ushort VT_UI4 = 19;

    [FieldOffset(0)] public ushort VarType;
    [FieldOffset(8)] public uint UInt32Value;
    [FieldOffset(8)] public IntPtr Pointer;

    public static PropVariant FromUInt32(uint value) => new() { VarType = VT_UI4, UInt32Value = value };

    public bool IsUInt32 => VarType == VT_UI4;
    public bool IsEmpty => VarType == VT_EMPTY;

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant pvar);

    public void Clear()
    {
        if (!IsEmpty)
        {
            PropVariantClear(ref this);
        }
    }
}

internal static class PolicyConfigFactory
{
    public static IPolicyConfig Create() => (IPolicyConfig)new PolicyConfigClient();
}
