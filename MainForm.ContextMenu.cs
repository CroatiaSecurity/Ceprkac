using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Http;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Ceprkac
{
    public partial class MainForm
    {
        private void Core_ContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            if (sharedEnvironment == null) return;
            var core = sender as CoreWebView2;
            if (core == null) return;
            string sourceUri = "";
            string linkUri = "";
            string selectionText = "";
            CoreWebView2ContextMenuTargetKind kind = CoreWebView2ContextMenuTargetKind.Page;
            try
            {
                var target = e.ContextMenuTarget;
                kind = target.Kind;
                if (target.HasSourceUri) sourceUri = target.SourceUri ?? "";
                if (target.HasLinkUri) linkUri = target.LinkUri ?? "";
                try { selectionText = target.SelectionText ?? ""; } catch { }
            }
            catch { }
            ReplaceNativeSaveAs(e.MenuItems, "saveImageAs", core, sourceUri);
            ReplaceNativeSaveAs(e.MenuItems, "saveVideoAs", core, sourceUri);
            ReplaceNativeSaveAs(e.MenuItems, "saveAudioAs", core, sourceUri);
            ReplaceNativeSaveAs(e.MenuItems, "saveLinkAs", core, linkUri);
            AddSearchMenuItems(e.MenuItems, kind, selectionText, sourceUri, linkUri);
        }

        // Adds "Search {engine} for ..." entries to the WebView2 context menu based on
        // what was right-clicked: selected text (text search), an image (reverse/image
        // search by URL / Google Lens), or a video/audio element (video search by URL).
        private void AddSearchMenuItems(
            IList<CoreWebView2ContextMenuItem> items,
            CoreWebView2ContextMenuTargetKind kind,
            string selectionText,
            string sourceUri,
            string linkUri)
        {
            if (sharedEnvironment == null) return;
            var engine = CurrentSearchEngineName();
            var toAdd = new List<CoreWebView2ContextMenuItem>();

            void AddItem(string label, string url)
            {
                if (string.IsNullOrWhiteSpace(url)) return;
                try
                {
                    var item = sharedEnvironment.CreateContextMenuItem(
                        label, null, CoreWebView2ContextMenuItemKind.Command);
                    var captured = url;
                    item.CustomItemSelected += (_, _) =>
                    {
                        try { BeginInvoke(new Action(() => AddNewTab(captured, focusOmnibox: false))); }
                        catch { }
                    };
                    toAdd.Add(item);
                }
                catch { }
            }

            void AddCopy(string label, string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                try
                {
                    var item = sharedEnvironment.CreateContextMenuItem(
                        label, null, CoreWebView2ContextMenuItemKind.Command);
                    var captured = text;
                    item.CustomItemSelected += (_, _) =>
                    {
                        try { BeginInvoke(new Action(() => { try { Clipboard.SetText(captured); statusLabel.Text = "Copied."; } catch { } })); }
                        catch { }
                    };
                    toAdd.Add(item);
                }
                catch { }
            }

            // Selected text → text search (works for any target kind that carries a selection).
            var sel = (selectionText ?? "").Trim();
            if (sel.Length > 0)
            {
                var shown = sel.Length > 40 ? sel.Substring(0, 40) + "…" : sel;
                AddItem($"Search {engine} for \"{shown}\"", BuildTextSearchUrl(sel));
            }

            // Prefer the media/source URI; fall back to a link that points at a media file.
            // Some in-page image/video viewers report Kind = Page/Other, so do not rely on
            // Kind alone — infer from the URI / CDN patterns as well.
            var mediaUri = !string.IsNullOrWhiteSpace(sourceUri) ? sourceUri : linkUri;

            bool isImage = kind == CoreWebView2ContextMenuTargetKind.Image
                           || LooksLikeImageUrl(sourceUri) || LooksLikeImageUrl(linkUri)
                           || LooksLikeImageHost(sourceUri) || LooksLikeImageHost(linkUri);
            // Kind=Image always wins even when the CDN URL has no file extension.
            if (kind == CoreWebView2ContextMenuTargetKind.Image && !string.IsNullOrWhiteSpace(sourceUri))
                isImage = true;

            bool isVideo = kind == CoreWebView2ContextMenuTargetKind.Video
                           || kind == CoreWebView2ContextMenuTargetKind.Audio
                           || LooksLikeVideoUrl(sourceUri) || LooksLikeVideoUrl(linkUri);

            if (isImage && !string.IsNullOrWhiteSpace(mediaUri))
            {
                // Explicit Lens label when Google is the default engine; otherwise engine-named image search.
                if (SearchHost().Contains("google."))
                    AddItem("Search image with Google Lens", BuildImageSearchUrl(mediaUri));
                else
                    AddItem($"Search {engine} for this image", BuildImageSearchUrl(mediaUri));
                AddItem("Open image in new tab", mediaUri);
                AddCopy("Copy image address", mediaUri);
            }
            else if (isVideo && !string.IsNullOrWhiteSpace(mediaUri))
            {
                AddItem($"Search {engine} for this video", BuildVideoSearchUrl(mediaUri));
                AddItem("Open media in new tab", mediaUri);
                AddCopy("Copy media address", mediaUri);
            }
            else if (!string.IsNullOrWhiteSpace(linkUri) && sel.Length == 0)
            {
                AddItem("Open link in new tab", linkUri);
                AddCopy("Copy link address", linkUri);
            }

            if (toAdd.Count == 0) return;

            // Insert our items at the top of the menu, each guarded independently so one
            // failure never suppresses the rest. Separator is best-effort and last.
            int idx = 0;
            foreach (var it in toAdd)
            {
                try { items.Insert(idx, it); idx++; }
                catch { }
            }
            try
            {
                var sep = sharedEnvironment.CreateContextMenuItem(
                    null, null, CoreWebView2ContextMenuItemKind.Separator);
                items.Insert(idx, sep);
            }
            catch { }
        }

        private static readonly string[] ImageExtensions =
            { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg", ".avif", ".ico", ".tiff", ".jfif", ".heic" };
        private static readonly string[] VideoExtensions =
            { ".mp4", ".webm", ".mkv", ".mov", ".avi", ".m4v", ".ogv", ".mpeg", ".mpg", ".m3u8" };
        private static readonly string[] ImageHostHints =
        {
            "googleusercontent.com", "ggpht.com", "gstatic.com", "ytimg.com",
            "twimg.com", "fbcdn.net", "cdninstagram.com", "pinimg.com",
            "imgur.com", "i.imgur.com", "wikimedia.org", "cloudinary.com",
            "imgix.net", "akamaihd.net", "discordapp.net", "discordcdn.com",
            "media.tenor.com", "giphy.com"
        };

        private static bool LooksLikeImageUrl(string? url) => UrlHasExtension(url, ImageExtensions);
        private static bool LooksLikeVideoUrl(string? url) => UrlHasExtension(url, VideoExtensions);

        private static bool LooksLikeImageHost(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            try
            {
                var u = new Uri(url!);
                var host = u.Host.ToLowerInvariant();
                foreach (var h in ImageHostHints)
                    if (host == h || host.EndsWith("." + h, StringComparison.Ordinal)) return true;
                var path = u.AbsolutePath.ToLowerInvariant();
                if (path.Contains("/image") || path.Contains("/img/") || path.Contains("/thumb")
                    || path.Contains("/photo") || path.Contains("/media/") || path.Contains("/avatar"))
                    return true;
            }
            catch { }
            return false;
        }

        private static bool UrlHasExtension(string? url, string[] exts)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            string path;
            try { path = new Uri(url!).AbsolutePath.ToLowerInvariant(); }
            catch { path = url!.ToLowerInvariant(); }
            // Strip query-like suffixes that sometimes stick to the path segment.
            int q = path.IndexOf('@');
            if (q > 0) path = path.Substring(0, q);
            foreach (var e in exts)
                if (path.EndsWith(e, StringComparison.Ordinal)) return true;
            return false;
        }

        // Resolve a friendly name for the active search engine from its search template.
        private string CurrentSearchEngineName()
        {
            foreach (var (name, _, search) in SearchEngines)
            {
                if (string.Equals(search, searchUrlTemplate, StringComparison.OrdinalIgnoreCase))
                    return name;
            }
            try { return new Uri(string.Format(searchUrlTemplate, "x")).Host.Replace("www.", ""); }
            catch { return "web"; }
        }

        private string BuildTextSearchUrl(string query) =>
            string.Format(searchUrlTemplate, Uri.EscapeDataString(query));

        // Image search: Google Lens by URL; Bing/Yandex reverse image; others fall back
        // to a normal query of the image URL.
        private string BuildImageSearchUrl(string imageUrl)
        {
            var host = SearchHost();
            var enc = Uri.EscapeDataString(imageUrl);
            if (host.Contains("google."))
                return "https://lens.google.com/uploadbyurl?url=" + enc;
            if (host.Contains("bing."))
                return "https://www.bing.com/images/search?q=imgurl:" + enc + "&view=detailv2&iss=sbi";
            if (host.Contains("yandex."))
                return "https://yandex.com/images/search?rpt=imageview&url=" + enc;
            // Always offer Lens as a capable fallback when the default engine has no reverse-image vertical.
            return "https://lens.google.com/uploadbyurl?url=" + enc;
        }

        // Video search: Google/Bing support a video vertical; others fall back to a query.
        private string BuildVideoSearchUrl(string videoUrl)
        {
            var host = SearchHost();
            var enc = Uri.EscapeDataString(videoUrl);
            if (host.Contains("google."))
                return "https://www.google.com/search?q=" + enc + "&tbm=vid";
            if (host.Contains("bing."))
                return "https://www.bing.com/videos/search?q=" + enc;
            return BuildTextSearchUrl(videoUrl);
        }

        private string SearchHost()
        {
            try { return new Uri(string.Format(searchUrlTemplate, "x")).Host.ToLowerInvariant(); }
            catch { return ""; }
        }

        private void ReplaceNativeSaveAs(IList<CoreWebView2ContextMenuItem> items, string name, CoreWebView2 core, string uri)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.Kind == CoreWebView2ContextMenuItemKind.Submenu)
                {
                    try { ReplaceNativeSaveAs(it.Children, name, core, uri); } catch { }
                    continue;
                }
                if (!string.Equals(it.Name, name, StringComparison.Ordinal)) continue;
                if (string.IsNullOrWhiteSpace(uri) || sharedEnvironment == null) break;
                try
                {
                    var custom = sharedEnvironment.CreateContextMenuItem(it.Label, null, CoreWebView2ContextMenuItemKind.Command);
                    var capturedCore = core;
                    var capturedUri = uri;
                    custom.CustomItemSelected += (_, _) =>
                    {
                        try { BeginInvoke(new Action(() => { _ = SaveUrlWithDialogAsync(capturedCore, capturedUri); })); }
                        catch { }
                    };
                    items.RemoveAt(i);
                    items.Insert(i, custom);
                }
                catch { }
                break;
            }
        }

    }
}
