# Changelog

## 0.6.8 — 2026-08-23

Installer: `Ceprkac-0.6.8-Setup.exe`

### Default browser

- Registers as a Windows browser (http, https, HTML files) so it appears in Settings → Default apps.
- Installer checkbox **Set Ceprkac as the default browser** (on by default). Windows 10/11 still needs one confirmation in Settings — the picker opens automatically.
- Menu → **Set as Default Browser...** does the same later.
- Single-instance: clicking a link opens a tab in the running window instead of a second process.
- Command line: `Ceprkac.exe https://example.com` and `Ceprkac.exe --register-browser`.

### Address bar

- New-tab typing no longer drops the first character (`cakes` no longer becomes `akes`).
- Keys are intercepted before WebView2 while the omnibox is focused, so the homepage search box cannot steal them.
- Homepage / source updates do not overwrite text you already typed, and refocus does not Select-All over it.

### Pages that would not open

- SmartScreen URL checks are off (same as GBrowser / Chrome). Warez-adjacent forums such as TeamOS were being blocked or left blank.
- Stopped attaching Chrome DevTools (`Page.addScriptToEvaluateOnNewDocument`) on every tab. Cloudflare treats that as a bot and some threads never finished loading.
- Cloudflare challenge pages are not ad-blocked or script-injected.
- Failed navigations show the error in the status bar instead of a silent blank page.

---

## 0.6.7 — 2026-08-23

Installer: `Ceprkac-0.6.7-Setup.exe`

### Downloads

- **Save image as** (and save link / video / audio as) uses a single Save As dialog and actually writes the file.
- WebView2’s follow-up picker (often named `aaaa`) is suppressed: `DownloadStarting` sets `Handled` and completes a deferral after the path is chosen.

### Address bar

- New tab focuses the omnibox **immediately**, before WebView2 finishes starting.
- The first typed character is no longer eaten by the page (`oprekin` no longer becomes `prekin`).
- Homepage navigation does not overwrite text you already typed in the box.

### Injected-module cleaner

- Still unloads Temp / AppData / Downloads overlays from **this process and every child**, including WebView2 GPU/renderer.
- Host still freeze-unloads unknown mappings after init.
- Children no longer freeze-unload unknown **Program Files** modules. That was unmapping the GPU driver stack every ~2 seconds and blanking the window (dark → bright → repeat).
- NVIDIA / AMD / Intel Program Files trees are kept, same as Windows / Edge / .NET.

---

## 0.6.6 — 2026-08-23

WebView2 edition of GBrowser for Windows — H.264/AAC (Discord embeds), small installer.

- **.NET Framework 4.8 x64** — framework-dependent publish, small setup, no extra .NET runtime
- **WebView2 auto-install** — downloads Evergreen Runtime from Microsoft if missing, then restarts once
- **Search engine is the homepage** — first-run picker sets both home and omnibox search
- **Injected-module cleaner** — starts in `Main`, unloads foreign DLLs from Ceprkac and its children; Edge/Windows/.NET kept so GPU/codec delay-loads do not crash the host
- **Stable address bar** — real text box, no toolbar layout loop, autocomplete only while typing so Google opens immediately

---

## 0.6.5.0 — 2026-05-07

Earlier public build. See GitHub Releases.
