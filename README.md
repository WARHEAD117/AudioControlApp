# AudioControlApp

一個常駐系統匣（托盤）的小工具，用來快速查看和切換 Windows 輸出裝置的喇叭配置（2.0 立體聲 / 5.1 / 7.1 …），
並能根據正在執行的程式自動切換。

A tray utility for Windows that shows and switches the speaker layout (2.0 / 5.1 / 7.1 …) of an output
device, and switches it automatically depending on which program is running. English section
[below](#english).

![tray icons](docs/icons.png)

## 為什麼需要它

接了 AV 擴大機（功放）的電腦通常會遇到一個兩難：

- 把 Windows 設成 **5.1**，遊戲和影片可以輸出真正的環繞聲；但一般的立體聲內容（音樂、瀏覽器、系統聲音）
  只會被送到左右聲道，其餘聲道靜音，擴大機收到的是 6 聲道 PCM，無法再做自己的環繞擴展，音量也偏小。
- 把 Windows 設成 **2.0**，擴大機收到 2 聲道 PCM，可以自行做 Dolby Surround / DTS Neural:X 等擴展，
  日常使用聲音更大更飽滿；但支援環繞的遊戲就只能輸出立體聲。

所以最理想的是：**平常 2.0，開啟支援環繞的程式時自動切到 5.1，關掉後自動切回來**。這正是這個工具做的事。

## 功能

- 系統匣圖示即時顯示目前配置（`2.0` / `5.1` / `7.1`，環繞配置帶綠色底線），隨淺色/深色工作列自動變色。
- 左鍵單擊圖示在兩種配置間快速切換（可設定），右鍵選單列出所有啟用的配置。
- **自動切換規則**：依可執行檔名稱（支援 `*`、`?` 萬用字元）在「程式執行中」或「程式位於前景」時套用指定配置；
  沒有規則符合時套用後備配置。規則為邊緣觸發：手動切換會保留到規則狀態再次改變。
- 可選擇跟隨 Windows 預設輸出裝置，或固定控制某一個裝置。
- 檢測裝置實際支援哪些配置（與 Windows 音效控制台使用相同的測試）。
- 全域快速鍵、切換通知、開機自動啟動（HKCU Run 項，並能偵測被工作管理員停用的情況）。
- 命令列介面，可從腳本 / Stream Deck / AutoHotkey 呼叫。
- 介面支援英文、簡體中文、繁體中文（自動跟隨系統，可手動指定）。
- **不需要管理員權限，也不依賴任何外部程式。**

## 運作原理

舊版（見 [`legacy/`](legacy/README.md)）透過 NirSoft SoundVolumeView 直接改寫登錄檔，再重啟 `Audiosrv`
服務，因此必須以管理員身分執行，也無法正常隨開機啟動。

新版改用 Windows 音效控制台（mmsys.cpl）本身所使用的 COM 介面 `IPolicyConfig`：

1. `SetPropertyValue` 寫入 `PKEY_AudioEndpoint_PhysicalSpeakers` 和 `PKEY_AudioEndpoint_FullRangeSpeakers`
   （控制台「設定」精靈顯示的喇叭配置）。
2. `SetDeviceFormat` 以目前的取樣率 / 位元深度加上新的聲道遮罩重新設定端點格式——這一步才是真正改變送往擴大機的
   聲道數的動作。

這兩個呼叫以一般使用者身分即可完成，變更立即生效，不需要重啟服務。
`IPolicyConfig` 是未公開介面，但自 Windows 7 起就被大量工具使用，在 Windows 10 / 11 上均可運作。

## 安裝與使用

需要 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（或使用自帶執行時的 self-contained 版本）。

1. 從 [Releases](https://github.com/WARHEAD117/AudioControlApp/releases) 下載並解壓到任意資料夾，執行 `AudioControlApp.exe`。
2. 右鍵系統匣圖示 →「設定…」：
   - **一般**：選擇輸出裝置、左鍵快速切換的兩種配置、快速鍵、開機啟動。
   - **自動切換**：新增規則（可從執行中的程式清單挑選），設定沒有規則符合時的後備配置。
   - **配置**：勾選要出現在選單中的配置；「檢測裝置支援情況」會標出裝置實際接受的配置。
3. 在 exe 旁邊放一個名為 `portable` 的空檔案即可改為可攜模式（設定與記錄存放在程式資料夾）。

設定檔：`%AppData%\AudioControlApp\settings.json`；記錄檔：`%LocalAppData%\AudioControlApp\app.log`。

### 命令列

```
AudioControlApp.exe                 啟動（已在執行時則開啟設定視窗）
AudioControlApp.exe --set 5.1       切換配置：2.0 | 5.1 | 5.1side | 7.1 | 0x<遮罩>
AudioControlApp.exe --toggle        在兩種快速切換配置間切換
AudioControlApp.exe --auto on|off   啟用 / 停用自動切換
AudioControlApp.exe --show [n]      開啟設定視窗（n = 分頁索引）
AudioControlApp.exe --status        在主控台印出目前裝置與配置
AudioControlApp.exe --exit          結束執行中的實例
```

## 從原始碼建置

```
dotnet build src/AudioControlApp/AudioControlApp.csproj -c Release
dotnet publish src/AudioControlApp/AudioControlApp.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

需要 .NET 8 SDK。唯一的相依套件是 [NAudio.Wasapi](https://github.com/naudio/NAudio)（裝置列舉與通知）。

## 常見問題

**切換後某個程式沒有聲音了？** 端點格式改變時 Windows 會讓既有音訊串流失效，多數程式會自動重新開啟；少數程式需要
自己重啟。這和在音效控制台手動改「預設格式」時的行為相同。

**選單裡的 5.1 顯示「裝置不接受」？** 表示裝置在目前取樣率 / 位元深度下不接受 6 聲道。切換時程式會自動嘗試
16 / 24 / 32 位元與 44.1 / 48 / 96 kHz 的組合；若仍失敗，請先在音效控制台把預設格式改成裝置支援的組合。

**規則沒有觸發？** 規則比對的是可執行檔名稱（含 `.exe`，不分大小寫）。用「執行中…」按鈕從清單中挑選最保險；
「前景」觸發對 UWP 應用程式無效（它們的前景視窗屬於 ApplicationFrameHost）。

---

## English

### Why

A PC connected to an AV receiver faces a dilemma: with Windows set to 5.1, surround-capable games and
players output real surround, but ordinary stereo content is sent only to the front left/right channels
and the receiver, seeing 6-channel PCM, cannot apply its own upmixing and plays quieter. With Windows set
to 2.0, the receiver gets 2-channel PCM and can upmix (Dolby Surround, DTS Neural:X …), which sounds
fuller for everyday use, but surround games are limited to stereo.

The ideal is *2.0 by default, 5.1 while a surround-capable program runs, back to 2.0 afterwards*. That is
what this tool does.

### Features

- Tray icon shows the current layout (`2.0`, `5.1`, `7.1` …), adapting to the light/dark taskbar.
- Left-click toggles between two layouts; the context menu lists all enabled layouts.
- Automatic switching rules by executable name (wildcards allowed), triggered while the program is running
  or while it is in the foreground, with a fallback layout when nothing matches. Rules are edge-triggered,
  so a manual switch is respected until the rule state changes.
- Follow the Windows default device or pin a specific one; probe which layouts the device accepts.
- Global hotkey, notifications, start-with-Windows, command-line interface, English / 简体中文 / 繁體中文.
- **No administrator rights and no external programs required.**

### How it works

The previous version drove NirSoft SoundVolumeView to edit the registry and then restarted the Windows
Audio service, which required elevation and made autostart unreliable. This version uses the undocumented
`IPolicyConfig` COM interface, the same one the Sound control panel uses: `SetPropertyValue` updates
`PKEY_AudioEndpoint_PhysicalSpeakers` / `FullRangeSpeakers`, and `SetDeviceFormat` re-applies the endpoint
format with the new channel mask. Both run as the current user and take effect immediately.

### Usage

Requires the .NET 8 Desktop Runtime (or use the self-contained build). Unzip, run `AudioControlApp.exe`,
right-click the tray icon → *Settings…*. Put an empty file named `portable` next to the executable to keep
settings and logs in the application folder.

```
AudioControlApp.exe --set 5.1 | 2.0 | 5.1side | 7.1 | 0x<mask>
AudioControlApp.exe --toggle
AudioControlApp.exe --auto on|off
AudioControlApp.exe --show [tab]
AudioControlApp.exe --status
AudioControlApp.exe --exit
```

### Building

`dotnet build src/AudioControlApp/AudioControlApp.csproj -c Release` with the .NET 8 SDK. The only
dependency is NAudio.Wasapi.

## License

MIT – see [LICENSE](LICENSE).
