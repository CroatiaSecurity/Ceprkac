# Ceprkac

A Chrome-inspired tabbed browser for **Windows x64**, built with C# WinForms and **WebView2**. Same UI and features as [GBrowser](https://github.com/CroatiaSecurity/GBrowser), with Edge codecs so **H.264 / AAC** actually play (Discord embeds, typical HTML5 players).

[![version](https://img.shields.io/badge/version-0.7.2-blue?style=flat-square)](CHANGELOG.md)
[![.NET](https://img.shields.io/badge/.NET-Framework%204.8-512BD4?style=flat-square)](#requirements)
[![engine](https://img.shields.io/badge/engine-WebView2%20(Chromium)-orange?style=flat-square)](#requirements)

**Download:** [Ceprkac 0.7.2 Setup](https://github.com/CroatiaSecurity/Ceprkac/releases/latest) · **History:** [CHANGELOG](CHANGELOG.md)

---

## Features

- Chrome-like tabs, dark UI, nested bookmarks, history, find-in-page, zoom
- Omnibox search (Google, Bing, DuckDuckGo, Yahoo, Brave, Startpage)
- Downloads with Save As; passwords (DPAPI) with auto-fill
- Network + DOM ad blocking (`blocklist.txt`); passkeys dismissed
- Injected-module cleaner (v0.7.1): **identity unload** on LDR load (queued, not under loader lock) and on WebView2 children — keep trees + bundled names + WinVerifyTrust; foreign/Temp/sideload plants `FreeLibrary` immediately
- WebView2 Evergreen Runtime auto-installed if missing
- Optional **default browser**: http/https + HTML files; links open in the existing window

| Shortcut | Action | Shortcut | Action |
|---|---|---|---|
| `Ctrl+T` / `Ctrl+W` | New / close tab | `Ctrl+Shift+T` | Restore tab |
| `Ctrl+Tab` | Next tab | `Ctrl+L` | Focus address bar |
| `Ctrl+D` | Bookmark | `Ctrl+F` | Find in page |
| `Ctrl+I` | DevTools | `Ctrl+Plus` / `Minus` / `0` | Zoom |

## Requirements

- Windows 10/11 x64, **.NET Framework 4.8** (already on the OS)
- **WebView2 Evergreen Runtime** (installed automatically if needed)

To **build** the installer: .NET SDK + [Inno Setup 6+](https://jrsoftware.org/isinfo.php)

```bat
dotnet run --project Ceprkac.csproj
build.bat
```

Output: `releases\0.7.2\Ceprkac-0.7.2-Setup.exe`

## Default browser

The installer offers **Set Ceprkac as the default browser**. That registers http, https, and HTML files and opens Windows Settings so you can confirm (Windows 10/11 do not allow a silent switch).

Later: menu → **Set as Default Browser...**, or:

```bat
Ceprkac.exe --register-browser
```

Opening a link while Ceprkac is already running adds a tab in that window.

## Data

All under `%AppData%\Ceprkac`: bookmarks, history, DPAPI passwords, settings, downloads, window geometry, WebView2 profile.

## Disclaimer

Authorized defensive, administrative, research, or educational use only. Provided **AS IS**, no warranties. Authors are not liable for damage, data loss, or legal exposure. You are responsible for lawful use. Not affiliated with Google, Microsoft, or Discord.

Built by **Gorstak**
