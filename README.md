# 🌐 Ceprkac 0.6.6

> **A Chrome-inspired tabbed web browser for Windows, built with C# WinForms and WebView2.**

<p align="center">
  <img src="https://img.shields.io/badge/version-0.6.6-blue?style=flat-square" alt="Version">
  <img src="https://img.shields.io/badge/.NET-Framework%204.8-512BD4?style=flat-square" alt=".NET">
  <img src="https://img.shields.io/badge/engine-WebView2%20(Chromium)-orange?style=flat-square" alt="Engine">
  <img src="https://img.shields.io/badge/platform-Windows%20x64-0078D6?style=flat-square&logo=windows&logoColor=white" alt="Platform">
</p>

---

## 📋 Overview

Ceprkac is a feature-rich desktop web browser for Windows, powered by Microsoft's WebView2 (Chromium) engine. It matches GBrowser's UI and features — dark Chrome-like tabs, nested bookmarks, encrypted passwords, downloads, ad blocking — while using Edge WebView2 so sites that need **H.264 / AAC** (embedded Discord videos, many news players) actually play.

The installer is small: it targets **.NET Framework 4.8** (already on Windows 10/11) and downloads the **WebView2 Evergreen Runtime** automatically if it is missing.

---

## ✨ Features

### 🗂️ Tabbed Browsing
- Chrome-style custom-drawn tab strip with rounded tabs
- Open new tabs with `Ctrl+T` or the `+` button (address bar focused so you can type immediately)
- Close tabs with `Ctrl+W`, the `×` button, or middle-click
- Drag tabs to reorder
- Reopen closed tabs with `Ctrl+Shift+T`
- Per-tab zoom (`Ctrl+Plus` / `Ctrl+Minus` / `Ctrl+0`)
- Tabs open next to the current tab, not in new windows
- `window.open` (OAuth, Google, Reddit) opens as a real tab so `window.opener` stays intact
- Switch tabs with `Ctrl+Tab` / `Ctrl+Shift+Tab`

### 🔍 Smart Address Bar
- Type a URL and hit Enter to navigate
- Type plain text to search with your chosen search engine
- Auto-prepends `https://` for bare domains
- Focus with `Ctrl+L`
- Suggestions from history and bookmarks appear **as you type** (not while a page is loading)
- The omnibox is a real text box hosted in the toolbar, so opening a tab does not flicker or stall the page

### 🔎 Search Engine Choice
- First-run prompt to pick your default search engine
- Choose from Google, Bing, DuckDuckGo, Yahoo, Brave Search, or Startpage
- Used as both **home page** and address-bar search
- Change anytime from the `≡` menu → "Change Search Engine..."

### ⭐ Bookmarks
- **Bookmarks Bar** — always visible below the toolbar with clickable chips and overflow (`»`) when the bar is full
- **Nested Folders** — folders appear as dropdown buttons with recursive submenus, just like Chrome
- **Add/Remove** — click `☆` to toggle bookmark for current page (`Ctrl+D`)
- **Import** — import from Chrome, Firefox, or Edge via standard HTML bookmark files (preserves full folder tree)
- **Export** — export to Netscape HTML format compatible with all major browsers
- **Clear** — remove all bookmarks with confirmation

### 🔑 Password Manager
- **Import from CSV** — reads Chrome/Edge password export format (`name,url,username,password`)
- **Encrypted Storage** — passwords encrypted with Windows DPAPI, tied to your user account
- **Auto-Fill** — automatically fills login forms when you visit a saved site
- **Multi-Account Picker** — if multiple accounts exist for a site, shows a dropdown to choose which one
- **SPA Support** — retries with increasing delays for single-page apps like Discord
- **Smart Detection** — only triggers on pages with login fields or login-related URLs

### 📥 Downloads
- Toolbar dropdown next to the bookmark star, with live status
- Intercepts all downloads with a Save As dialog
- Real-time progress in the status bar
- Click a finished download to open it; list is remembered across restarts (`downloads.json`)

### 📜 History
- Automatically records the last 100 visited URLs
- Clear all history from the `≡` menu

### 🎨 Dark Theme
- Chrome-inspired dark color scheme across all UI elements
- Dark Windows title bar via `DwmSetWindowAttribute`
- Dark toolbar, tab strip, bookmarks bar, menus, and status bar
- Window position and size remembered (`config.json`)

### ⌨️ Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+T` | New tab |
| `Ctrl+W` | Close current tab |
| `Ctrl+Shift+T` | Restore closed tab |
| `Ctrl+L` | Focus address bar |
| `Ctrl+D` | Add/remove bookmark |
| `Ctrl+F` | Find in page |
| `Ctrl+I` | Open DevTools |
| `Ctrl+Plus` | Zoom in |
| `Ctrl+Minus` | Zoom out |
| `Ctrl+0` | Reset zoom |
| `Ctrl+Tab` | Next tab |
| `Ctrl+Shift+Tab` | Previous tab |
| `Enter` (in address bar) | Navigate or search |

### 🛡️ Ad Blocker (powered by GSecurity Ad Shield)
- **Network-level blocking** — blocks requests to known ad/tracking domains (Google Ads, Taboola, Outbrain, Facebook Pixel, etc.)
- **Element hiding** — removes ad containers, sponsored content, and overlay ads from page DOM
- **Always on** — no configuration needed, works on all sites
- **First-party safe** — same-site requests are never blocked
- **Passkeys off** — WebAuthn prompts are dismissed
- **Lightweight** — domain matching via HashSet plus `blocklist.txt`

### 🛡️ Injected-module cleaner
Unloads DLLs that do **not** belong to Ceprkac from **this process and its children only** (not system-wide). Starts on the first line of `Main`, before the window exists — infectors that map at process start are not grandfathered.

- **Kept:** Ceprkac's folder, Edge WebView2, Windows, .NET
- **Unloaded immediately:** Temp / AppData overlays and other user-profile drops
- **After init settles:** any later mapping that is still not in those trees
- Edge delay-loaded GPU/codec modules are left alone so the host does not crash

### 🛡️ WebView2 Auto-Install
- On first run, if the WebView2 Evergreen Runtime is not installed, Ceprkac downloads it from Microsoft and installs it
- The process is **x64** (`Prefer32Bit` off) so it matches the installed runtime
- After a successful install the app restarts once (`--after-webview2`)

### 🎬 Media
- Uses the system WebView2 / Edge codec pack, so **H.264 and AAC** play (Discord embeds, YouTube nocookie, typical HTML5 players)
- `discordapp.net` and `youtube-nocookie.com` are whitelisted so those embeds are not treated as ads

---

## 🚀 Usage

### Run from source

Requires the .NET SDK (to build) and .NET Framework 4.8 (to run):

```bash
dotnet run --project Ceprkac.csproj
```

### Build & publish

```bash
dotnet publish Ceprkac.csproj -c Release -o bin\publish
```

Copy `Ceprkac.ico` and `runtimes\win-x64\native\WebView2Loader.dll` next to `Ceprkac.exe` if you run the publish folder directly.

### Build the installer

`build.bat` publishes the framework-dependent x64 build and compiles the Inno Setup installer:

```bash
build.bat
```

Output: `releases\0.6.6\Ceprkac-0.6.6-Setup.exe`

---

## 📦 Requirements

- **Windows 10/11 x64** with **.NET Framework 4.8** (included with the OS; the installer stays small)
- **WebView2 Evergreen Runtime** — auto-installed if missing
- **Inno Setup 6** — only needed to *build* the installer
- **.NET SDK** — only needed to *build* from source

---

## 🏗️ Project Structure

| File | Description |
|---|---|
| `MainForm.cs` | Browser UI — tabs, toolbar, bookmarks, passwords, ad block, WebView2 |
| `InjectedModuleCleaner.cs` | Unloads foreign DLLs from Ceprkac and its children |
| `Program.cs` | Entry point — starts the module cleaner, then the UI |
| `Ceprkac.csproj` | `net48` / x64, WebView2 + System.Text.Json |
| `Ceprkac.iss` | Inno Setup script (framework-dependent publish folder) |
| `Ceprkac.ico` | Application icon |
| `blocklist.txt` | Extra ad/tracker domains |
| `build.bat` | Publish + installer pipeline |

---

## 💾 Data Storage

All user data is stored in `%AppData%\Ceprkac`:

| File | Contents |
|---|---|
| `bookmarks.txt` | Bookmark tree (folders and links) |
| `history.txt` | Browsing history (last 100 URLs) |
| `passwords.dat` | Saved passwords (DPAPI encrypted) |
| `settings.txt` | Search engine and home page preference |
| `downloads.json` | Recent download list |
| `config.json` | Window geometry |
| `WebView2UserData/` | Chromium profile data (cookies, cache, etc.) |

---

## 🔗 Related

[GBrowser](https://github.com/CroatiaSecurity/GBrowser) is the Python / Qt WebEngine edition (honest Qt identity, no proprietary H.264 in the shipped engine). Ceprkac is the WebView2 edition for Windows when you need those codecs.

---

## 📜 License & Disclaimer

This project is intended for authorized defensive, administrative, research, or educational use only.

- Use only on systems, networks, and environments where you have explicit permission.
- Misuse may violate law, contracts, policy, or acceptable-use terms.
- Running security, hardening, monitoring, or response tooling can impact stability and may disrupt legitimate software.
- Validate all changes in a test environment before production use.
- This project is provided **"AS IS"**, without warranties of any kind, including merchantability, fitness for a particular purpose, and non-infringement.
- Authors and contributors are **not liable** for direct or indirect damages, data loss, downtime, business interruption, legal exposure, or compliance impact.
- You are solely responsible for lawful operation, configuration choices, and compliance obligations in your jurisdiction.
- Saved passwords are encrypted using Windows DPAPI and are only accessible by the Windows user account that created them. The authors are not responsible for any credential exposure resulting from system compromise, misconfiguration, or misuse.
- This software is not affiliated with or endorsed by Google, Microsoft, Discord, or any other third party.

---

<p align="center">
  <sub>Built with care by <strong>Gorstak</strong></sub>
</p>
