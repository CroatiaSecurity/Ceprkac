# Changelog

## 0.8.0 — 2026-08-31

Installer: `Ceprkac-0.8.0-Setup.exe`

### Credential autofill: first-load timing + Google two-step login

- **Passwords now fill on the first page load — no manual refresh needed.** The old autofill used an aggressive 3-second time debounce that was set *before* a retry loop lasting up to ~12 seconds, so the first `NavigationCompleted` locked out the real form load and multiple SPA navigations were dropped. Autofill is now keyed per-URL with a monotonic token: a genuinely new page always re-attempts, and a stale retry loop self-cancels the moment the page navigates on.
- **Google's two-step sign-in (email page → separate password page) is handled.** Autofill now also triggers on `SourceChanged`, so the client-side route to Google's password step is caught even without a full navigation. Domain matching accepts registrable-domain suffixes (so `accounts.google.com` credentials fill on the `google.com` password step). A password-only page fills only the visible password field via the new `FillPasswordOnly` path, leaving Google's hidden username input alone instead of fighting it.
- The account picker (multiple saved logins for a site) now respects password-only pages too.

---

## 0.7.9 — 2026-08-31

Installer: `Ceprkac-0.7.9-Setup.exe`

### Injected-module cleaner: full loadable-module-extension coverage

- **The in-process and child-process module cleaner now covers every loadable module extension, not just `.dll`.** `InjectedModuleCleaner` enumerates every mapped module via `EnumProcessModulesEx` regardless of extension and its keep/unload decision is path + Microsoft-family signature — so a foreign or user-writable-drop module is unloaded whether it is a `.dll`, a managed `.winmd` (WinRT metadata carrying MSIL), an `.ocx`, `.cpl`, `.ax`, `.node`, `.drv`, `.acm`, `.tsp`, `.mui`, or `.efi`.
- **Bundled framework modules are kept regardless of extension.** `Microsoft.*.winmd` and `System.*.winmd` shipped next to `Ceprkac.exe` (WinUI/WinRT) are no longer misjudged as foreign — `IsBundledFileName` now recognizes any loadable-module extension.
- **Search-order hijack detection is extension-aware.** Sideload target base names (`dbghelp`, `version`, `winmm`, …) are matched against any loadable-module extension rather than a hardcoded `.dll` list.
- No change to the unload primitive (queued `FreeLibrary` APC) or the keep-tree (Windows, .NET, Edge/WebView2, GPU vendors).

---

## 0.7.8 — 2026-08-31

Installer: `Ceprkac-0.7.8-Setup.exe`

### Autofill: payment methods and addresses

- **Saved credit/debit cards.** Menu → **Payment Methods...** manages a list of cards (nickname, cardholder, number, expiry, CVC). Stored encrypted at rest with DPAPI (CurrentUser scope) in `%AppData%\Ceprkac\cards.dat` — same scheme as saved passwords.
- **Saved addresses / contact profiles.** Menu → **Addresses...** manages name, email, phone, and full postal address, stored DPAPI-encrypted in `%AppData%\Ceprkac\addresses.dat`.
- **Checkout autofill.** On checkout/billing/shipping pages, Ceprkac detects card and address fields (via `autocomplete` attributes and common name/id patterns) and fills them. When more than one card or address is saved, a picker is shown so you choose which to use.
- Card number and CVC never leave the local encrypted store. As with any browser-stored payment data, anything running as your Windows user can potentially decrypt it — same trust model as Chrome/Edge saved cards.

---

## 0.7.7 — 2026-08-28

Installer: `Ceprkac-0.7.7-Setup.exe`

### Context-menu search fix

- **Right-click "Search {engine} for this image/video" now actually appears.** In 0.7.6 the search items shared a single error handler with the leading separator; when the separator failed to construct, every search item was silently dropped. Items are now inserted individually and the separator is best-effort, so a separator failure never hides the search entries.
- Added URL-based fallback detection: if a right-clicked element's reported target kind is generic (as some in-page image/video viewers report), Ceprkac now infers image vs. video from the media/link URL extension, so image and video search still show up.

---

## 0.7.6 — 2026-08-28

Installer: `Ceprkac-0.7.6-Setup.exe`

### Address bar fixes

- **Address bar now updates when you click a link.** In-page navigations (clicking a link, related video, back/forward) reliably reflect the new URL. The bar previously stayed blank because a stale omnibox-focus flag suppressed the update.
- **Typing in the address bar no longer eats the first character.** The custom keystroke-redirection layer (which fought WinForms focus and autocomplete handle recreation) has been removed; the address bar is now a normal text field. Searching "cake" searches "cake" — not "ake" with the homepage prepended.
- A freshly opened tab focuses and selects the address bar once, so the first keystroke cleanly replaces the pre-filled URL, then releases focus so you can click into the page normally.

### Context-menu search

- Right-click now offers **Search {engine} for ...** using your selected default search engine:
  - **Selected text** → text search for the selection.
  - **Right-click an image** → image search (Google Lens / Bing / Yandex by image URL; other engines fall back to a query).
  - **Right-click a video or audio element** → video search (Google / Bing verticals; others fall back to a query).
- Results open in a new tab.

---

## 0.7.5 — 2026-08-28

Installer: `Ceprkac-0.7.5-Setup.exe`

### YouTube ads (direction-independent)

- The main-world YouTube ad blocker is now installed **once per tab, unconditionally**, at ad-blocker setup via `Page.addScriptToEvaluateOnNewDocument` — no longer armed lazily on a cancellable top-level `NavigationStarting`.
- Ads are stripped regardless of how you reach a video: fresh page load, clicking a related/next video (SPA soft-navigation), and back/forward all run the main-world strip before YouTube's scripts, instead of only the first hard navigation.
- Ad-blocking no longer silently drops after a renderer recovery — the injected script is bound to every new document, so it survives page-process restarts.
- The main-world script self-guards on hostname (inert on non-YouTube hosts and on auth/OAuth pages), so installing it globally never affects other sites (Cloudflare forums, etc.).

---

## 0.7.4 — 2026-08-25

Installer: `Ceprkac-0.7.4-Setup.exe`

### Chrome layout (4K @ 175%)

GBrowser (Qt) was fine on the same display because it uses an HBox: nav buttons keep their size and the address bar stretches. Ceprkac's ToolStrip hosted the omnibox, so at 175% the right-side buttons (bookmark / downloads / menu) were clipped, and WebView2's fake `WM_DPICHANGED 96` left tabs + toolbar at 96-DPI sizes under a correctly-scaled Windows title bar.

- Nav bar is a `TableLayoutPanel` (same structure as GBrowser's `QHBoxLayout`) — back / forward / reload / go / bookmark / downloads / menu cannot be eaten by the omnibox.
- Tab strip, toolbar, bookmarks, and status use **monitor effective DPI** (`GetDpiForMonitor`), not `DeviceDpi` / WebView2's 96.
- Fonts are pixel-sized (`GraphicsUnit.Pixel × dpi/96`) so they match the bar heights.
- Bogus WebView2 DPI-96 messages are ignored so chrome cannot collapse after the first tab.
- Nav buttons use custom vector `OnPaint` (`ChromeButton`) instead of Unicode glyphs that GDI+ misrenders at non-standard DPI.
- PerMonitorV2 DPI awareness set at process level (`SetProcessDpiAwarenessContext`) as belt-and-suspenders alongside the app manifest.

---

## 0.7.2 — 2026-08-24

Installer: `Ceprkac-0.7.2-Setup.exe`

### DPI chrome (4K @ 175%)

- `AutoScaleMode.None` — WinForms no longer compounds ToolStrip/tab heights on each WebView2 DPI message.
- Tab strip, toolbar, and bookmark bar sizes are `design-pixels × DeviceDpi/96`, reapplied from constants (not from the current height).
- Bookmark/tab buttons no longer shrink over time.

Sentinel 2.2.6 is required: 2.2.5 treated CLR JIT as ALLOCVM_REMOTE and killed Ceprkac with exit 80131506.

---

## 0.7.1 — 2026-08-24

Installer: `Ceprkac-0.7.1-Setup.exe`

The module cleaner is the browser backbone again. Kiro registered `LdrRegisterDllNotification` and left an empty callback; children only lost Temp/AppData DLLs; freeze-after-2s grandfathered anything mapped during WebView2 startup.

### Module identity (immediate unload)

- LDR callback **queues** path+base (never `FreeLibrary` under the loader lock) and the worker unloads on the next pulse.
- A module stays only if it is: this process image, a keep-tree (Windows / Edge WebView / WebView2 user-data / .NET / GPU), a GPU ICD name, a **bundled** Ceprkac filename (`WebView2Loader`, `Microsoft.*`, `System.*`), or Microsoft/NVIDIA/Google/Intel-family **WinVerifyTrust** from Program Files.
- `version.dll` (and the rest of the sideload set) next to `Ceprkac.exe` is unloaded unless Microsoft-signed.
- Unsigned `evil.dll` in the Ceprkac directory is unloaded.
- WebView2 children use the **same** identity check — not "Temp only".
- Remote unload prefers `QueueUserAPC(FreeLibrary)` (same as Sentinel). `CreateRemoteThread` is fallback only.
- `%AppData%\Ceprkac\WebView2UserData` is a keep tree so Widevine/Crashpad are not fight-unloaded.

---

## 0.7.0 — 2026-08-24

Installer: `Ceprkac-0.7.0-Setup.exe`

Milestone build of the WebView2 browser: default-browser registration, reliable new-tab typing, YouTube ads, and forums that were blank or blocked.

### Default browser
- Registers http / https / HTML so Ceprkac appears in Windows Settings → Default apps.
- Installer checkbox and menu **Set as Default Browser...** (Windows still requires one confirmation).
- Single-instance: links open a tab in the running window (`Ceprkac.exe https://example.com`).

### Address bar
- New-tab search keeps the first character (`cakes` no longer becomes `akes`).
- Keys are caught before WebView2; homepage/URL updates do not overwrite what you already typed.

### YouTube
- Prerolls and feed ads are stripped in the page main world before YouTube’s scripts run.
- DevTools injection is armed only when that tab opens YouTube.

### Forums and other pages
- XenForo / TeamOS threads are not wiped by the generic ad hider.
- SmartScreen URL checks are off (same as GBrowser / Chrome).
- Cloudflare challenge pages are not treated as a bot via global DevTools scripts.
- Failed loads show the error in the status bar.

---

## 0.6.10 — 2026-08-23

Installer: `Ceprkac-0.6.10-Setup.exe`

### YouTube ads

- Restored main-world injection (`Page.addScriptToEvaluateOnNewDocument`) so prerolls and feed ads are stripped from `ytInitialData` / player JSON **before** YouTube’s scripts run.
- 0.6.8 switched that to an isolated-world `<script>` tag so Cloudflare would not see DevTools on every tab. YouTube’s CSP blocks those tags, which is why ads came back.
- CDP is armed only when **this tab** navigates to YouTube, then the navigation is replayed. Other tabs (TeamOS, etc.) never attach it.

---

## 0.6.9 — 2026-08-23

Installer: `Ceprkac-0.6.9-Setup.exe`

### Forums (TeamOS / XenForo)

- Thread pages no longer render as a blank dark view. The generic ad hider was matching XenForo layout (`<article>` posts, `.promoted`, `[class*="ad_"]`, `[class*="sponsor"]`) and deleting the whole page after the title loaded.
- Cosmetic hiding and the fetch/XHR patch are skipped on XenForo and Discourse.
- Social-site “promoted/sponsored” scrapers only run on Reddit, Facebook, X, and Instagram.

### Address bar / passwords

- Saved passwords no longer scan every page on that domain and then report “No login fields detected” on threads.

---

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
