# Changelog

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
