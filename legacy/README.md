# Legacy version (v1)

This folder is an untouched backup of the original tray application, kept for reference.

## How it worked

- A WinForms app (.NET 6) polled the default render endpoint every two seconds and read
  `PKEY_AudioEndpoint_PhysicalSpeakers` to show "2.0" or "5.1" in the tray.
- Switching ran NirSoft **SoundVolumeView.exe** with `/SetSpeakersConfig "DENON-AVR" 0x3f 0x3f 0x3f`
  (or `0x3`). The device name was hard-coded.
- One second later it stopped and restarted the Windows Audio service (`Audiosrv`), which interrupts all
  audio.
- Start with Windows wrote the bare executable path to the value `AudioControlApp` under
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- Closing the window only hid it. There was no exit command, so the app could only be ended from Task
  Manager.

## Known problems

- **Service restart needs administrator rights.** The manifest requests `asInvoker`, so a normally started
  instance, including one started by the Run entry, cannot stop `Audiosrv`. The exception is caught and
  ignored, so the failure is silent. Switching therefore depended on how the app was started.
- **External dependency.** `SoundVolumeView.exe` had to sit next to the executable.
- **Hard-coded device name.** Only an endpoint named "DENON-AVR" could be switched.

The exact reason autostart felt unreliable was never confirmed. The silent service-restart failure above
is the most likely cause.

## Reference files

`reference/2.0.reg` and `reference/5.1.reg` are registry exports of the endpoint in both states. They differ
in `PKEY_AudioEndpoint_PhysicalSpeakers`, `PKEY_AudioEndpoint_FullRangeSpeakers` and the
`WAVEFORMATEXTENSIBLE` stored in `PKEY_AudioEngine_DeviceFormat`.

## Upgrading to v2

v2 uses the same Run value name, so enabling *Start with Windows* in v2 replaces the v1 entry. End v1 in
Task Manager before starting v2. The two versions do not block each other, and unticking startup in v1
deletes v2's entry.

`SoundVolumeView.exe` is not committed to the repository. It is NirSoft freeware, available from
https://www.nirsoft.net/utils/sound_volume_view.html. The v2 application in `src/` does not need it.
