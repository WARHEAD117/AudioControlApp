using System.Runtime.InteropServices;
using AudioControlApp.App;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace AudioControlApp.Audio;

public sealed record AudioDeviceInfo(string Id, string Name, bool IsDefault);

public sealed record DeviceStatus(
    string Id,
    string Name,
    WaveFormatInfo Format,
    uint PhysicalSpeakers,
    bool IsDefault,
    bool IsFallback)
{
    /// <summary>The mask the audio engine currently renders with – the value that matters to the amplifier.</summary>
    public uint Mask => Format.ChannelMask;
    public string ShortLabel => SpeakerLayout.ShortLabel(Mask);
}

public sealed record SetLayoutResult(bool Success, string Message, WaveFormatInfo? Applied, int HResult = 0)
{
    public static SetLayoutResult Ok(WaveFormatInfo applied, string message) => new(true, message, applied);
    public static SetLayoutResult Fail(string message, int hr = 0) => new(false, message, null, hr);
}

/// <summary>
/// Enumerates render endpoints, reads their current speaker layout and changes it through
/// <see cref="IPolicyConfig"/>. All members are expected to be called from the UI thread; change
/// notifications from the audio system are marshalled back to the thread that created the service.
/// </summary>
public sealed class AudioDeviceService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly NotificationClient _notificationClient;
    private readonly SynchronizationContext? _sync;
    private bool _disposed;

    /// <summary>Raised (on the creating thread) when devices, the default device or a relevant property changed.</summary>
    public event EventHandler? Changed;

    public AudioDeviceService()
    {
        _sync = SynchronizationContext.Current;
        _notificationClient = new NotificationClient(this);
        try
        {
            _enumerator.RegisterEndpointNotificationCallback(_notificationClient);
        }
        catch (Exception ex)
        {
            Logger.Warn("Could not register for endpoint notifications: " + ex.Message);
        }
    }

    public IReadOnlyList<AudioDeviceInfo> GetRenderDevices()
    {
        var result = new List<AudioDeviceInfo>();
        string? defaultId = null;
        try
        {
            using var def = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            defaultId = def.ID;
        }
        catch (COMException)
        {
            // no default device
        }

        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            using (device)
            {
                result.Add(new AudioDeviceInfo(device.ID, SafeName(device), device.ID == defaultId));
            }
        }

        return result;
    }

    /// <summary>
    /// Resolves the endpoint to operate on: the preferred one when it is present and active, otherwise the
    /// Windows default render endpoint. Returns null when there is no usable render endpoint.
    /// </summary>
    public DeviceStatus? GetStatus(string? preferredDeviceId)
    {
        MMDevice? device = null;
        bool fallback = false;
        try
        {
            if (!string.IsNullOrEmpty(preferredDeviceId))
            {
                try
                {
                    device = _enumerator.GetDevice(preferredDeviceId);
                    if (device.State != DeviceState.Active)
                    {
                        device.Dispose();
                        device = null;
                    }
                }
                catch (COMException)
                {
                    device = null;
                }

                fallback = device is null;
            }

            if (device is null)
            {
                try
                {
                    device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                }
                catch (COMException)
                {
                    return null;
                }
            }

            string? defaultId = null;
            try
            {
                using var def = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                defaultId = def.ID;
            }
            catch (COMException)
            {
            }

            uint physical = ReadPhysicalSpeakers(device);
            WaveFormatInfo? format = GetDeviceFormat(device.ID);
            if (format is null)
            {
                // Fall back to the property store copy of the device format.
                format = ReadDeviceFormatProperty(device);
            }

            format ??= new WaveFormatInfo(WaveFormatInfo.WAVE_FORMAT_PCM, 2, 48000, 16, 16, physical != 0 ? physical : SpeakerLayout.Stereo, WaveFormatInfo.SubFormatPcm);

            return new DeviceStatus(device.ID, SafeName(device), format, physical, device.ID == defaultId, fallback);
        }
        finally
        {
            device?.Dispose();
        }
    }

    /// <summary>Reads the current (shared-mode) device format via IPolicyConfig.</summary>
    public WaveFormatInfo? GetDeviceFormat(string deviceId)
    {
        IPolicyConfig? policy = null;
        try
        {
            policy = PolicyConfigFactory.Create();
            int hr = policy.GetDeviceFormat(deviceId, 0, out IntPtr p);
            if (hr != 0 || p == IntPtr.Zero)
            {
                Logger.Warn($"GetDeviceFormat failed: 0x{hr:X8}");
                return null;
            }

            try
            {
                return WaveFormatInfo.FromPointer(p);
            }
            finally
            {
                Marshal.FreeCoTaskMem(p);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("GetDeviceFormat threw: " + ex.Message);
            return null;
        }
        finally
        {
            if (policy is not null)
            {
                Marshal.ReleaseComObject(policy);
            }
        }
    }

    /// <summary>Tests (exclusive mode, like the Sound control panel does) whether the endpoint accepts a format.</summary>
    public bool IsFormatSupported(string deviceId, WaveFormatInfo format)
    {
        try
        {
            using var device = _enumerator.GetDevice(deviceId);
            return IsFormatSupported(device, format);
        }
        catch (Exception ex)
        {
            Logger.Warn("IsFormatSupported threw: " + ex.Message);
            return false;
        }
    }

    private static bool IsFormatSupported(MMDevice device, WaveFormatInfo format)
    {
        byte[] bytes = format.ToExtensibleBytes();
        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            WaveFormat wf = WaveFormat.MarshalFromPtr(handle.AddrOfPinnedObject());
            return device.AudioClient.IsFormatSupported(AudioClientShareMode.Exclusive, wf);
        }
        catch (Exception ex)
        {
            Logger.Warn($"IsFormatSupported({format}) threw: {ex.Message}");
            return false;
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>
    /// For each known layout, says whether the endpoint accepts it at its current sample rate / bit depth.
    /// </summary>
    public IReadOnlyDictionary<uint, bool> ProbeLayouts(string deviceId, IEnumerable<uint> masks)
    {
        var result = new Dictionary<uint, bool>();
        WaveFormatInfo? current = GetDeviceFormat(deviceId);
        if (current is null)
        {
            return result;
        }

        try
        {
            using var device = _enumerator.GetDevice(deviceId);
            foreach (uint mask in masks.Distinct())
            {
                if (SpeakerLayout.ChannelCount(mask) == 0)
                {
                    result[mask] = false;
                    continue;
                }

                result[mask] = FindSupportedVariant(device, current.WithChannelMask(mask)) is not null;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("ProbeLayouts threw: " + ex.Message);
        }

        return result;
    }

    /// <summary>
    /// Switches the endpoint to the given channel mask, keeping the current sample rate and bit depth when the
    /// device accepts them, otherwise trying common alternatives. Also updates the speaker configuration
    /// properties so the Sound control panel reflects the change.
    /// </summary>
    public SetLayoutResult SetLayout(string deviceId, uint mask)
    {
        int channels = SpeakerLayout.ChannelCount(mask);
        if (channels == 0)
        {
            return SetLayoutResult.Fail("Invalid channel mask.");
        }

        IPolicyConfig? policy = null;
        try
        {
            policy = PolicyConfigFactory.Create();

            int hr = policy.GetDeviceFormat(deviceId, 0, out IntPtr pCurrent);
            if (hr != 0 || pCurrent == IntPtr.Zero)
            {
                return SetLayoutResult.Fail($"GetDeviceFormat failed (0x{hr:X8}).", hr);
            }

            WaveFormatInfo current;
            try
            {
                current = WaveFormatInfo.FromPointer(pCurrent);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pCurrent);
            }

            WaveFormatInfo requested = current.WithChannelMask(mask);
            WaveFormatInfo target = requested;
            try
            {
                using var device = _enumerator.GetDevice(deviceId);
                WaveFormatInfo? supported = FindSupportedVariant(device, requested);
                if (supported is not null)
                {
                    target = supported;
                }
                else
                {
                    Logger.Warn($"No probed variant of {requested} reported as supported; trying it anyway.");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Format probing failed, using requested format: " + ex.Message);
            }

            Logger.Info($"SetLayout {deviceId}: {current} -> {target}");

            // 1. Speaker configuration properties (what the control panel's "Configure" wizard writes).
            var value = PropVariant.FromUInt32(mask);
            var key = PropKey.AudioEndpoint_PhysicalSpeakers;
            int hrPhysical = policy.SetPropertyValue(deviceId, 0, ref key, ref value);
            key = PropKey.AudioEndpoint_FullRangeSpeakers;
            int hrFullRange = policy.SetPropertyValue(deviceId, 0, ref key, ref value);
            if (hrPhysical != 0 || hrFullRange != 0)
            {
                Logger.Warn($"SetPropertyValue: PhysicalSpeakers=0x{hrPhysical:X8} FullRangeSpeakers=0x{hrFullRange:X8}");
            }

            // 2. The device format – this is what actually changes the number of channels sent to the device.
            IntPtr pFormat = target.ToUnmanaged();
            try
            {
                hr = policy.SetDeviceFormat(deviceId, pFormat, pFormat);
            }
            finally
            {
                Marshal.FreeHGlobal(pFormat);
            }

            if (hr != 0)
            {
                string msg = $"SetDeviceFormat failed (0x{hr:X8}): {Marshal.GetExceptionForHR(hr)?.Message}";
                Logger.Error(msg);
                return SetLayoutResult.Fail(msg, hr);
            }

            // 3. Verify.
            WaveFormatInfo? after = GetDeviceFormat(deviceId);
            if (after is not null && after.ChannelMask != mask)
            {
                string msg = $"Device reports {after} after the change.";
                Logger.Warn(msg);
                return SetLayoutResult.Fail(msg);
            }

            return SetLayoutResult.Ok(after ?? target, target.DescribeFormat());
        }
        catch (Exception ex)
        {
            Logger.Error("SetLayout threw: " + ex);
            return SetLayoutResult.Fail(ex.Message, ex.HResult);
        }
        finally
        {
            if (policy is not null)
            {
                Marshal.ReleaseComObject(policy);
            }
        }
    }

    /// <summary>Tries the requested format first, then common bit depths and sample rates.</summary>
    private static WaveFormatInfo? FindSupportedVariant(MMDevice device, WaveFormatInfo requested)
    {
        if (IsFormatSupported(device, requested))
        {
            return requested;
        }

        var candidates = new List<WaveFormatInfo>();
        foreach (uint rate in new[] { requested.SampleRate, 48000u, 44100u, 96000u })
        {
            foreach ((ushort bits, ushort valid) in new[] { ((ushort)16, (ushort)16), ((ushort)24, (ushort)24), ((ushort)32, (ushort)24), ((ushort)32, (ushort)32) })
            {
                var candidate = requested.WithSampleRate(rate).WithBits(bits, valid);
                if (candidate != requested && !candidates.Contains(candidate))
                {
                    candidates.Add(candidate);
                }
            }
        }

        foreach (var candidate in candidates)
        {
            if (IsFormatSupported(device, candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static uint ReadPhysicalSpeakers(MMDevice device)
    {
        try
        {
            var props = device.Properties;
            if (props.Contains(PropertyKeys.PKEY_AudioEndpoint_PhysicalSpeakers))
            {
                object? v = props[PropertyKeys.PKEY_AudioEndpoint_PhysicalSpeakers].Value;
                return v switch
                {
                    uint u => u,
                    int i => unchecked((uint)i),
                    _ => 0,
                };
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("Reading PhysicalSpeakers failed: " + ex.Message);
        }

        return 0;
    }

    private static WaveFormatInfo? ReadDeviceFormatProperty(MMDevice device)
    {
        try
        {
            var props = device.Properties;
            if (props.Contains(PropertyKeys.PKEY_AudioEngine_DeviceFormat) &&
                props[PropertyKeys.PKEY_AudioEngine_DeviceFormat].Value is byte[] blob && blob.Length >= 18)
            {
                GCHandle handle = GCHandle.Alloc(blob, GCHandleType.Pinned);
                try
                {
                    return WaveFormatInfo.FromPointer(handle.AddrOfPinnedObject());
                }
                finally
                {
                    handle.Free();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("Reading DeviceFormat property failed: " + ex.Message);
        }

        return null;
    }

    private static string SafeName(MMDevice device)
    {
        try
        {
            return device.FriendlyName;
        }
        catch
        {
            return device.ID;
        }
    }

    private void RaiseChanged()
    {
        if (_disposed)
        {
            return;
        }

        if (_sync is not null)
        {
            _sync.Post(_ => Changed?.Invoke(this, EventArgs.Empty), null);
        }
        else
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _enumerator.UnregisterEndpointNotificationCallback(_notificationClient);
        }
        catch
        {
        }

        _enumerator.Dispose();
    }

    private sealed class NotificationClient : IMMNotificationClient
    {
        private readonly AudioDeviceService _owner;

        public NotificationClient(AudioDeviceService owner) => _owner = owner;

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) => _owner.RaiseChanged();

        public void OnDeviceAdded(string pwstrDeviceId) => _owner.RaiseChanged();

        public void OnDeviceRemoved(string deviceId) => _owner.RaiseChanged();

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Multimedia)
            {
                _owner.RaiseChanged();
            }
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
            if (key.formatId == PropKey.AudioEndpointGuid || key.formatId == PropKey.AudioEngineDeviceFormatGuid)
            {
                _owner.RaiseChanged();
            }
        }
    }
}
