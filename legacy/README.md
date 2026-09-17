# Legacy version (v1)

This folder is an untouched backup of the original tray application, kept for reference.

How it worked:

- A WinForms app (.NET 6) polled the default render endpoint every two seconds and read
  `PKEY_AudioEndpoint_PhysicalSpeakers` to show "2.0" or "5.1" in the tray.
- Switching shelled out to NirSoft **SoundVolumeView.exe** with `/SetSpeakersConfig`, which writes the
  speaker configuration and device format straight into the registry
  (`HKLM\...\MMDevices\Audio\Render\{id}\Properties`). That requires elevation.
- Because registry edits do not take effect immediately, it then stopped and restarted the
  `Audiosrv` service, which also requires elevation and interrupts all audio.
- The elevation requirement is why it could not autostart reliably: Windows silently skips
  `Run` entries that need UAC.

`reference/2.0.reg` and `reference/5.1.reg` are registry exports of the endpoint in both states; the
differences are `PKEY_AudioEndpoint_PhysicalSpeakers`, `PKEY_AudioEndpoint_FullRangeSpeakers` and the
`WAVEFORMATEXTENSIBLE` stored in `PKEY_AudioEngine_DeviceFormat`.

`SoundVolumeView.exe` is not committed to the repository (it is NirSoft freeware, available from
https://www.nirsoft.net/utils/sound_volume_view.html). The v2 application in `src/` does not need it.
