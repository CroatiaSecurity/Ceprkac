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
        private void LoadBookmarks()
        {
            if (!File.Exists(bookmarksFile)) return;
            bookmarks.Clear();
            var stack = new Stack<List<BookmarkNode>>();
            stack.Push(bookmarks);
            foreach (var line in File.ReadAllLines(bookmarksFile).Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                var parts = line.Split(new[] { '\t' }, 3);
                // Fallback for old pipe-delimited format
                if (parts.Length < 2) parts = line.Split(new[] { '|' }, 3);
                var current = stack.Peek();
                if (parts[0] == "FOLDER" && parts.Length >= 2)
                {
                    var folder = new BookmarkNode { Type = "folder", Title = parts[1] };
                    current.Add(folder);
                    stack.Push(folder.Children);
                }
                else if (parts[0] == "ENDFOLDER")
                {
                    if (stack.Count > 1) stack.Pop();
                }
                else if (parts[0] == "LINK" && parts.Length >= 3)
                {
                    current.Add(new BookmarkNode { Type = "link", Title = parts[1], Href = parts[2] });
                }
                else
                {
                    // Legacy flat format: Title|Url
                    var legacy = line.Split(new[] { '|' }, 2);
                    if (legacy.Length == 2)
                        current.Add(new BookmarkNode { Type = "link", Title = legacy[0], Href = legacy[1] });
                    else
                        current.Add(new BookmarkNode { Type = "link", Title = GetDisplayTitle(line), Href = line });
                }
            }
        }

        private void SaveBookmarks()
        {
            var lines = new List<string>();
            WriteBookmarkNodes(lines, bookmarks);
            File.WriteAllLines(bookmarksFile, lines);
        }

        private static void WriteBookmarkNodes(List<string> lines, List<BookmarkNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.Type == "folder")
                {
                    lines.Add($"FOLDER\t{node.Title}");
                    WriteBookmarkNodes(lines, node.Children);
                    lines.Add("ENDFOLDER");
                }
                else
                {
                    lines.Add($"LINK\t{node.Title}\t{node.Href}");
                }
            }
        }

        private void AddCurrentPageBookmark()
        {
            var tab = ActiveTab; if (tab == null) return;
            var url = tab.WebView.Source?.AbsoluteUri ?? addressBox.Text;
            if (string.IsNullOrWhiteSpace(url)) return;
            if (RemoveBookmarkFromTree(bookmarks, url))
            {
                SaveBookmarks(); RefreshBookmarksBar();
                bookmarkBtn.StarFilled = false; bookmarkBtn.Invalidate();
                statusLabel.Text = "Bookmark removed.";
            }
            else
            {
                bookmarks.Insert(0, new BookmarkNode { Type = "link", Title = tab.Title ?? GetDisplayTitle(url), Href = url });
                SaveBookmarks(); RefreshBookmarksBar();
                bookmarkBtn.StarFilled = true; bookmarkBtn.Invalidate();
                statusLabel.Text = "Bookmark added.";
            }
        }

        private void RefreshBookmarksBar()
        {
            bookmarksBar.SuspendLayout();
            try
            {
                bookmarksBar.Items.Clear();
                foreach (var node in bookmarks)
                {
                    if (node.Type == "folder")
                    {
                        var dropDown = new ToolStripDropDownButton(node.Title)
                        {
                            ForeColor = Theme.ForeLight,
                            Font = bookmarksBar.Font,
                            DisplayStyle = ToolStripItemDisplayStyle.Text,
                        };
                        dropDown.DropDown.BackColor = Theme.ActiveTab;
                        dropDown.DropDown.ForeColor = Color.White;
                        AddChildrenToMenu(dropDown.DropDownItems, node.Children);
                        bookmarksBar.Items.Add(dropDown);
                    }
                    else
                    {
                        var btn = new ToolStripButton(node.Title)
                        {
                            ForeColor = Theme.ForeLight,
                            Font = bookmarksBar.Font,
                            DisplayStyle = ToolStripItemDisplayStyle.Text,
                            Tag = node.Href,
                        };
                        btn.Click += (_, _) => NavigateCurrentTab(node.Href);
                        bookmarksBar.Items.Add(btn);
                    }
                }
            }
            finally
            {
                bookmarksBar.ResumeLayout(true);
            }
        }

        private void AddChildrenToMenu(ToolStripItemCollection items, List<BookmarkNode> children)
        {
            foreach (var child in children)
            {
                if (child.Type == "folder")
                {
                    var sub = new ToolStripMenuItem(child.Title)
                    {
                        ForeColor = Color.White,
                        BackColor = Theme.ActiveTab,
                    };
                    AddChildrenToMenu(sub.DropDownItems, child.Children);
                    sub.DropDown.BackColor = Theme.ActiveTab;
                    sub.DropDown.ForeColor = Color.White;
                    items.Add(sub);
                }
                else
                {
                    var href = child.Href;
                    var item = new ToolStripMenuItem(child.Title)
                    {
                        ForeColor = Color.White,
                        BackColor = Theme.ActiveTab,
                    };
                    item.Click += (_, _) => NavigateCurrentTab(href);
                    items.Add(item);
                }
            }
        }

        private static bool BookmarkExistsInTree(List<BookmarkNode> nodes, string url)
        {
            foreach (var node in nodes)
            {
                if (node.Type == "link" && string.Equals(node.Href, url, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (node.Type == "folder" && BookmarkExistsInTree(node.Children, url))
                    return true;
            }
            return false;
        }

        private static bool RemoveBookmarkFromTree(List<BookmarkNode> nodes, string url)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Type == "link" && string.Equals(nodes[i].Href, url, StringComparison.OrdinalIgnoreCase))
                {
                    nodes.RemoveAt(i);
                    return true;
                }
                if (nodes[i].Type == "folder" && RemoveBookmarkFromTree(nodes[i].Children, url))
                    return true;
            }
            return false;
        }

        private void ClearBookmarks()
        {
            if (MessageBox.Show(this, "Clear all bookmarks?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            bookmarks.Clear(); SaveBookmarks(); RefreshBookmarksBar(); statusLabel.Text = "Bookmarks cleared.";
        }

        private void ImportBookmarksHtml()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Import Bookmarks",
                Filter = "Bookmark Files (*.html;*.htm)|*.html;*.htm|All Files|*.*",
                RestoreDirectory = true,
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var html = File.ReadAllText(dlg.FileName);
                var parsed = ParseBookmarksHtml(html);
                // If the top level is a single folder, unwrap it
                if (parsed.Count == 1 && parsed[0].Type == "folder")
                    parsed = parsed[0].Children;
                bookmarks.Clear();
                bookmarks.AddRange(parsed);
                SaveBookmarks();
                RefreshBookmarksBar();
                int count = CountLinks(bookmarks);
                statusLabel.Text = $"Imported {count} bookmarks.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Import failed:\r\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static List<BookmarkNode> ParseBookmarksHtml(string html)
        {
            // Find the first <DL> tag and parse recursively (Netscape bookmark format)
            int dlStart = html.IndexOf("<DL", StringComparison.OrdinalIgnoreCase);
            if (dlStart < 0) dlStart = html.IndexOf("<dl", StringComparison.Ordinal);
            if (dlStart >= 0)
                return ParseDL(html, ref dlStart);

            // Fallback: extract all <A> tags as flat links
            var result = new List<BookmarkNode>();
            int pos = 0;
            while (pos < html.Length)
            {
                int aStart = html.IndexOf("<A ", pos, StringComparison.OrdinalIgnoreCase);
                if (aStart < 0) aStart = html.IndexOf("<a ", pos, StringComparison.OrdinalIgnoreCase);
                if (aStart < 0) break;
                var (href, title, endPos) = ExtractATag(html, aStart);
                if (!string.IsNullOrWhiteSpace(href))
                    result.Add(new BookmarkNode { Type = "link", Title = title, Href = href });
                pos = endPos;
            }
            return result;
        }

        private static List<BookmarkNode> ParseDL(string html, ref int pos)
        {
            var nodes = new List<BookmarkNode>();
            // Skip past the opening <DL...> tag
            int tagEnd = html.IndexOf('>', pos);
            if (tagEnd < 0) return nodes;
            pos = tagEnd + 1;

            while (pos < html.Length)
            {
                // Skip whitespace and text
                int nextTag = html.IndexOf('<', pos);
                if (nextTag < 0) break;
                pos = nextTag;

                // Peek at the tag
                int closeAngle = html.IndexOf('>', pos);
                if (closeAngle < 0) break;
                string tag = html.Substring(pos, closeAngle - pos + 1);
                string tagUpper = tag.ToUpperInvariant();

                // End of this DL
                if (tagUpper.StartsWith("</DL"))
                {
                    pos = closeAngle + 1;
                    return nodes;
                }

                // Skip <DT>, <p>, <DD> opening tags
                if (tagUpper.StartsWith("<DT") || tagUpper.StartsWith("<P") || tagUpper.StartsWith("<DD"))
                {
                    pos = closeAngle + 1;
                    continue;
                }

                // Folder header: <H3...>title</H3>
                if (tagUpper.StartsWith("<H3") || tagUpper.StartsWith("<H1") || tagUpper.StartsWith("<H2"))
                {
                    pos = closeAngle + 1;
                    // Find closing </H3> (or </H1>, </H2>)
                    string closeTag = "</" + tag.Substring(1, 2) + ">";
                    int hEnd = html.IndexOf(closeTag, pos, StringComparison.OrdinalIgnoreCase);
                    if (hEnd < 0) { hEnd = html.IndexOf("</h3>", pos, StringComparison.OrdinalIgnoreCase); }
                    string folderTitle = "Folder";
                    if (hEnd > pos)
                    {
                        folderTitle = StripHtmlTags(html.Substring(pos, hEnd - pos)).Trim();
                        pos = hEnd + closeTag.Length;
                    }

                    // Look for the next <DL> which contains this folder's children
                    var children = new List<BookmarkNode>();
                    int searchLimit = Math.Min(pos + 200, html.Length);
                    int childDL = html.IndexOf("<DL", pos, searchLimit - pos, StringComparison.OrdinalIgnoreCase);
                    if (childDL < 0) childDL = html.IndexOf("<dl", pos, searchLimit - pos, StringComparison.OrdinalIgnoreCase);
                    if (childDL >= 0)
                    {
                        int dlPos = childDL;
                        children = ParseDL(html, ref dlPos);
                        pos = dlPos;
                    }

                    nodes.Add(new BookmarkNode { Type = "folder", Title = folderTitle, Children = children });
                    continue;
                }

                // Link: <A HREF="...">title</A>
                if (tagUpper.StartsWith("<A ") && tagUpper.Contains("HREF"))
                {
                    var (href, title, endPos) = ExtractATag(html, pos);
                    pos = endPos;
                    if (!string.IsNullOrWhiteSpace(href) && Uri.TryCreate(href, UriKind.Absolute, out _))
                        nodes.Add(new BookmarkNode { Type = "link", Title = string.IsNullOrWhiteSpace(title) ? GetDisplayTitle(href) : title, Href = href });
                    continue;
                }

                // Skip any other tag
                pos = closeAngle + 1;
            }
            return nodes;
        }

        private static (string href, string title, int endPos) ExtractATag(string html, int aStart)
        {
            int tagEnd = html.IndexOf('>', aStart);
            if (tagEnd < 0) return ("", "", aStart + 1);
            string tag = html.Substring(aStart, tagEnd - aStart + 1);

            string href = "";
            int hrefStart = tag.IndexOf("HREF=\"", StringComparison.OrdinalIgnoreCase);
            if (hrefStart < 0) hrefStart = tag.IndexOf("href=\"", StringComparison.Ordinal);
            if (hrefStart >= 0)
            {
                hrefStart = tag.IndexOf('"', hrefStart) + 1;
                int hrefEnd = tag.IndexOf('"', hrefStart);
                if (hrefEnd > hrefStart)
                    href = tag.Substring(hrefStart, hrefEnd - hrefStart).Trim();
            }

            string title = "";
            int aEnd = html.IndexOf("</A>", tagEnd, StringComparison.OrdinalIgnoreCase);
            if (aEnd < 0) aEnd = html.IndexOf("</a>", tagEnd, StringComparison.Ordinal);
            if (aEnd > tagEnd)
            {
                title = StripHtmlTags(html.Substring(tagEnd + 1, aEnd - tagEnd - 1)).Trim();
                return (href, title, aEnd + 4);
            }
            return (href, title, tagEnd + 1);
        }

        private static string StripHtmlTags(string s)
        {
            var sb = new StringBuilder();
            bool inTag = false;
            foreach (char c in s)
            {
                if (c == '<') { inTag = true; continue; }
                if (c == '>') { inTag = false; continue; }
                if (!inTag) sb.Append(c);
            }
            return sb.ToString();
        }

        private static int CountLinks(List<BookmarkNode> nodes)
        {
            int count = 0;
            foreach (var n in nodes)
            {
                if (n.Type == "link") count++;
                else if (n.Type == "folder") count += CountLinks(n.Children);
            }
            return count;
        }

        private void ExportBookmarksHtml()
        {
            using var dlg = new SaveFileDialog
            {
                Title = "Export Bookmarks",
                Filter = "Bookmark File (*.html)|*.html",
                FileName = "bookmarks.html",
                RestoreDirectory = true,
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                using var w = new StreamWriter(dlg.FileName, false, System.Text.Encoding.UTF8);
                w.WriteLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
                w.WriteLine("<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">");
                w.WriteLine("<TITLE>Bookmarks</TITLE>");
                w.WriteLine("<H1>Bookmarks</H1>");
                w.WriteLine("<DL><p>");
                WriteBookmarksHtml(w, bookmarks, "    ");
                w.WriteLine("</DL><p>");
                int count = CountLinks(bookmarks);
                statusLabel.Text = $"Exported {count} bookmarks.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Export failed:\r\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void WriteBookmarksHtml(StreamWriter w, List<BookmarkNode> nodes, string indent)
        {
            foreach (var node in nodes)
            {
                if (node.Type == "folder")
                {
                    var safeTitle = System.Net.WebUtility.HtmlEncode(node.Title);
                    w.WriteLine($"{indent}<DT><H3>{safeTitle}</H3>");
                    w.WriteLine($"{indent}<DL><p>");
                    WriteBookmarksHtml(w, node.Children, indent + "    ");
                    w.WriteLine($"{indent}</DL><p>");
                }
                else
                {
                    var safeTitle = System.Net.WebUtility.HtmlEncode(node.Title);
                    var safeUrl = System.Net.WebUtility.HtmlEncode(node.Href);
                    w.WriteLine($"{indent}<DT><A HREF=\"{safeUrl}\">{safeTitle}</A>");
                }
            }
        }

        private static string GetDisplayTitle(string url)
        {
            try { return new Uri(url).Host; } catch { return url.Length > 30 ? url.Substring(0, 27) + "..." : url; }
        }

    }
}
