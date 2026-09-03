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
        private void Core_PermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
        {
            var kind = e.PermissionKind;
            if (kind == CoreWebView2PermissionKind.Camera || kind == CoreWebView2PermissionKind.Microphone)
            {
                string name = kind == CoreWebView2PermissionKind.Camera ? "camera" : "microphone";
                var r = MessageBox.Show(this, $"Allow this site to use your {name}?", "Permission",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                e.State = r == DialogResult.Yes
                    ? CoreWebView2PermissionState.Allow
                    : CoreWebView2PermissionState.Deny;
                return;
            }
            e.State = CoreWebView2PermissionState.Default;
        }

        private const string DisablePasskeyJs = @"
(function(){
  if (window.__gNoPasskey) return;
  window.__gNoPasskey = 1;
  try {
    if (window.PublicKeyCredential) {
      PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable = function(){ return Promise.resolve(false); };
      PublicKeyCredential.isConditionalMediationAvailable = function(){ return Promise.resolve(false); };
    }
  } catch(e) {}
  try {
    if (navigator.credentials) {
      var origGet = navigator.credentials.get.bind(navigator.credentials);
      var origCreate = navigator.credentials.create.bind(navigator.credentials);
      navigator.credentials.get = function(opts){
        if (opts && opts.publicKey)
          return Promise.reject(new DOMException('NotAllowedError'));
        return origGet(opts);
      };
      navigator.credentials.create = function(opts){
        if (opts && opts.publicKey)
          return Promise.reject(new DOMException('NotAllowedError'));
        return origCreate(opts);
      };
    }
  } catch(e) {}
})();";

        private void RefreshAddressSuggest()
        {
            addressSuggest.Clear();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void add(string? u)
            {
                if (string.IsNullOrWhiteSpace(u) || !seen.Add(u)) return;
                addressSuggest.Add(u);
            }
            foreach (var h in history) add(h);
            void walk(List<BookmarkNode> nodes)
            {
                foreach (var n in nodes)
                {
                    if (n.Type == "link") add(n.Href);
                    else walk(n.Children);
                }
            }
            walk(bookmarks);
        }

        private void LoadWindowState()
        {
            try
            {
                if (!File.Exists(configFile)) return;
                using var doc = JsonDocument.Parse(File.ReadAllText(configFile));
                if (!doc.RootElement.TryGetProperty("geometry", out var g)) return;
                int x = g.GetProperty("x").GetInt32();
                int y = g.GetProperty("y").GetInt32();
                int w = g.GetProperty("width").GetInt32();
                int h = g.GetProperty("height").GetInt32();
                bool max = g.TryGetProperty("maximized", out var m) && m.GetBoolean();
                StartPosition = FormStartPosition.Manual;
                Bounds = new Rectangle(x, y, Math.Max(600, w), Math.Max(400, h));
                if (max) WindowState = FormWindowState.Maximized;
            }
            catch { }
        }

        private void SaveWindowState()
        {
            try
            {
                var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                var json = JsonSerializer.Serialize(new
                {
                    geometry = new { x = b.X, y = b.Y, width = b.Width, height = b.Height, maximized = WindowState == FormWindowState.Maximized }
                }, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configFile, json);
            }
            catch { }
        }


        // ── Bookmarks ──
        // ── Settings ──
        private static readonly (string Name, string Home, string Search)[] SearchEngines = new[]
        {
            ("Google",      "https://www.google.com",       "https://www.google.com/search?q={0}"),
            ("Bing",        "https://www.bing.com",         "https://www.bing.com/search?q={0}"),
            ("DuckDuckGo",  "https://duckduckgo.com",       "https://duckduckgo.com/?q={0}"),
            ("Yahoo",       "https://search.yahoo.com",     "https://search.yahoo.com/search?p={0}"),
            ("Brave Search","https://search.brave.com",     "https://search.brave.com/search?q={0}"),
            ("Startpage",   "https://www.startpage.com",    "https://www.startpage.com/do/search?q={0}"),
        };

        private void LoadSettings()
        {
            if (!File.Exists(settingsFile)) return;
            try
            {
                foreach (var line in File.ReadAllLines(settingsFile))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length != 2) continue;
                    switch (parts[0].Trim().ToLower())
                    {
                        case "homepage": homePageUrl = parts[1].Trim(); break;
                        case "searchurl": searchUrlTemplate = parts[1].Trim(); break;
                    }
                }
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                File.WriteAllLines(settingsFile, new[]
                {
                    $"homepage={homePageUrl}",
                    $"searchurl={searchUrlTemplate}",
                });
            }
            catch { }
        }

        private void ShowSearchEnginePicker()
        {
            using var dlg = new Form
            {
                Text = "Choose Your Search Engine",
                ClientSize = new Size(360, 340),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Theme.ActiveTab,
                ForeColor = Color.White,
            };

            var label = new Label
            {
                Text = "Select your default search engine:",
                Location = new Point(20, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.White,
            };
            dlg.Controls.Add(label);

            var list = new ListBox
            {
                Location = new Point(20, 48),
                Size = new Size(320, 220),
                Font = new Font("Segoe UI", 11f),
                BackColor = Theme.TitleBar,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
            };
            foreach (var (name, _, _) in SearchEngines)
                list.Items.Add(name);
            list.SelectedIndex = 0;
            dlg.Controls.Add(list);

            var okBtn = new Button
            {
                Text = "OK",
                Location = new Point(240, 280),
                Size = new Size(100, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Accent,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 10f),
                DialogResult = DialogResult.OK,
            };
            okBtn.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(okBtn);
            dlg.AcceptButton = okBtn;

            if (dlg.ShowDialog(this) == DialogResult.OK && list.SelectedIndex >= 0)
            {
                var choice = SearchEngines[list.SelectedIndex];
                homePageUrl = choice.Home;
                searchUrlTemplate = choice.Search;
                SaveSettings();
                if (ActiveTab != null) NavigateCurrentTab(homePageUrl);
            }
            else
                SaveSettings();
        }

        // ── History ──
        private void LoadHistory()
        {
            if (!File.Exists(historyFile)) return;
            history.Clear();
            var lines = File.ReadAllLines(historyFile).Where(l => !string.IsNullOrWhiteSpace(l)).Distinct().ToList();
            history.AddRange(lines.Count <= 100 ? lines : lines.GetRange(lines.Count - 100, 100));
        }

        private void SaveHistory() { File.WriteAllLines(historyFile, history); }

        private void AddToHistory(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            history.RemoveAll(item => string.Equals(item, url, StringComparison.OrdinalIgnoreCase));
            history.Add(url);
            if (history.Count > 100) history.RemoveRange(0, history.Count - 100);
            SaveHistory();
            if (!addressSuggest.Contains(url)) addressSuggest.Add(url);
        }

        private void ClearHistory()
        {
            if (MessageBox.Show(this, "Clear all history?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            history.Clear(); SaveHistory(); statusLabel.Text = "History cleared.";
        }

        // ── Ad Blocker (powered by GSecurity Ad Shield + EasyList + EasyPrivacy) ──
    }
}
