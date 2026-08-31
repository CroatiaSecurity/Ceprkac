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
    // ───────────────────────── colour palette (Chrome-dark inspired) ─────────
    internal static class Theme
    {
        public static readonly Color TitleBar      = Color.FromArgb(32, 33, 36);
        public static readonly Color TabBar        = Color.FromArgb(32, 33, 36);
        public static readonly Color ActiveTab     = Color.FromArgb(53, 54, 58);
        public static readonly Color InactiveTab   = Color.FromArgb(40, 41, 45);
        public static readonly Color TabHover      = Color.FromArgb(48, 49, 53);
        public static readonly Color Toolbar       = Color.FromArgb(53, 54, 58);
        public static readonly Color AddressBox    = Color.FromArgb(41, 42, 45);
        public static readonly Color BookmarkBar   = Color.FromArgb(53, 54, 58);
        public static readonly Color StatusBar     = Color.FromArgb(32, 33, 36);
        public static readonly Color ForeLight     = Color.White;
        public static readonly Color ForeDim       = Color.FromArgb(180, 184, 190);
        public static readonly Color Accent        = Color.FromArgb(138, 180, 248);
        public static readonly Color CloseHover    = Color.FromArgb(200, 60, 60);
        public static readonly Color Border        = Color.FromArgb(60, 64, 67);
    }

    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripGradientBegin => Theme.Toolbar;
        public override Color ToolStripGradientMiddle => Theme.Toolbar;
        public override Color ToolStripGradientEnd => Theme.Toolbar;
        public override Color ToolStripBorder => Theme.Toolbar;
        public override Color ToolStripDropDownBackground => Theme.ActiveTab;
        public override Color MenuBorder => Theme.Border;
        public override Color MenuItemBorder => Theme.Border;
        public override Color MenuItemSelected => Theme.TabHover;
        public override Color MenuItemSelectedGradientBegin => Theme.TabHover;
        public override Color MenuItemSelectedGradientEnd => Theme.TabHover;
        public override Color ImageMarginGradientBegin => Theme.ActiveTab;
        public override Color ImageMarginGradientMiddle => Theme.ActiveTab;
        public override Color ImageMarginGradientEnd => Theme.ActiveTab;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Border;
        public override Color StatusStripGradientBegin => Theme.StatusBar;
        public override Color StatusStripGradientEnd => Theme.StatusBar;
        public override Color ButtonSelectedBorder => Theme.Border;
        public override Color ButtonSelectedHighlight => Theme.TabHover;
        public override Color ButtonSelectedGradientBegin => Theme.TabHover;
        public override Color ButtonSelectedGradientEnd => Theme.TabHover;
        public override Color OverflowButtonGradientBegin => Theme.Toolbar;
        public override Color OverflowButtonGradientMiddle => Theme.Toolbar;
        public override Color OverflowButtonGradientEnd => Theme.Toolbar;
    }

    internal sealed class DarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        public DarkToolStripRenderer() : base(new DarkColorTable()) { RoundedEdges = false; }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var b = new SolidBrush(e.ToolStrip is StatusStrip ? Theme.StatusBar : e.ToolStrip.BackColor);
            e.Graphics.FillRectangle(b, e.AffectedBounds);
        }
    }

    internal enum ChromeIconKind { Back, Forward, Reload, Go, Star, Download, Menu }

    /// <summary>Flat nav button. Icons are drawn as lines — WinForms GDI throws "Parameter is not valid" on several Unicode glyphs (↻ ≡) at 175% DPI.</summary>
    internal sealed class ChromeButton : Button
    {
        public ChromeIconKind Kind { get; }
        public bool StarFilled { get; set; }
        public string Badge { get; set; } = "";
        private bool _hover;

        public ChromeButton(ChromeIconKind kind)
        {
            Kind = kind;
            Text = "";
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Theme.Toolbar;
            ForeColor = Color.White;
            TabStop = false;
            Cursor = Cursors.Hand;
            Margin = new Padding(0);
            Padding = new Padding(0);
            UseVisualStyleBackColor = false;
            AutoSize = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                var rc = ClientRectangle;
                if (rc.Width < 4 || rc.Height < 4) return;
                using (var bg = new SolidBrush(_hover && Enabled ? Theme.TabHover : BackColor))
                    g.FillRectangle(bg, rc);
                var color = Enabled ? Color.White : Theme.ForeDim;
                int side = Math.Max(8, Math.Min(rc.Width, rc.Height) * 5 / 10);
                var icon = new Rectangle(rc.X + (rc.Width - side) / 2, rc.Y + (rc.Height - side) / 2, side, side);
                using var pen = new Pen(color, Math.Max(1.4f, side / 10f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                DrawKind(g, pen, color, icon);
                if (!string.IsNullOrEmpty(Badge))
                {
                    using var f = new Font("Segoe UI", Math.Max(8f, rc.Height / 3.5f), FontStyle.Regular, GraphicsUnit.Pixel);
                    TextRenderer.DrawText(g, Badge, f, rc, Theme.Accent,
                        TextFormatFlags.Bottom | TextFormatFlags.Right | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                }
            }
            catch { }
        }

        private void DrawKind(Graphics g, Pen pen, Color color, Rectangle r)
        {
            int x = r.X, y = r.Y, w = r.Width, h = r.Height;
            switch (Kind)
            {
                case ChromeIconKind.Back:
                    g.DrawLines(pen, new[] { new Point(x + w * 2 / 3, y + h / 6), new Point(x + w / 4, y + h / 2), new Point(x + w * 2 / 3, y + h * 5 / 6) });
                    break;
                case ChromeIconKind.Forward:
                    g.DrawLines(pen, new[] { new Point(x + w / 3, y + h / 6), new Point(x + w * 3 / 4, y + h / 2), new Point(x + w / 3, y + h * 5 / 6) });
                    break;
                case ChromeIconKind.Reload:
                    int t = Math.Max(2, w / 6);
                    g.DrawArc(pen, x + t, y + t, w - t * 2, h - t * 2, 45, 280);
                    g.DrawLines(pen, new[] {
                        new Point(x + w - t, y + h / 2),
                        new Point(x + w - t, y + t),
                        new Point(x + w * 2 / 3, y + t + 2)
                    });
                    break;
                case ChromeIconKind.Go:
                    using (var br = new SolidBrush(color))
                    {
                        g.FillPolygon(br, new[] {
                            new Point(x + w / 5, y + h / 6),
                            new Point(x + w * 4 / 5, y + h / 2),
                            new Point(x + w / 5, y + h * 5 / 6)
                        });
                    }
                    break;
                case ChromeIconKind.Star:
                    var star = StarPoints(r);
                    if (StarFilled) { using var br = new SolidBrush(Theme.Accent); g.FillPolygon(br, star); }
                    else g.DrawPolygon(pen, star);
                    break;
                case ChromeIconKind.Download:
                    g.DrawLine(pen, x + w / 2, y + h / 6, x + w / 2, y + h * 2 / 3);
                    g.DrawLines(pen, new[] { new Point(x + w / 4, y + h / 2), new Point(x + w / 2, y + h * 2 / 3), new Point(x + w * 3 / 4, y + h / 2) });
                    g.DrawLine(pen, x + w / 5, y + h * 5 / 6, x + w * 4 / 5, y + h * 5 / 6);
                    break;
                case ChromeIconKind.Menu:
                    int m = h / 5;
                    g.DrawLine(pen, x + w / 5, y + m * 1, x + w * 4 / 5, y + m * 1);
                    g.DrawLine(pen, x + w / 5, y + m * 2 + 1, x + w * 4 / 5, y + m * 2 + 1);
                    g.DrawLine(pen, x + w / 5, y + m * 4, x + w * 4 / 5, y + m * 4);
                    break;
            }
        }

        private static Point[] StarPoints(Rectangle r)
        {
            var pts = new Point[10];
            double cx = r.X + r.Width / 2.0, cy = r.Y + r.Height / 2.0;
            double orad = Math.Min(r.Width, r.Height) / 2.0, irad = orad * 0.4;
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 5;
                double rad = (i % 2 == 0) ? orad : irad;
                pts[i] = new Point((int)Math.Round(cx + rad * Math.Cos(a)), (int)Math.Round(cy + rad * Math.Sin(a)));
            }
            return pts;
        }
    }

    // ───────────────────────── tab data model ───────────────────────────────
    internal sealed class BrowserTab
    {
        public string Title { get; set; } = "New Tab";
        public string Url { get; set; } = "";
        public WebView2 WebView { get; set; } = null!;
        public bool IsLoading { get; set; }
        public int LoadProgress { get; set; }
        public double ZoomFactor { get; set; } = 1.0;
        public bool IsPopup { get; set; }
        public bool FocusOmnibox { get; set; }
        public DateTime LastAutoFillAttempt { get; set; } = DateTime.MinValue;
        public DateTime LastAutoFillFormsAttempt { get; set; } = DateTime.MinValue;
        // The URL the last credential autofill actually ran against. A genuinely new
        // URL (e.g. Google's identifier -> password page) must always re-attempt even
        // if it happens inside the time-based debounce window.
        public string LastAutoFillUrl { get; set; } = "";
        // Monotonic token identifying the most recent autofill loop. A loop only owns the
        // "in progress" state while its token is current; a newer invocation (for a new URL)
        // bumps the token, so the older loop self-cancels and does not clear the newer guard.
        public long AutoFillToken { get; set; }
        public bool AutoFillInProgress { get; set; }
        // The URL that the SourceChanged handler last kicked an autofill attempt for. Autofill
        // writes to input fields dispatch input/change events, which on some SPAs push a new
        // history entry -> SourceChanged fires again -> autofill re-runs -> events -> ... a
        // self-feeding loop that flickered the address bar. The SourceChanged handler only
        // re-triggers autofill when core.Source differs from this, breaking the loop.
        public string LastSourceAutoFillUrl { get; set; } = "";
    }

    // ───────────────────────── custom tab strip control ─────────────────────
    internal sealed class ChromeTabStrip : Control
    {
        public List<BrowserTab> Tabs { get; } = new();
        public int SelectedIndex { get; set; } = -1;
        public int HoverIndex { get; private set; } = -1;
        public int HoverCloseIndex { get; private set; } = -1;
        private Point? _dragStart;
        private int _dragTab = -1;

        public event EventHandler<int>? TabClicked;
        public event EventHandler<int>? TabCloseClicked;
        public event EventHandler? NewTabClicked;

        private const int TabHeight = 34;
        private const int TabMaxWidth = 240;
        private const int TabMinWidth = 60;
        private const int CloseSize = 16;
        private const int NewTabBtnWidth = 28;
        private const int TopPadding = 6;
        private const int LeftPadding = 8;

        private float _dpi = 1f;
        private int Dip(int v) => Math.Max(1, (int)Math.Round(v * _dpi));

        public ChromeTabStrip()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Theme.TabBar;
            Font = new Font("Segoe UI", 8.5f);
            ApplyDpiScale(1f);
        }

        /// <summary>Scale from 96-DPI design pixels (Qt-style device-independent height). Always pass monitorDpi/96.</summary>
        public void ApplyDpiScale(float scale)
        {
            if (scale < 0.5f || float.IsNaN(scale) || float.IsInfinity(scale)) scale = 1f;
            _dpi = scale;
            // Pixel fonts: GDI point fonts ignore PerMonitorV2 and stay 96-DPI tiny on 4K.
            Font = new Font("Segoe UI", Math.Max(10f, 13f * _dpi), FontStyle.Regular, GraphicsUnit.Pixel);
            int h = Dip(TabHeight + TopPadding + 2);
            MinimumSize = new Size(0, h);
            Height = h;
            Invalidate();
        }

        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            int min = Dip(TabHeight + TopPadding + 2);
            if (height < min) height = min;
            base.SetBoundsCore(x, y, width, height, specified);
        }

        private int GetTabWidth()
        {
            if (Tabs.Count == 0) return Dip(TabMaxWidth);
            int available = Width - Dip(LeftPadding) - Dip(NewTabBtnWidth) - Dip(16);
            int w = available / Math.Max(Tabs.Count, 1);
            return Math.Max(Dip(TabMinWidth), Math.Min(Dip(TabMaxWidth), w));
        }

        private Rectangle GetTabRect(int index)
        {
            int w = GetTabWidth();
            int x = Dip(LeftPadding) + index * (w + 1);
            return new Rectangle(x, Dip(TopPadding), w, Dip(TabHeight));
        }

        private Rectangle GetCloseRect(Rectangle tabRect)
        {
            int cs = Dip(CloseSize);
            int x = tabRect.Right - cs - Dip(8);
            int y = tabRect.Y + (tabRect.Height - cs) / 2;
            return new Rectangle(x, y, cs, cs);
        }

        private Rectangle GetNewTabRect()
        {
            int w = GetTabWidth();
            int x = Dip(LeftPadding) + Tabs.Count * (w + 1);
            return new Rectangle(x + Dip(4), Dip(TopPadding) + Dip(4), Dip(NewTabBtnWidth), Dip(TabHeight) - Dip(8));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try { PaintTabs(e.Graphics); }
            catch { }
        }

        private void PaintTabs(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.TabBar);

            for (int i = 0; i < Tabs.Count; i++)
            {
                if (i == SelectedIndex) continue;
                DrawTab(g, i);
            }
            if (SelectedIndex >= 0 && SelectedIndex < Tabs.Count)
                DrawTab(g, SelectedIndex);

            // New tab button (+)
            var newRect = GetNewTabRect();
            using (var brush = new SolidBrush(Theme.InactiveTab))
            using (var path = RoundedRect(newRect, Dip(8)))
                g.FillPath(brush, path);
            using (var pen = new Pen(Theme.ForeLight, Math.Max(1f, 1.5f * _dpi)))
            {
                int cx = newRect.X + newRect.Width / 2;
                int cy = newRect.Y + newRect.Height / 2;
                int arm = Dip(5);
                g.DrawLine(pen, cx - arm, cy, cx + arm, cy);
                g.DrawLine(pen, cx, cy - arm, cx, cy + arm);
            }

            // Bottom line under inactive area
            if (SelectedIndex >= 0 && SelectedIndex < Tabs.Count)
            {
                using var pen = new Pen(Theme.ActiveTab, 2);
                var selRect = GetTabRect(SelectedIndex);
                g.DrawLine(pen, 0, Height - 1, selRect.Left, Height - 1);
                g.DrawLine(pen, selRect.Right, Height - 1, Width, Height - 1);
            }
        }

        private void DrawTab(Graphics g, int index)
        {
            var rect = GetTabRect(index);
            bool active = index == SelectedIndex;
            bool hover = index == HoverIndex && !active;
            Color bg = active ? Theme.ActiveTab : (hover ? Theme.TabHover : Theme.InactiveTab);

            int radius = active ? Dip(10) : Dip(8);
            using (var path = RoundedRectTop(rect, radius))
            using (var brush = new SolidBrush(bg))
                g.FillPath(brush, path);

            var tab = Tabs[index];
            int textRight = rect.Right - Dip(CloseSize) - Dip(16);
            int textLeft = rect.X + Dip(12);
            var textRect = new Rectangle(textLeft, rect.Y + 2, textRight - textLeft, rect.Height - 2);
            var textColor = active ? Theme.ForeLight : Theme.ForeDim;
            TextRenderer.DrawText(g, tab.Title, Font, textRect, textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (Tabs.Count > 1 || active)
            {
                var closeRect = GetCloseRect(rect);
                bool closeHover = index == HoverCloseIndex;
                if (closeHover)
                {
                    using var closeBrush = new SolidBrush(Theme.CloseHover);
                    g.FillEllipse(closeBrush, closeRect);
                }
                using var closePen = new Pen(closeHover ? Color.White : Theme.ForeDim, Math.Max(1f, 1.2f * _dpi));
                int m = Dip(4);
                g.DrawLine(closePen, closeRect.X + m, closeRect.Y + m, closeRect.Right - m, closeRect.Bottom - m);
                g.DrawLine(closePen, closeRect.Right - m, closeRect.Y + m, closeRect.X + m, closeRect.Bottom - m);
            }

            if (tab.IsLoading)
            {
                using var loadPen = new Pen(Theme.Accent, 2);
                int pw = tab.LoadProgress > 0
                    ? Math.Max(1, (rect.Width - 8) * Math.Min(tab.LoadProgress, 100) / 100)
                    : (rect.Width - 8) / 3;
                g.DrawLine(loadPen, rect.X + 4, rect.Bottom - 2, rect.X + 4 + pw, rect.Bottom - 2);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragStart.HasValue && _dragTab >= 0)
            {
                int delta = e.X - _dragStart.Value.X;
                int tabW = GetTabWidth() + 1;
                if (Math.Abs(delta) > tabW / 2)
                {
                    int dir = delta > 0 ? 1 : -1;
                    int newIdx = _dragTab + dir;
                    if (newIdx >= 0 && newIdx < Tabs.Count)
                    {
                        (Tabs[_dragTab], Tabs[newIdx]) = (Tabs[newIdx], Tabs[_dragTab]);
                        if (SelectedIndex == _dragTab) SelectedIndex = newIdx;
                        else if (SelectedIndex == newIdx) SelectedIndex = _dragTab;
                        _dragTab = newIdx;
                        _dragStart = e.Location;
                        Invalidate();
                        TabClicked?.Invoke(this, SelectedIndex);
                    }
                }
                return;
            }
            int oldHover = HoverIndex, oldClose = HoverCloseIndex;
            HoverIndex = -1;
            HoverCloseIndex = -1;
            for (int i = 0; i < Tabs.Count; i++)
            {
                var rect = GetTabRect(i);
                if (rect.Contains(e.Location))
                {
                    HoverIndex = i;
                    if (GetCloseRect(rect).Contains(e.Location))
                        HoverCloseIndex = i;
                    break;
                }
            }
            if (oldHover != HoverIndex || oldClose != HoverCloseIndex) Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (HoverIndex != -1 || HoverCloseIndex != -1) { HoverIndex = -1; HoverCloseIndex = -1; Invalidate(); }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (GetNewTabRect().Contains(e.Location)) { NewTabClicked?.Invoke(this, EventArgs.Empty); return; }
            for (int i = 0; i < Tabs.Count; i++)
            {
                var rect = GetTabRect(i);
                if (!rect.Contains(e.Location)) continue;
                if (GetCloseRect(rect).Contains(e.Location)) TabCloseClicked?.Invoke(this, i);
                else TabClicked?.Invoke(this, i);
                return;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Middle)
            {
                for (int i = 0; i < Tabs.Count; i++)
                    if (GetTabRect(i).Contains(e.Location)) { TabCloseClicked?.Invoke(this, i); return; }
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                for (int i = 0; i < Tabs.Count; i++)
                {
                    var rect = GetTabRect(i);
                    if (rect.Contains(e.Location) && !GetCloseRect(rect).Contains(e.Location))
                    {
                        _dragStart = e.Location;
                        _dragTab = i;
                        break;
                    }
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragStart = null;
            _dragTab = -1;
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            if (r.Width < 2 || r.Height < 2)
            {
                if (r.Width > 0 && r.Height > 0) path.AddRectangle(r);
                return path;
            }
            int d = Math.Max(2, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath RoundedRectTop(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            if (r.Width < 2 || r.Height < 2)
            {
                if (r.Width > 0 && r.Height > 0) path.AddRectangle(r);
                return path;
            }
            int d = Math.Max(2, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
            path.CloseFigure();
            return path;
        }
    }

    // ───────────────────────── bookmark data model (tree) ──────────────────
    internal sealed class BookmarkNode
    {
        public string Type { get; set; } = "link"; // "link" or "folder"
        public string Title { get; set; } = "";
        public string Href { get; set; } = "";
        public List<BookmarkNode> Children { get; set; } = new();
    }

    internal sealed class DownloadItem
    {
        public string Filename { get; set; } = "";
        public string Path { get; set; } = "";
        public string Url { get; set; } = "";
        public long Received { get; set; }
        public long Total { get; set; }
        public string Status { get; set; } = "Downloading";
    }

    // ───────────────────────── main form ────────────────────────────────────
    public class MainForm : Form, IMessageFilter
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("user32.dll")]
        private static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
        private const uint GA_ROOT = 2;
        private const int SW_RESTORE = 9;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;
        private const int WM_DPICHANGED = 0x02E0;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_CHAR = 0x0102;
        private const int WM_DEADCHAR = 0x0103;
        private const int WM_UNICHAR = 0x0109;

        private readonly ChromeTabStrip tabStrip;
        private readonly Panel navPanel;
        private readonly TableLayoutPanel navLayout;
        private readonly Panel addressWrap;
        private readonly TextBox addressBox;
        private readonly ChromeButton goBtn;
        private readonly ChromeButton backBtn;
        private readonly ChromeButton fwdBtn;
        private readonly ChromeButton refreshBtn;
        private readonly ChromeButton bookmarkBtn;
        private readonly ChromeButton downloadsBtn;
        private readonly ChromeButton menuBtn;
        private readonly ContextMenuStrip menuStrip;
        private readonly ContextMenuStrip downloadsMenu;
        private readonly ToolTip chromeTip;
        private readonly ToolStrip bookmarksBar;
        private float _chromeDpiScale = 1f;
        private int _chromeDpi = 96;
        private Font? _navFont;
        private Font? _navFontLg;
        private Font? _addressFont;
        private Font? _bookmarkFont;
        private Font? _statusFont;
        private Font? _findFont;
        private readonly Panel webViewPanel;
        private readonly Panel findBar;
        private readonly TextBox findInput;
        private readonly ToolStripStatusLabel statusLabel;
        private readonly StatusStrip statusStrip;

        private readonly string appDataFolder;
        private readonly string bookmarksFile;
        private readonly string historyFile;
        private readonly string passwordsFile;
        private readonly string cardsFile;
        private readonly string addressesFile;
        private readonly string settingsFile;
        private readonly string downloadsFile;
        private readonly string configFile;
        private readonly List<BookmarkNode> bookmarks = new();
        private readonly List<string> history = new();
        private readonly List<SavedCredential> savedPasswords = new();
        private readonly List<SavedCard> savedCards = new();
        private readonly List<SavedAddress> savedAddresses = new();
        private readonly List<string> closedTabs = new();
        private readonly List<DownloadItem> downloads = new();
        private readonly AutoCompleteStringCollection addressSuggest = new();
        private string homePageUrl = "https://www.google.com";
        private string searchUrlTemplate = "https://www.google.com/search?q={0}";
        private CoreWebView2Environment? sharedEnvironment;
        private InjectedModuleCleaner? moduleCleaner;
        private DateTime lastProcessRecover = DateTime.MinValue;
        private readonly List<string> pendingExternalUrls = new();

        private BrowserTab? ActiveTab => tabStrip.SelectedIndex >= 0 && tabStrip.SelectedIndex < tabStrip.Tabs.Count
            ? tabStrip.Tabs[tabStrip.SelectedIndex] : null;

        public MainForm(IEnumerable<string>? startupUrls = null)
        {
            EnsureModuleCleaner();
            if (startupUrls != null)
            {
                foreach (var u in startupUrls)
                    if (!string.IsNullOrWhiteSpace(u)) pendingExternalUrls.Add(u.Trim());
            }
            Text = "Ceprkac";
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(1280, 860);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(600, 400);
            BackColor = Theme.TitleBar;

            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ceprkac.ico");
                if (File.Exists(iconPath))
                {
                    using var src = new Icon(iconPath);
                    Icon = (Icon)src.Clone();
                }
            }
            catch { }

            appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ceprkac");
            bookmarksFile = Path.Combine(appDataFolder, "bookmarks.txt");
            historyFile = Path.Combine(appDataFolder, "history.txt");
            passwordsFile = Path.Combine(appDataFolder, "passwords.dat");
            cardsFile = Path.Combine(appDataFolder, "cards.dat");
            addressesFile = Path.Combine(appDataFolder, "addresses.dat");
            settingsFile = Path.Combine(appDataFolder, "settings.txt");
            downloadsFile = Path.Combine(appDataFolder, "downloads.json");
            configFile = Path.Combine(appDataFolder, "config.json");

            // Tab strip
            tabStrip = new ChromeTabStrip { Dock = DockStyle.Top };
            tabStrip.TabClicked += (_, i) => SwitchToTab(i);
            tabStrip.TabCloseClicked += (_, i) => CloseTab(i);
            tabStrip.NewTabClicked += (_, _) => AddNewTab(homePageUrl);

            // Nav bar — GBrowser-style HBox: buttons keep their size, address stretches.
            // ToolStrip hosted the omnibox and clipped bookmark/downloads/menu at 4K 175%.
            var darkRenderer = new DarkToolStripRenderer();
            chromeTip = new ToolTip();

            navPanel = new Panel
            {
                Dock = DockStyle.Top,
                BackColor = Theme.Toolbar,
                Padding = new Padding(8, 4, 8, 4),
                Height = 44,
            };
            navLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 8,
                RowCount = 1,
                BackColor = Theme.Toolbar,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            navLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            navLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            navLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            navLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            navLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            navLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            navLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            navLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            navLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));

            backBtn = new ChromeButton(ChromeIconKind.Back);
            fwdBtn = new ChromeButton(ChromeIconKind.Forward);
            refreshBtn = new ChromeButton(ChromeIconKind.Reload);
            goBtn = new ChromeButton(ChromeIconKind.Go);
            bookmarkBtn = new ChromeButton(ChromeIconKind.Star);
            downloadsBtn = new ChromeButton(ChromeIconKind.Download);
            menuBtn = new ChromeButton(ChromeIconKind.Menu);
            chromeTip.SetToolTip(backBtn, "Back");
            chromeTip.SetToolTip(fwdBtn, "Forward");
            chromeTip.SetToolTip(refreshBtn, "Reload");
            chromeTip.SetToolTip(goBtn, "Go");
            chromeTip.SetToolTip(bookmarkBtn, "Bookmark (Ctrl+D)");
            chromeTip.SetToolTip(downloadsBtn, "Downloads");
            chromeTip.SetToolTip(menuBtn, "Menu");

            addressBox = new TextBox
            {
                BackColor = Theme.AddressBox,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 13f, FontStyle.Regular, GraphicsUnit.Pixel),
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill,
                AutoCompleteMode = AutoCompleteMode.None,
                AutoCompleteSource = AutoCompleteSource.CustomSource,
                AutoCompleteCustomSource = addressSuggest,
            };
            addressBox.KeyPress += (_, e) =>
            {
                if (char.IsControl(e.KeyChar)) return;
                // Enable suggest only after the first character is already in the box.
                // Turning it on during the first KeyPress recreates the edit handle and
                // swallows that character.
                if (addressBox.Text.Length >= 1 && addressBox.AutoCompleteMode != AutoCompleteMode.Suggest)
                    addressBox.AutoCompleteMode = AutoCompleteMode.Suggest;
            };
            addressBox.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.Handled = true;
                e.SuppressKeyPress = true;
                addressBox.AutoCompleteMode = AutoCompleteMode.None;
                NavigateCurrentTab(addressBox.Text);
                var t = ActiveTab;
                if (t != null) t.FocusOmnibox = false;
            };
            addressWrap = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Toolbar,
                Padding = new Padding(6, 4, 6, 4),
                Margin = Padding.Empty,
            };
            addressWrap.Controls.Add(addressBox);

            downloadsMenu = new ContextMenuStrip
            {
                BackColor = Theme.ActiveTab,
                ForeColor = Color.White,
                Renderer = darkRenderer,
                ShowImageMargin = false,
            };
            menuStrip = new ContextMenuStrip
            {
                BackColor = Theme.ActiveTab,
                ForeColor = Color.White,
                Renderer = darkRenderer,
                ShowImageMargin = false,
            };
            menuStrip.Items.Add(new ToolStripMenuItem("New Tab", null, (_, _) => AddNewTab(homePageUrl)) { ShortcutKeyDisplayString = "Ctrl+T", ForeColor = Color.White, BackColor = Theme.ActiveTab });
            menuStrip.Items.Add(new ToolStripMenuItem("Reopen Closed Tab", null, (_, _) => RestoreClosedTab()) { ShortcutKeyDisplayString = "Ctrl+Shift+T", ForeColor = Color.White, BackColor = Theme.ActiveTab });
            menuStrip.Items.Add(new ToolStripSeparator());
            menuStrip.Items.Add(new ToolStripMenuItem("Find in Page...", null, (_, _) => ToggleFindBar()) { ShortcutKeyDisplayString = "Ctrl+F", ForeColor = Color.White, BackColor = Theme.ActiveTab });
            menuStrip.Items.Add(new ToolStripMenuItem("Zoom In", null, (_, _) => ZoomBy(0.1)) { ShortcutKeyDisplayString = "Ctrl+Plus", ForeColor = Color.White, BackColor = Theme.ActiveTab });
            menuStrip.Items.Add(new ToolStripMenuItem("Zoom Out", null, (_, _) => ZoomBy(-0.1)) { ShortcutKeyDisplayString = "Ctrl+Minus", ForeColor = Color.White, BackColor = Theme.ActiveTab });
            menuStrip.Items.Add(new ToolStripMenuItem("Reset Zoom", null, (_, _) => ZoomReset()) { ShortcutKeyDisplayString = "Ctrl+0", ForeColor = Color.White, BackColor = Theme.ActiveTab });
            menuStrip.Items.Add(new ToolStripSeparator());
            menuStrip.Items.Add(new ToolStripMenuItem("Add Bookmark", null, (_, _) => AddCurrentPageBookmark()) { ShortcutKeys = Keys.Control | Keys.D, ForeColor = Color.White, BackColor = Theme.ActiveTab });
            menuStrip.Items.Add(new ToolStripMenuItem("Import Bookmarks...", null, (_, _) => ImportBookmarksHtml()) { ForeColor = Color.White, BackColor = Theme.ActiveTab });
            menuStrip.Items.Add(new ToolStripMenuItem("Export Bookmarks...", null, (_, _) => ExportBookmarksHtml()) { ForeColor = Color.White, BackColor = Theme.ActiveTab });
            menuStrip.Items.Add(new ToolStripMenuItem("Clear Bookmarks", null, (_, _) => ClearBookmarks()) { ForeColor = Color.White, BackColor = Theme.ActiveTab });
            menuStrip.Items.Add(new ToolStripSeparator());
            menuStrip.Items.Add(new ToolStripMenuItem("Clear History", null, (_, _) => ClearHistory()) { ForeColor = Color.White, BackColor = Theme.ActiveTab });
            menuStrip.Items.Add(new ToolStripSeparator());
            menuStrip.Items.Add(new ToolStripMenuItem("Import Passwords (CSV)...", null, (_, _) => ImportPasswordsCsv()) { ForeColor = Color.White, BackColor = Theme.ActiveTab });
            menuStrip.Items.Add(new ToolStripMenuItem("Clear Saved Passwords", null, (_, _) => ClearPasswords()) { ForeColor = Color.White, BackColor = Theme.ActiveTab });
            menuStrip.Items.Add(new ToolStripSeparator());
            menuStrip.Items.Add(new ToolStripMenuItem("Payment Methods...", null, (_, _) => ManageCards()) { ForeColor = Color.White, BackColor = Theme.ActiveTab });
            menuStrip.Items.Add(new ToolStripMenuItem("Addresses...", null, (_, _) => ManageAddresses()) { ForeColor = Color.White, BackColor = Theme.ActiveTab });
            menuStrip.Items.Add(new ToolStripSeparator());
            menuStrip.Items.Add(new ToolStripMenuItem("DevTools", null, (_, _) => ActiveTab?.WebView.CoreWebView2?.OpenDevToolsWindow()) { ShortcutKeys = Keys.Control | Keys.I, ForeColor = Color.White, BackColor = Theme.ActiveTab });
            menuStrip.Items.Add(new ToolStripMenuItem("Change Search Engine...", null, (_, _) => { ShowSearchEnginePicker(); }) { ForeColor = Color.White, BackColor = Theme.ActiveTab });
            menuStrip.Items.Add(new ToolStripMenuItem("Set as Default Browser...", null, (_, _) => SetAsDefaultBrowser()) { ForeColor = Color.White, BackColor = Theme.ActiveTab });
            menuStrip.Items.Add(new ToolStripSeparator());
            menuStrip.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => Close()) { ForeColor = Color.White, BackColor = Theme.ActiveTab });

            backBtn.Click += (_, _) => { var c = ActiveTab?.WebView.CoreWebView2; if (c?.CanGoBack == true) c.GoBack(); };
            fwdBtn.Click += (_, _) => { var c = ActiveTab?.WebView.CoreWebView2; if (c?.CanGoForward == true) c.GoForward(); };
            refreshBtn.Click += (_, _) => ActiveTab?.WebView.CoreWebView2?.Reload();
            goBtn.Click += (_, _) => NavigateCurrentTab(addressBox.Text);
            bookmarkBtn.Click += (_, _) => AddCurrentPageBookmark();
            downloadsBtn.Click += (_, _) =>
            {
                RebuildDownloadsMenu();
                downloadsMenu.Show(downloadsBtn, new Point(0, downloadsBtn.Height));
            };
            menuBtn.Click += (_, _) => menuStrip.Show(menuBtn, new Point(0, menuBtn.Height));

            void HostNav(Control c, int col)
            {
                c.Dock = DockStyle.Fill;
                navLayout.Controls.Add(c, col, 0);
            }
            HostNav(backBtn, 0);
            HostNav(fwdBtn, 1);
            HostNav(refreshBtn, 2);
            HostNav(addressWrap, 3);
            HostNav(goBtn, 4);
            HostNav(bookmarkBtn, 5);
            HostNav(downloadsBtn, 6);
            HostNav(menuBtn, 7);
            navPanel.Controls.Add(navLayout);

            Shown += (_, _) => ApplyChromeDpi();
            HandleCreated += (_, _) => ApplyChromeDpi();
            DpiChanged += MainForm_DpiChanged;

            // Bookmarks bar (ToolStrip for nested folder support)
            bookmarksBar = new ToolStrip
            {
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                BackColor = Theme.BookmarkBar,
                ForeColor = Color.White,
                Renderer = darkRenderer,
                Padding = new Padding(4, 2, 4, 2),
                AutoSize = false,
                Height = 30,
                Font = new Font("Segoe UI", 8f),
                CanOverflow = true,
                LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow,
            };

            findBar = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Theme.Toolbar, Visible = false };
            findInput = new TextBox { Left = 8, Top = 4, Width = 280, BackColor = Theme.AddressBox, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            findInput.KeyDown += FindInput_KeyDown;
            var findNext = new Button { Text = "Next", Left = 296, Top = 3, Width = 60, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Theme.ActiveTab };
            var findPrev = new Button { Text = "Prev", Left = 360, Top = 3, Width = 60, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Theme.ActiveTab };
            var findClose = new Button { Text = "×", Left = 424, Top = 3, Width = 32, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Theme.ActiveTab };
            findNext.Click += (_, _) => FindInPage(false);
            findPrev.Click += (_, _) => FindInPage(true);
            findClose.Click += (_, _) => { findBar.Visible = false; };
            findBar.Controls.AddRange(new Control[] { findInput, findNext, findPrev, findClose });

            // WebView panel
            webViewPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.ActiveTab };

            // Status bar
            statusLabel = new ToolStripStatusLabel("Ready") { ForeColor = Theme.ForeDim };
            statusStrip = new StatusStrip { BackColor = Theme.StatusBar, Renderer = darkRenderer, SizingGrip = false, AutoSize = false, Height = 22 };
            statusStrip.Items.Add(statusLabel);

            // Layout (reverse dock order)
            Controls.Add(webViewPanel);
            Controls.Add(findBar);
            Controls.Add(bookmarksBar);
            Controls.Add(navPanel);
            Controls.Add(tabStrip);
            Controls.Add(statusStrip);

            KeyPreview = true;
            KeyDown += MainForm_KeyDown;
            Application.AddMessageFilter(this);
            Load += (_, _) => InitializeAsync();
            FormClosing += (_, _) =>
            {
                Application.RemoveMessageFilter(this);
                SaveWindowState();
                moduleCleaner?.Stop();
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int v = 1; DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, sizeof(int)); } catch { }
        }

        // WinForms + WebView2 will otherwise scale chrome on every DPI message (compound or collapse to 96).
        protected override void ScaleControl(SizeF factor, BoundsSpecified specified) { }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_DPICHANGED && IsHandleCreated)
            {
                int proposed = (int)(m.WParam.ToInt64() & 0xFFFF);
                int monitorDpi = ReadMonitorDpi();
                // WebView2 posts WM_DPICHANGED 96 while the window sits on a 175% monitor.
                // Applying that shrinks tabs/toolbar to unusable 96-DPI sizes and eats the buttons.
                if (proposed > 0 && monitorDpi >= 96 && Math.Abs(proposed - monitorDpi) > 12)
                {
                    ApplyChromeDpi();
                    return;
                }
            }
            base.WndProc(ref m);
        }

        private void MainForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.Shift && e.KeyCode == Keys.T) { RestoreClosedTab(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.T) { AddNewTab(homePageUrl); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.W) { if (tabStrip.SelectedIndex >= 0) CloseTab(tabStrip.SelectedIndex); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.L) { addressBox.Focus(); addressBox.SelectAll(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.F) { ToggleFindBar(); e.Handled = true; }
            else if (e.Control && (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add)) { ZoomBy(0.1); e.Handled = true; }
            else if (e.Control && (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract)) { ZoomBy(-0.1); e.Handled = true; }
            else if (e.Control && (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0)) { ZoomReset(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape && findBar.Visible) { findBar.Visible = false; e.Handled = true; }
            else if (e.KeyCode == Keys.Escape && ActiveTab?.FocusOmnibox == true)
            {
                ActiveTab.FocusOmnibox = false;
                try { ActiveTab.WebView.Focus(); } catch { }
                e.Handled = true;
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.Tab)
            {
                if (tabStrip.Tabs.Count > 1) SwitchToTab((tabStrip.SelectedIndex - 1 + tabStrip.Tabs.Count) % tabStrip.Tabs.Count);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Tab)
            {
                if (tabStrip.Tabs.Count > 1) SwitchToTab((tabStrip.SelectedIndex + 1) % tabStrip.Tabs.Count);
                e.Handled = true;
            }
        }

        private async void InitializeAsync()
        {
            try
            {
                Directory.CreateDirectory(appDataFolder);
                LoadSettings();
                if (!File.Exists(settingsFile))
                    ShowSearchEnginePicker();
                LoadBookmarks();
                LoadHistory();
                LoadPasswords();
                LoadCards();
                LoadAddresses();
                LoadDownloads();
                LoadWindowState();
                RefreshBookmarksBar();
                RefreshAddressSuggest();

                // Load or download ad blocklist
                await LoadOrUpdateBlocklistAsync();

                var userDataFolder = Path.Combine(appDataFolder, "WebView2UserData");
                Directory.CreateDirectory(userDataFolder);

                if (!await EnsureWebView2RuntimeAsync())
                    return;

                try
                {
                    var envOpts = new CoreWebView2EnvironmentOptions(
                        "--no-first-run --disable-sync --disable-background-networking --disable-features=msSmartScreenProtection");
                    sharedEnvironment = await CoreWebView2Environment.CreateAsync(null, userDataFolder, envOpts);
                }
                catch (Exception createEx)
                {
                    statusLabel.Text = "WebView2 runtime missing or broken — repairing…";
                    Refresh();
                    if (await InstallWebView2RuntimeAsync() && !AlreadyRestartedForWebView2)
                    {
                        RestartApp("--after-webview2");
                        return;
                    }
                    throw new Exception(createEx.Message, createEx);
                }
                if (pendingExternalUrls.Count > 0)
                {
                    var urls = pendingExternalUrls.ToArray();
                    pendingExternalUrls.Clear();
                    foreach (var u in urls) AddNewTab(u, focusOmnibox: false);
                }
                else
                    AddNewTab(homePageUrl);
                // WebView2 can post a fake 96-DPI message during init — re-assert chrome after the first tab.
                BeginInvoke(new Action(ApplyChromeDpi));
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Failed to initialize WebView2.";
                MessageBox.Show(this, $"WebView2 initialization failed:\r\n{ex.Message}\r\n\r\n{ex.StackTrace}",
                    "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void EnableTls12()
        {
            try
            {
                System.Net.ServicePointManager.SecurityProtocol |=
                    System.Net.SecurityProtocolType.Tls12 | (System.Net.SecurityProtocolType)3072;
            }
            catch { }
        }

        private const string WebView2ClientGuid = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

        private static bool IsWebView2InRegistry()
        {
            string[] keys =
            {
                @"SOFTWARE\Microsoft\EdgeUpdate\Clients\" + WebView2ClientGuid,
                @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\" + WebView2ClientGuid,
            };
            foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                foreach (var key in keys)
                {
                    try
                    {
                        using var k = hive.OpenSubKey(key);
                        var pv = k?.GetValue("pv") as string;
                        if (!string.IsNullOrEmpty(pv) && pv != "0.0.0.0") return true;
                    }
                    catch { }
                }
            }
            return false;
        }

        private static bool IsWebView2RuntimeInstalled()
        {
            try
            {
                var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                if (!string.IsNullOrEmpty(version)) return true;
            }
            catch { }
            return IsWebView2InRegistry();
        }

        private static bool AlreadyRestartedForWebView2 =>
            Environment.GetCommandLineArgs().Any(a =>
                string.Equals(a, "--after-webview2", StringComparison.OrdinalIgnoreCase));

        private async Task<bool> EnsureWebView2RuntimeAsync()
        {
            bool apiOk = false;
            try { apiOk = !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString()); } catch { }

            if (apiOk) return true;

            // Installed for this machine but this process cannot see it yet (bitness / loader).
            if (IsWebView2InRegistry())
            {
                if (!AlreadyRestartedForWebView2)
                {
                    RestartApp("--after-webview2");
                    return false;
                }
                return true;
            }

            statusLabel.Text = "WebView2 not found — downloading runtime from Microsoft…";
            Refresh();
            if (!await InstallWebView2RuntimeAsync())
            {
                var retry = MessageBox.Show(this,
                    "The Edge WebView2 runtime is required and could not be installed automatically.\r\n\r\nRetry download?",
                    "WebView2 Required", MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);
                if (retry == DialogResult.Retry)
                    return await EnsureWebView2RuntimeAsync();
                statusLabel.Text = "WebView2 runtime is required.";
                return false;
            }

            if (!AlreadyRestartedForWebView2)
            {
                RestartApp("--after-webview2");
                return false;
            }
            return IsWebView2RuntimeInstalled();
        }

        private async Task<bool> InstallWebView2RuntimeAsync()
        {
            EnableTls12();
            var bootstrapperPath = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");
            try
            {
                statusLabel.Text = "Downloading WebView2 runtime…";
                Refresh();
                byte[]? bytes = null;
                try
                {
                    using (var http = new HttpClient())
                    {
                        http.Timeout = TimeSpan.FromMinutes(5);
                        bytes = await http.GetByteArrayAsync(
                            "https://go.microsoft.com/fwlink/p/?LinkId=2124703");
                    }
                }
                catch
                {
                    using (var wc = new System.Net.WebClient())
                        bytes = await wc.DownloadDataTaskAsync(
                            "https://go.microsoft.com/fwlink/p/?LinkId=2124703");
                }
                if (bytes == null || bytes.Length < 10000) return false;
                File.WriteAllBytes(bootstrapperPath, bytes);

                statusLabel.Text = "Installing WebView2 runtime…";
                Refresh();
                await RunWebView2Setup(bootstrapperPath, "/silent /install", false);
                if (!IsWebView2InRegistry() && !IsWebView2RuntimeInstalled())
                    await RunWebView2Setup(bootstrapperPath, "/install", true);

                for (int i = 0; i < 20 && !IsWebView2InRegistry() && !IsWebView2RuntimeInstalled(); i++)
                {
                    statusLabel.Text = "Waiting for WebView2 runtime…";
                    await Task.Delay(500);
                }
                return IsWebView2InRegistry() || IsWebView2RuntimeInstalled();
            }
            catch
            {
                return false;
            }
            finally
            {
                try { File.Delete(bootstrapperPath); } catch { }
            }
        }

        private static async Task<bool> RunWebView2Setup(string path, string args, bool elevate)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = args,
                    UseShellExecute = elevate,
                    CreateNoWindow = !elevate,
                };
                if (elevate) psi.Verb = "runas";
                var proc = Process.Start(psi);
                if (proc == null) return false;
                await Task.Run(() => proc.WaitForExit());
                return IsWebView2RuntimeInstalled();
            }
            catch
            {
                return false;
            }
        }

        private void RestartApp(string extraArg = "")
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    Arguments = extraArg ?? "",
                    UseShellExecute = true,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                });
            }
            catch { }
            BeginInvoke(new Action(Close));
        }

        private void EnsureModuleCleaner()
        {
            if (moduleCleaner != null) return;
            moduleCleaner = InjectedModuleCleaner.Instance ?? InjectedModuleCleaner.StartGlobal();
        }

        private void MainForm_DpiChanged(object? sender, DpiChangedEventArgs e)
        {
            int proposed = e.DeviceDpiNew;
            int monitorDpi = ReadMonitorDpi();
            if (proposed > 0 && monitorDpi >= 96 && Math.Abs(proposed - monitorDpi) > 12)
            {
                ApplyChromeDpi();
                return;
            }
            Bounds = new Rectangle(
                e.SuggestedRectangle.Left, e.SuggestedRectangle.Top,
                e.SuggestedRectangle.Width, e.SuggestedRectangle.Height);
            ApplyChromeDpi();
        }

        /// <summary>Monitor effective DPI. WebView2's window DPI is often 96 even on a 175% display.</summary>
        private int ReadMonitorDpi()
        {
            try
            {
                if (IsHandleCreated)
                {
                    var mon = MonitorFromWindow(Handle, MONITOR_DEFAULTTONEAREST);
                    if (mon != IntPtr.Zero && GetDpiForMonitor(mon, MDT_EFFECTIVE_DPI, out uint x, out _) == 0 && x >= 96)
                        return (int)Math.Min(x, 384);
                }
            }
            catch { }
            try
            {
                if (IsHandleCreated)
                {
                    uint w = GetDpiForWindow(Handle);
                    if (w >= 96 && w <= 384) return (int)w;
                }
            }
            catch { }
            try { if (DeviceDpi >= 96) return DeviceDpi; } catch { }
            return 96;
        }

        private int Dip(int v) => Math.Max(1, (int)Math.Round(v * _chromeDpiScale));

        private static Font UiPx(float px96, float scale)
            => new Font("Segoe UI", Math.Max(8f, px96 * scale), FontStyle.Regular, GraphicsUnit.Pixel);

        private void ApplyChromeDpi()
        {
            if (IsDisposed || !IsHandleCreated) return;
            int dpi = ReadMonitorDpi();
            if (dpi < 96) dpi = 96;
            _chromeDpi = dpi;
            _chromeDpiScale = dpi / 96f;

            MinimumSize = new Size(Dip(600), Dip(400));
            tabStrip.ApplyDpiScale(_chromeDpiScale);

            int btn = Dip(36);
            int navH = Dip(44);
            navPanel.Padding = new Padding(Dip(8), Dip(4), Dip(8), Dip(4));
            navPanel.MinimumSize = new Size(0, navH);
            navPanel.Height = navH;

            navLayout.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, btn);
            navLayout.ColumnStyles[1] = new ColumnStyle(SizeType.Absolute, btn);
            navLayout.ColumnStyles[2] = new ColumnStyle(SizeType.Absolute, btn);
            navLayout.ColumnStyles[3] = new ColumnStyle(SizeType.Percent, 100f);
            navLayout.ColumnStyles[4] = new ColumnStyle(SizeType.Absolute, btn);
            navLayout.ColumnStyles[5] = new ColumnStyle(SizeType.Absolute, btn);
            navLayout.ColumnStyles[6] = new ColumnStyle(SizeType.Absolute, btn);
            navLayout.ColumnStyles[7] = new ColumnStyle(SizeType.Absolute, btn);

            var oldNav = _navFont;
            var oldNavLg = _navFontLg;
            var oldAddr = _addressFont;
            var oldBm = _bookmarkFont;
            var oldSt = _statusFont;
            var oldFind = _findFont;
            _navFont = UiPx(16f, _chromeDpiScale);
            _navFontLg = UiPx(18f, _chromeDpiScale);
            _addressFont = UiPx(14f, _chromeDpiScale);
            _bookmarkFont = UiPx(12f, _chromeDpiScale);
            _statusFont = UiPx(11f, _chromeDpiScale);
            _findFont = UiPx(12f, _chromeDpiScale);

            foreach (var b in new[] { backBtn, fwdBtn, goBtn, bookmarkBtn, downloadsBtn })
            {
                b.Font = _navFont;
                b.MinimumSize = new Size(btn, Dip(28));
            }
            refreshBtn.Font = _navFontLg;
            refreshBtn.MinimumSize = new Size(btn, Dip(28));
            menuBtn.Font = _navFontLg;
            menuBtn.MinimumSize = new Size(btn, Dip(28));
            addressBox.Font = _addressFont;
            addressWrap.Padding = new Padding(Dip(6), Dip(4), Dip(6), Dip(4));

            bookmarksBar.ImageScalingSize = new Size(Dip(16), Dip(16));
            bookmarksBar.Padding = new Padding(Dip(4), Dip(2), Dip(4), Dip(2));
            bookmarksBar.Font = _bookmarkFont;
            bookmarksBar.Height = Dip(30);
            bookmarksBar.MinimumSize = new Size(0, Dip(28));
            foreach (ToolStripItem item in bookmarksBar.Items)
                item.Font = _bookmarkFont;
            menuStrip.Font = _bookmarkFont;
            downloadsMenu.Font = _bookmarkFont;

            statusStrip.AutoSize = false;
            statusStrip.Height = Dip(22);
            statusLabel.Font = _statusFont;

            findBar.Height = Dip(32);
            foreach (Control c in findBar.Controls)
            {
                c.Top = Dip(3);
                c.Height = Dip(24);
                c.Font = _findFont;
            }
            if (findBar.Controls.Count >= 4)
            {
                findBar.Controls[0].Left = Dip(8);
                findBar.Controls[0].Width = Dip(280);
                findBar.Controls[1].Left = Dip(296);
                findBar.Controls[1].Width = Dip(60);
                findBar.Controls[2].Left = Dip(362);
                findBar.Controls[2].Width = Dip(60);
                findBar.Controls[3].Left = Dip(428);
                findBar.Controls[3].Width = Dip(32);
            }
            navLayout.PerformLayout();
            Invalidate(true);
            DisposeFont(oldNav);
            DisposeFont(oldNavLg);
            DisposeFont(oldAddr);
            DisposeFont(oldBm);
            DisposeFont(oldSt);
            DisposeFont(oldFind);
        }

        private static void DisposeFont(Font? f)
        {
            if (f == null) return;
            try { f.Dispose(); } catch { }
        }

        private void SetAddressText(string? url)
        {
            url = url ?? "";
            // Never clobber what the user is actively typing/selecting in the box.
            if (addressBox.Focused) return;
            if (addressBox.Text == url) return;
            addressBox.AutoCompleteMode = AutoCompleteMode.None;
            addressBox.Text = url;
            addressBox.SelectionStart = addressBox.Text.Length;
            addressBox.SelectionLength = 0;
        }

        // The address bar is a normal WinForms TextBox and handles its own input.
        // The old custom keystroke-redirection (WM_CHAR/WM_KEYDOWN → ApplyOmniboxChar)
        // fought WinForms focus + AutoComplete handle recreation and dropped the first
        // character. Input now flows straight to the focused TextBox. This filter is
        // retained only to satisfy IMessageFilter and does not intercept anything.
        public bool PreFilterMessage(ref Message m) => false;

        // Focus the address bar and select its contents so the first keystroke
        // replaces the pre-filled URL (standard browser omnibox behavior). Typing
        // is handled natively by the TextBox — no manual character routing.
        private void FocusAddressBar(bool selectAll = true)
        {
            if (addressBox.IsDisposed) return;
            addressBox.Focus();
            if (selectAll) addressBox.SelectAll();
            else
            {
                addressBox.SelectionLength = 0;
                addressBox.SelectionStart = addressBox.Text.Length;
            }
        }

        public void RestoreAndFocus()
        {
            if (IsDisposed) return;
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Show();
            Activate();
            BringToFront();
            try
            {
                ShowWindow(Handle, SW_RESTORE);
                SetForegroundWindow(Handle);
            }
            catch { }
        }

        public void OpenExternalUrl(string url)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OpenExternalUrl(url)));
                return;
            }
            RestoreAndFocus();
            if (string.IsNullOrWhiteSpace(url)) return;
            if (sharedEnvironment == null)
            {
                pendingExternalUrls.Add(url);
                return;
            }
            AddNewTab(url, focusOmnibox: false);
        }

        private void SetAsDefaultBrowser()
        {
            try
            {
                BrowserRegistration.RegisterAndRequestDefault();
                statusLabel.Text = BrowserRegistration.IsDefault()
                    ? "Ceprkac is the default browser."
                    : "Pick Ceprkac under http / https in Windows Settings.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not register as default browser:\r\n" + ex.Message,
                    "Ceprkac", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void AddNewTab(string url, int? insertAfter = null, bool focusOmnibox = true)
        {
            if (sharedEnvironment == null) return;
            var webView = new WebView2
            {
                Dock = DockStyle.Fill,
                Visible = true,
                TabStop = false,
                DefaultBackgroundColor = Theme.ActiveTab,
            };
            var tab = new BrowserTab { Url = url, WebView = webView, FocusOmnibox = focusOmnibox };

            int insertIndex = insertAfter.HasValue ? insertAfter.Value + 1
                : tabStrip.SelectedIndex >= 0 ? tabStrip.SelectedIndex + 1
                : tabStrip.Tabs.Count;

            tabStrip.Tabs.Insert(insertIndex, tab);
            webViewPanel.Controls.Add(webView);
            _ = webView.Handle;
            webView.GotFocus += (_, _) =>
            {
                // Only for a freshly opened blank tab: pull focus to the omnibox once,
                // then release so the user can click into the page normally afterwards.
                if (!tab.FocusOmnibox) return;
                tab.FocusOmnibox = false;
                try { FocusAddressBar(selectAll: true); } catch { }
            };

            SwitchToTab(insertIndex);
            if (focusOmnibox) FocusAddressBar(selectAll: true);

            try
            {
                await webView.EnsureCoreWebView2Async(sharedEnvironment);
                var core = webView.CoreWebView2;
                if (core != null)
                {
                    core.NavigationStarting += (_, _) => { tab.IsLoading = true; tab.LoadProgress = 10; if (ActiveTab == tab) statusLabel.Text = "Loading..."; tabStrip.Invalidate(); };
                    core.NavigationCompleted += (_, e) =>
                    {
                        tab.IsLoading = false;
                        tab.LoadProgress = 100;
                        UpdateTabState(tab);
                        tabStrip.Invalidate();
                        if (!e.IsSuccess)
                        {
                            if (ActiveTab == tab && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
                                statusLabel.Text = "Page failed to load (" + e.WebErrorStatus + ")";
                            return;
                        }
                        TryAutoFillCredentials(tab);
                        TryAutoFillPaymentAndAddress(tab);
                        InjectAdElementHider(tab);
                    };
                    core.DocumentTitleChanged += (_, _) => { tab.Title = core.DocumentTitle ?? "New Tab"; if (ActiveTab == tab) Text = tab.Title + " - Ceprkac"; tabStrip.Invalidate(); };
                    core.SourceChanged += (_, _) =>
                    {
                        tab.Url = core.Source ?? "";
                        // Clicking a link (or any in-page navigation) must reflect the new URL.
                        // Only skip when the user is actively editing the address box.
                        if (ActiveTab == tab && !addressBox.Focused)
                            SetAddressText(tab.Url);
                        // SPA / client-side route changes (e.g. Google's identifier -> password
                        // step) often do NOT raise NavigationCompleted. Retry autofill here too —
                        // BUT only once per distinct URL. Autofill dispatches input/change events
                        // when it fills a field; on some SPAs that pushes a new history entry,
                        // which re-raises SourceChanged. Without this guard that formed a
                        // self-feeding loop that fired autofill (and SetAddressText) continuously,
                        // flickering the address bar many times a second. Re-triggering only when
                        // the URL actually changed since the last SourceChanged-driven attempt
                        // keeps the identifier -> password step working without the loop.
                        if (!string.Equals(tab.LastSourceAutoFillUrl, tab.Url, StringComparison.OrdinalIgnoreCase))
                        {
                            tab.LastSourceAutoFillUrl = tab.Url;
                            TryAutoFillCredentials(tab);
                        }
                    };
                    core.NewWindowRequested += (_, args) =>
                    {
                        var uri = (args.Uri ?? "").ToLower();
                        if (IsAdUrl(uri))
                        {
                            args.Handled = true;
                            adsBlockedCount++;
                            return;
                        }
                        // Open window.open in a real tab and keep window.opener (GBrowser behaviour).
                        args.Handled = true;
                        var deferral = args.GetDeferral();
                        int idx = tabStrip.Tabs.IndexOf(tab);
                        BeginInvoke(async () =>
                        {
                            try
                            {
                                var child = await CreateTabForNewWindow(idx >= 0 ? idx : (int?)null);
                                if (child?.CoreWebView2 != null)
                                    args.NewWindow = child.CoreWebView2;
                            }
                            finally { deferral.Complete(); }
                        });
                    };
                    core.DownloadStarting += Core_DownloadStarting;
                    core.ContextMenuRequested += Core_ContextMenuRequested;
                    core.PermissionRequested += Core_PermissionRequested;
                    core.ProcessFailed += (_, e) =>
                    {
                        try
                        {
                            BeginInvoke(new Action(() =>
                            {
                                if ((DateTime.UtcNow - lastProcessRecover).TotalSeconds < 3) return;
                                lastProcessRecover = DateTime.UtcNow;
                                statusLabel.Text = "Page process recovered — reloading…";
                                try { if (tab.WebView.CoreWebView2 != null) tab.WebView.Reload(); } catch { }
                            }));
                        }
                        catch { }
                    };
                    _ = core.AddScriptToExecuteOnDocumentCreatedAsync(DisablePasskeyJs);

                    // Block navigations to ad domains — cancel and auto-close empty tabs
                    core.NavigationStarting += (_, navArgs) =>
                    {
                        var navUri = (navArgs.Uri ?? "").ToLower();
                        if (IsAdUrl(navUri))
                        {
                            navArgs.Cancel = true;
                            adsBlockedCount++;
                            // If this tab has no real content (was just opened for the ad), close it
                            var tabUrl = (tab.Url ?? "").ToLower();
                            bool isEmptyTab = string.IsNullOrEmpty(tabUrl) || tabUrl == "about:blank" ||
                                tabUrl.StartsWith("data:") || IsAdUrl(tabUrl);
                            if (isEmptyTab && tabStrip.Tabs.Count > 1)
                            {
                                _ = Task.Delay(100).ContinueWith(_ =>
                                {
                                    try { Invoke(() => { int ti = tabStrip.Tabs.IndexOf(tab); if (ti >= 0) CloseTab(ti); }); } catch { }
                                });
                            }
                            else
                            {
                                // Tab has real content — just go back
                                if (core.CanGoBack) core.GoBack();
                            }
                        }
                    };

                    // Handle window.close() from auth flows — close the tab
                    core.WindowCloseRequested += (_, _) =>
                    {
                        int tabIdx = tabStrip.Tabs.IndexOf(tab);
                        if (tabIdx >= 0) CloseTab(tabIdx);
                    };

                    // Auto-close tabs that show "close this window" auth completion messages
                    core.NavigationCompleted += (s2, e2) =>
                    {
                        var src = core.Source ?? "";
                        if (src.Contains("/callback") && (src.Contains("oauth") || src.Contains("auth")))
                        {
                            // Auth callback page — auto-close after a short delay
                            _ = Task.Delay(1500).ContinueWith(_ =>
                            {
                                try { Invoke(() => { int ti = tabStrip.Tabs.IndexOf(tab); if (ti >= 0) CloseTab(ti); }); } catch { }
                            });
                        }
                    };

                    // Ad blocker — network-level request blocking. Awaited so the YouTube
                    // main-world JSON stripper is registered before this tab navigates.
                    await SetupAdBlocker(core);
                }
                if (!string.IsNullOrWhiteSpace(tab.Url)) NavigateTab(tab, tab.Url);
                if (tab.FocusOmnibox) FocusAddressBar(selectAll: true);
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Tab creation failed.";
                MessageBox.Show(this, $"Failed to create tab:\r\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SwitchToTab(int index)
        {
            if (index < 0 || index >= tabStrip.Tabs.Count) return;
            if (tabStrip.SelectedIndex >= 0 && tabStrip.SelectedIndex < tabStrip.Tabs.Count && tabStrip.SelectedIndex != index)
            {
                var prev = tabStrip.Tabs[tabStrip.SelectedIndex];
                prev.FocusOmnibox = false;
                prev.WebView.Visible = false;
            }
            tabStrip.SelectedIndex = index;
            var tab = tabStrip.Tabs[index];
            tab.WebView.Visible = true;
            tab.WebView.BringToFront();
            try { tab.WebView.ZoomFactor = tab.ZoomFactor; } catch { }
            if (!addressBox.Focused)
                SetAddressText(tab.Url);
            Text = tab.Title + " - Ceprkac";
            UpdateTabState(tab);
            tabStrip.Invalidate();
            if (tab.FocusOmnibox) FocusAddressBar(selectAll: true);
            else tab.WebView.Focus();
        }

        private async void OpenOAuthPopup(string url, BrowserTab parentTab)
        {
            if (sharedEnvironment == null) return;

            var popup = new Form
            {
                Text = "Sign In",
                ClientSize = new Size(500, 650),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Theme.TitleBar,
                MinimizeBox = false,
                MaximizeBox = false,
            };

            // Dark title bar
            try
            {
                int v = 1;
                DwmSetWindowAttribute(popup.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, sizeof(int));
            }
            catch { }

            var popupWebView = new WebView2 { Dock = DockStyle.Fill };
            popup.Controls.Add(popupWebView);

            try
            {
                // Use a separate environment for OAuth popups — no ad blocking scripts
                var popupUserData = Path.Combine(appDataFolder, "WebView2OAuthData");
                Directory.CreateDirectory(popupUserData);
                var popupEnv = await CoreWebView2Environment.CreateAsync(null, popupUserData);
                await popupWebView.EnsureCoreWebView2Async(popupEnv);
                var popupCore = popupWebView.CoreWebView2;
                if (popupCore == null) { popup.Dispose(); return; }

                // No ad blocker on OAuth popups — auth providers get blocked otherwise

                // Auto-close when the OAuth flow completes (redirects back to the original site)
                string? parentDomain = null;
                try { parentDomain = new Uri(parentTab.Url).Host.ToLower(); } catch { }

                popupCore.NavigationStarting += (_, navArgs) =>
                {
                    try
                    {
                        var navHost = new Uri(navArgs.Uri).Host.ToLower();
                        // If navigating back to the parent site, the auth is done
                        if (parentDomain != null && navHost.Contains(parentDomain))
                        {
                            popup.BeginInvoke(() =>
                            {
                                popup.Close();
                                // Refresh the parent tab to pick up the auth
                                parentTab.WebView.CoreWebView2?.Reload();
                            });
                        }
                    }
                    catch { }
                };

                // Also auto-close if the popup tries to close itself (window.close())
                popupCore.WindowCloseRequested += (_, _) =>
                {
                    popup.BeginInvoke(() => popup.Close());
                };

                // Update popup title
                popupCore.DocumentTitleChanged += (_, _) =>
                {
                    popup.BeginInvoke(() => popup.Text = popupCore.DocumentTitle ?? "Sign In");
                };

                popupCore.Navigate(url);
                popup.ShowDialog(this);
            }
            catch { }
            finally
            {
                popupWebView.Dispose();
                popup.Dispose();
            }
        }

        private void CloseTab(int index)
        {
            if (index < 0 || index >= tabStrip.Tabs.Count) return;
            if (tabStrip.Tabs.Count == 1) { NavigateTab(tabStrip.Tabs[0], homePageUrl); return; }
            var tab = tabStrip.Tabs[index];
            if (!string.IsNullOrWhiteSpace(tab.Url) && tab.Url != "about:blank")
            {
                closedTabs.Add(tab.Url);
                if (closedTabs.Count > 20) closedTabs.RemoveRange(0, closedTabs.Count - 20);
            }
            tab.WebView.Visible = false;
            webViewPanel.Controls.Remove(tab.WebView);
            tab.WebView.Dispose();
            tabStrip.Tabs.RemoveAt(index);
            SwitchToTab(Math.Min(index, tabStrip.Tabs.Count - 1));
        }

        private async Task<WebView2?> CreateTabForNewWindow(int? insertAfter)
        {
            if (sharedEnvironment == null) return null;
            var webView = new WebView2 { Dock = DockStyle.Fill, Visible = true, TabStop = false, DefaultBackgroundColor = Theme.ActiveTab };
            var tab = new BrowserTab { Url = "", WebView = webView, IsPopup = true };
            int insertIndex = insertAfter.HasValue ? insertAfter.Value + 1 : tabStrip.Tabs.Count;
            tabStrip.Tabs.Insert(insertIndex, tab);
            webViewPanel.Controls.Add(webView);
            webView.BringToFront();
            _ = webView.Handle;
            await webView.EnsureCoreWebView2Async(sharedEnvironment);
            var core = webView.CoreWebView2;
            if (core != null)
            {
                core.NavigationStarting += (_, _) => { tab.IsLoading = true; tabStrip.Invalidate(); };
                core.NavigationCompleted += (_, _) => { tab.IsLoading = false; UpdateTabState(tab); tabStrip.Invalidate(); };
                core.DocumentTitleChanged += (_, _) => { tab.Title = core.DocumentTitle ?? "New Tab"; tabStrip.Invalidate(); };
                core.SourceChanged += (_, _) => { tab.Url = core.Source ?? ""; if (ActiveTab == tab && !addressBox.Focused) SetAddressText(tab.Url); };
                core.WindowCloseRequested += (_, _) => { int ti = tabStrip.Tabs.IndexOf(tab); if (ti >= 0) CloseTab(ti); };
                core.DownloadStarting += Core_DownloadStarting;
                core.ContextMenuRequested += Core_ContextMenuRequested;
                core.PermissionRequested += Core_PermissionRequested;
                // Awaited so the YouTube main-world JSON stripper is registered BEFORE the
                // opener starts navigating this new window. When a YouTube video is opened via
                // window.open / target=_blank (e.g. from a search-engine result), the parent
                // begins navigation as soon as args.NewWindow is assigned — which is right after
                // this method returns. Registering the CDP script first is what removes the
                // "ads until refresh" symptom on that path.
                await SetupAdBlocker(core);
                _ = core.AddScriptToExecuteOnDocumentCreatedAsync(DisablePasskeyJs);
            }
            SwitchToTab(insertIndex);
            return webView;
        }

        private void RestoreClosedTab()
        {
            if (closedTabs.Count == 0) return;
            var url = closedTabs[closedTabs.Count - 1];
            closedTabs.RemoveAt(closedTabs.Count - 1);
            AddNewTab(url);
        }

        private void ZoomBy(double delta)
        {
            var tab = ActiveTab; if (tab == null) return;
            tab.ZoomFactor = Math.Max(0.3, Math.Min(3.0, tab.ZoomFactor + delta));
            try { tab.WebView.ZoomFactor = tab.ZoomFactor; } catch { }
            statusLabel.Text = $"Zoom: {(int)(tab.ZoomFactor * 100)}%";
        }

        private void ZoomReset()
        {
            var tab = ActiveTab; if (tab == null) return;
            tab.ZoomFactor = 1.0;
            try { tab.WebView.ZoomFactor = 1.0; } catch { }
            statusLabel.Text = "Zoom: 100%";
        }

        private void ToggleFindBar()
        {
            findBar.Visible = !findBar.Visible;
            if (findBar.Visible) { findInput.Focus(); findInput.SelectAll(); }
        }

        private void FindInput_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { FindInPage(e.Shift); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { findBar.Visible = false; e.Handled = true; }
        }

        private async void FindInPage(bool backward)
        {
            var core = ActiveTab?.WebView.CoreWebView2;
            if (core == null) return;
            string q = findInput.Text.Replace("\\", "\\\\").Replace("'", "\\'");
            string js = $"window.find('{q}', false, {(backward ? "true" : "false")}, true, false, false, false);";
            try { await core.ExecuteScriptAsync(js); } catch { }
        }

        private void NavigateTab(BrowserTab tab, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            // If it's not a URL, treat as search query
            if ((!url.Contains("://") && !url.Contains(".")) || (url.Contains(" ") && !url.Contains("://")))
            {
                url = string.Format(searchUrlTemplate, Uri.EscapeDataString(url));
            }
            else if (!url.Contains("://"))
            {
                url = "https://" + url;
            }
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
            tab.Url = uri.ToString();
            if (tab.WebView.CoreWebView2 != null)
                tab.WebView.CoreWebView2.Navigate(uri.ToString());
            if (ActiveTab == tab && !addressBox.Focused)
                SetAddressText(uri.ToString());
            AddToHistory(uri.ToString());
        }

        private void NavigateCurrentTab(string url) { if (ActiveTab != null) NavigateTab(ActiveTab, url); }

        private void UpdateTabState(BrowserTab tab)
        {
            if (ActiveTab != tab) return;
            var core = tab.WebView.CoreWebView2;
            backBtn.Enabled = core?.CanGoBack ?? false;
            backBtn.ForeColor = backBtn.Enabled ? Color.White : Theme.ForeDim;
            fwdBtn.Enabled = core?.CanGoForward ?? false;
            fwdBtn.ForeColor = fwdBtn.Enabled ? Color.White : Theme.ForeDim;
            if (!addressBox.Focused)
                SetAddressText(tab.WebView.Source?.AbsoluteUri ?? tab.Url);
            statusLabel.Text = $"Ready | Ads blocked: {adsBlockedCount} | Domains: {BlockedAdDomains.Count}";
            var currentUrl = tab.WebView.Source?.AbsoluteUri ?? "";
            bookmarkBtn.Text = BookmarkExistsInTree(bookmarks, currentUrl) ? "★" : "☆";
        }

        private void Core_DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
        {
            // Hide WebView2's default save UI (often suggests a dummy "aaaa" name for
            // Save image as) and defer until our dialog has actually set the path.
            e.Handled = true;
            var deferral = e.GetDeferral();
            var op = e.DownloadOperation;
            void Prompt()
            {
                try
                {
                    if (IsDisposed) { e.Cancel = true; return; }
                    var filename = SuggestDownloadFileName(e.ResultFilePath, op.Uri);
                    using var dialog = new SaveFileDialog
                    {
                        FileName = filename,
                        Filter = "All Files|*.*",
                        Title = "Save Download",
                        RestoreDirectory = true,
                        OverwritePrompt = true,
                    };
                    try
                    {
                        var dir = Path.GetDirectoryName(e.ResultFilePath);
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                            dialog.InitialDirectory = dir;
                    }
                    catch { }
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        e.Cancel = true;
                        statusLabel.Text = "Download canceled.";
                        return;
                    }
                    e.ResultFilePath = dialog.FileName;
                    WatchWebViewDownload(op, dialog.FileName);
                }
                catch (Exception ex)
                {
                    e.Cancel = true;
                    statusLabel.Text = "Download failed.";
                    try { MessageBox.Show(this, ex.Message, "Ceprkac", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
                }
                finally
                {
                    deferral.Complete();
                }
            }
            try
            {
                if (!IsHandleCreated || IsDisposed) { e.Cancel = true; deferral.Complete(); return; }
                BeginInvoke(new Action(Prompt));
            }
            catch
            {
                e.Cancel = true;
                deferral.Complete();
            }
        }

        // "Save image as" (and save link/media as) shows Chromium's file picker
        // *before* DownloadStarting. That first pick is discarded, then we would
        // prompt again with WebView2's dummy "aaaa" name. Replace those items so
        // only our dialog runs and the file is actually written.
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
        // search by URL), or a video/audio element (video search by URL).
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

            // Selected text → text search (works for any target kind that carries a selection).
            var sel = (selectionText ?? "").Trim();
            if (sel.Length > 0)
            {
                var shown = sel.Length > 40 ? sel.Substring(0, 40) + "…" : sel;
                AddItem($"Search {engine} for \"{shown}\"", BuildTextSearchUrl(sel));
            }

            // Prefer the media/source URI; fall back to a link that points at a media file.
            // Some in-page image/video viewers report Kind = Page/Other, so do not rely on
            // Kind alone — infer from the URI as well.
            var mediaUri = !string.IsNullOrWhiteSpace(sourceUri) ? sourceUri : linkUri;

            bool isImage = kind == CoreWebView2ContextMenuTargetKind.Image
                           || LooksLikeImageUrl(sourceUri) || LooksLikeImageUrl(linkUri);
            bool isVideo = kind == CoreWebView2ContextMenuTargetKind.Video
                           || kind == CoreWebView2ContextMenuTargetKind.Audio
                           || LooksLikeVideoUrl(sourceUri) || LooksLikeVideoUrl(linkUri);

            if (isImage && !string.IsNullOrWhiteSpace(mediaUri))
                AddItem($"Search {engine} for this image", BuildImageSearchUrl(mediaUri));
            else if (isVideo && !string.IsNullOrWhiteSpace(mediaUri))
                AddItem($"Search {engine} for this video", BuildVideoSearchUrl(mediaUri));

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
            { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg", ".avif", ".ico", ".tiff" };
        private static readonly string[] VideoExtensions =
            { ".mp4", ".webm", ".mkv", ".mov", ".avi", ".m4v", ".ogv", ".mpeg", ".mpg", ".m3u8" };

        private static bool LooksLikeImageUrl(string? url) => UrlHasExtension(url, ImageExtensions);
        private static bool LooksLikeVideoUrl(string? url) => UrlHasExtension(url, VideoExtensions);

        private static bool UrlHasExtension(string? url, string[] exts)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            string path;
            try { path = new Uri(url!).AbsolutePath.ToLowerInvariant(); }
            catch { path = url!.ToLowerInvariant(); }
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

        // Image search: Google/Bing support dedicated image verticals; others fall back
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
            return BuildTextSearchUrl(imageUrl);
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

        private async Task SaveUrlWithDialogAsync(CoreWebView2 core, string uri)
        {
            if (string.IsNullOrWhiteSpace(uri) || IsDisposed) return;
            var filename = SuggestDownloadFileName("", uri);
            if (string.IsNullOrEmpty(Path.GetExtension(filename)))
            {
                var ext = GuessExtensionFromUri(uri);
                filename += ext.Length > 0 ? ext : ".jpg";
            }
            using var dialog = new SaveFileDialog
            {
                FileName = filename,
                Filter = "All Files|*.*",
                Title = "Save Download",
                RestoreDirectory = true,
                OverwritePrompt = true,
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                statusLabel.Text = "Download canceled.";
                return;
            }
            var item = new DownloadItem
            {
                Filename = Path.GetFileName(dialog.FileName),
                Path = dialog.FileName,
                Url = uri,
                Status = "Downloading",
            };
            downloads.Add(item);
            if (downloads.Count > 40) downloads.RemoveRange(0, downloads.Count - 40);
            statusLabel.Text = $"Downloading {item.Filename}…";
            RefreshDownloadsButton();
            try
            {
                await DownloadUriToFileAsync(core, uri, dialog.FileName, item);
                item.Status = "Complete";
                if (item.Total <= 0) item.Total = item.Received;
                statusLabel.Text = $"Download complete: {item.Filename}";
            }
            catch (Exception ex)
            {
                item.Status = "Interrupted";
                statusLabel.Text = $"Download interrupted: {item.Filename}";
                try { MessageBox.Show(this, $"Could not save file:\r\n{ex.Message}", "Ceprkac", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
            }
            SaveDownloads();
            RefreshDownloadsButton();
        }

        private async Task DownloadUriToFileAsync(CoreWebView2 core, string uri, string dest, DownloadItem item)
        {
            if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                WriteDataUriToFile(uri, dest);
                item.Received = item.Total = new FileInfo(dest).Length;
                return;
            }
            if (uri.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
            {
                await WriteBlobUriToFileAsync(core, uri, dest);
                item.Received = item.Total = new FileInfo(dest).Length;
                return;
            }
            if (uri.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(new Uri(uri).LocalPath, dest, overwrite: true);
                item.Received = item.Total = new FileInfo(dest).Length;
                return;
            }

            string? ua = null;
            try
            {
                var raw = await core.ExecuteScriptAsync("navigator.userAgent");
                ua = JsonSerializer.Deserialize<string>(raw);
            }
            catch { }

            var cookieParts = new List<string>();
            try
            {
                foreach (var c in await core.CookieManager.GetCookiesAsync(uri))
                    cookieParts.Add(c.Name + "=" + c.Value);
            }
            catch { }

            using var handler = new HttpClientHandler
            {
                UseCookies = false,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            if (!string.IsNullOrEmpty(ua))
                req.Headers.TryAddWithoutValidation("User-Agent", ua);
            try
            {
                var referer = core.Source;
                if (!string.IsNullOrEmpty(referer) && referer.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    req.Headers.TryAddWithoutValidation("Referer", referer);
            }
            catch { }
            if (cookieParts.Count > 0)
                req.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookieParts));
            req.Headers.TryAddWithoutValidation("Accept", "*/*");

            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            if (resp.Content.Headers.ContentLength is long len && len > 0)
                item.Total = len;

            var dir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using var input = await resp.Content.ReadAsStreamAsync();
            using var output = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await output.WriteAsync(buffer, 0, read);
                item.Received += read;
                var copy = item;
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (copy.Status != "Downloading") return;
                        statusLabel.Text = copy.Total > 0
                            ? $"Downloading {copy.Filename}: {copy.Received:N0} / {copy.Total:N0}"
                            : $"Downloading {copy.Filename}: {copy.Received:N0}";
                    }));
                }
                catch { }
            }
        }

        private static void WriteDataUriToFile(string uri, string dest)
        {
            int comma = uri.IndexOf(',');
            if (comma < 0) throw new InvalidOperationException("Invalid data URL.");
            var meta = uri.Substring(0, comma);
            var payload = uri.Substring(comma + 1);
            byte[] bytes = meta.IndexOf("base64", StringComparison.OrdinalIgnoreCase) >= 0
                ? Convert.FromBase64String(payload)
                : Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
            var dir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(dest, bytes);
        }

        private static async Task WriteBlobUriToFileAsync(CoreWebView2 core, string uri, string dest)
        {
            var escaped = uri.Replace("\\", "\\\\").Replace("'", "\\'");
            var script = "(async()=>{const r=await fetch('" + escaped + "');const b=new Uint8Array(await r.arrayBuffer());let s='';for(let i=0;i<b.length;i++)s+=String.fromCharCode(b[i]);return btoa(s);})()";
            var json = await core.ExecuteScriptAsync(script);
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                throw new InvalidOperationException("Could not read image data from the page.");
            var b64 = JsonSerializer.Deserialize<string>(json);
            if (string.IsNullOrEmpty(b64))
                throw new InvalidOperationException("Could not read image data from the page.");
            var dir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(dest, Convert.FromBase64String(b64));
        }

        private void WatchWebViewDownload(CoreWebView2DownloadOperation op, string path)
        {
            var item = new DownloadItem
            {
                Filename = Path.GetFileName(path),
                Path = path,
                Url = op.Uri ?? "",
                Status = "Downloading",
            };
            downloads.Add(item);
            if (downloads.Count > 40) downloads.RemoveRange(0, downloads.Count - 40);
            statusLabel.Text = $"Downloading {item.Filename}…";
            RefreshDownloadsButton();
            op.BytesReceivedChanged += (_, _) => BeginInvoke(() =>
            {
                if (item.Status != "Downloading") return;
                item.Received = op.BytesReceived;
                item.Total = (long)op.TotalBytesToReceive.GetValueOrDefault();
                statusLabel.Text = item.Total > 0
                    ? $"Downloading {item.Filename}: {item.Received:N0} / {item.Total:N0}"
                    : $"Downloading {item.Filename}: {item.Received:N0}";
            });
            op.StateChanged += (_, _) => BeginInvoke(() =>
            {
                if (op.State == CoreWebView2DownloadState.Completed)
                {
                    item.Status = "Complete";
                    if (item.Total <= 0) item.Total = item.Received;
                    statusLabel.Text = $"Download complete: {item.Filename}";
                    SaveDownloads();
                    RefreshDownloadsButton();
                }
                else if (op.State == CoreWebView2DownloadState.Interrupted)
                {
                    item.Status = "Interrupted";
                    statusLabel.Text = $"Download interrupted: {item.Filename}";
                    SaveDownloads();
                    RefreshDownloadsButton();
                }
            });
        }

        private static string SuggestDownloadFileName(string? resultFilePath, string? uri)
        {
            var name = "";
            try { name = Path.GetFileName(resultFilePath ?? ""); } catch { }
            if (!string.IsNullOrWhiteSpace(name) && !IsPlaceholderDownloadName(name))
                return SanitizeFileName(name);

            string fromUri = "";
            if (!string.IsNullOrWhiteSpace(uri))
            {
                try { fromUri = Path.GetFileName(new Uri(uri).LocalPath); } catch { }
            }
            fromUri = SanitizeFileName(fromUri);
            if (!string.IsNullOrWhiteSpace(fromUri) && fromUri.IndexOf('.') >= 0 && !IsPlaceholderDownloadName(fromUri)
                && fromUri.Length > 2
                && !fromUri.Equals("images", StringComparison.OrdinalIgnoreCase)
                && !fromUri.Equals("image", StringComparison.OrdinalIgnoreCase)
                && !fromUri.Equals("img", StringComparison.OrdinalIgnoreCase))
                return fromUri;

            var ext = GuessExtensionFromUri(uri);
            if (!string.IsNullOrWhiteSpace(fromUri) && fromUri.Length > 1 && !IsPlaceholderDownloadName(fromUri))
                return fromUri + ext;
            return (ext.Length > 0 ? "image" + ext : "download");
        }

        private static bool IsPlaceholderDownloadName(string fileName)
        {
            var stem = Path.GetFileNameWithoutExtension(fileName ?? "").Trim();
            if (stem.Length == 0) return true;
            return stem.Equals("aaaa", StringComparison.OrdinalIgnoreCase);
        }

        private static string GuessExtensionFromUri(string? uri)
        {
            if (string.IsNullOrWhiteSpace(uri)) return "";
            var lower = uri.ToLowerInvariant();
            if (lower.StartsWith("data:image/png", StringComparison.Ordinal)) return ".png";
            if (lower.StartsWith("data:image/jpeg", StringComparison.Ordinal) || lower.StartsWith("data:image/jpg", StringComparison.Ordinal)) return ".jpg";
            if (lower.StartsWith("data:image/gif", StringComparison.Ordinal)) return ".gif";
            if (lower.StartsWith("data:image/webp", StringComparison.Ordinal)) return ".webp";
            if (lower.StartsWith("data:image/svg", StringComparison.Ordinal)) return ".svg";
            if (lower.Contains(".png")) return ".png";
            if (lower.Contains(".webp")) return ".webp";
            if (lower.Contains(".gif")) return ".gif";
            if (lower.Contains(".jpg") || lower.Contains(".jpeg")) return ".jpg";
            if (lower.Contains(".svg")) return ".svg";
            if (lower.Contains(".mp4")) return ".mp4";
            if (lower.Contains(".webm")) return ".webm";
            if (lower.Contains(".pdf")) return ".pdf";
            if (lower.Contains("gstatic.com") || lower.Contains("googleusercontent.com") || lower.Contains("ggpht.com")
                || lower.Contains("/image") || lower.Contains("=image") || lower.Contains("tbn:"))
                return ".jpg";
            return "";
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            name = name.Trim().Trim('.');
            if (name.Length == 0 || name == "." || name == "..") return "";
            if (name.Length > 120) name = name.Substring(0, 120);
            return name;
        }

        private void RefreshDownloadsButton()
        {
            int active = downloads.Count(d => d.Status == "Downloading");
            downloadsBtn.Text = active > 0 ? $"\u2913 {active}" : "\u2913";
            chromeTip.SetToolTip(downloadsBtn, active > 0 ? $"Downloads — {active} in progress" : "Downloads");
        }

        private void RebuildDownloadsMenu()
        {
            downloadsMenu.Items.Clear();
            var recent = downloads.AsEnumerable().Reverse().Take(15).ToList();
            if (recent.Count == 0)
            {
                downloadsMenu.Items.Add(new ToolStripMenuItem("No downloads yet.") { Enabled = false, ForeColor = Theme.ForeDim });
                return;
            }
            foreach (var dl in recent)
            {
                string extra = dl.Status == "Downloading" && dl.Total > 0
                    ? $"{dl.Received * 100 / Math.Max(dl.Total, 1)}%"
                    : dl.Status;
                var itemDl = dl;
                var mi = new ToolStripMenuItem($"{dl.Filename}  —  {extra}")
                {
                    ForeColor = Color.White, BackColor = Theme.ActiveTab,
                };
                mi.Click += (_, _) => { try { if (File.Exists(itemDl.Path)) Process.Start(new ProcessStartInfo(itemDl.Path) { UseShellExecute = true }); } catch { } };
                downloadsMenu.Items.Add(mi);
            }
            downloadsMenu.Items.Add(new ToolStripSeparator());
            var clear = new ToolStripMenuItem("Clear") { ForeColor = Color.White, BackColor = Theme.ActiveTab };
            clear.Click += (_, _) =>
            {
                downloads.RemoveAll(d => d.Status != "Downloading");
                SaveDownloads();
                RefreshDownloadsButton();
            };
            downloadsMenu.Items.Add(clear);
        }

        private void LoadDownloads()
        {
            try
            {
                if (!File.Exists(downloadsFile)) return;
                var list = JsonSerializer.Deserialize<List<DownloadItem>>(File.ReadAllText(downloadsFile));
                if (list == null) return;
                foreach (var d in list.Skip(Math.Max(0, list.Count - 40)))
                {
                    if (d.Status == "Downloading") d.Status = "Complete";
                    downloads.Add(d);
                }
            }
            catch { }
        }

        private void SaveDownloads()
        {
            try
            {
                var doneAll = downloads.Where(d => d.Status != "Downloading").ToList();
                var done = doneAll.Skip(Math.Max(0, doneAll.Count - 40)).ToList();
                File.WriteAllText(downloadsFile, JsonSerializer.Serialize(done, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

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

        private void AddressBar_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { e.Handled = true; e.SuppressKeyPress = true; NavigateCurrentTab(addressBox.Text); }
        }

        // ── Bookmarks ──
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
                SaveBookmarks(); RefreshBookmarksBar(); bookmarkBtn.Text = "☆"; statusLabel.Text = "Bookmark removed.";
            }
            else
            {
                bookmarks.Insert(0, new BookmarkNode { Type = "link", Title = tab.Title ?? GetDisplayTitle(url), Href = url });
                SaveBookmarks(); RefreshBookmarksBar(); bookmarkBtn.Text = "★"; statusLabel.Text = "Bookmark added.";
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
        private static readonly HashSet<string> BlockedAdDomains = new(StringComparer.OrdinalIgnoreCase)
        {
            // Google Ads & Analytics
            "doubleclick.net","googleadservices.com","googlesyndication.com","adservice.google.com",
            "ads.google.com","google-analytics.com","googletagmanager.com","googletagservices.com",
            "pagead2.googlesyndication.com","pagead2.googleadservices.com",
            // Major ad networks
            "adnxs.com","taboola.com","outbrain.com","criteo.com","scorecardresearch.com","pubmatic.com",
            "rubiconproject.com","quantserve.com","quantcast.com","omniture.com","comscore.com",
            "krux.com","bluekai.com","exelate.com","adform.com","adroll.com","vungle.com","inmobi.com",
            "flurry.com","mixpanel.com","heap.io","amplitude.com","optimizely.com","bizible.com",
            "pardot.com","hubspot.com","marketo.com","eloqua.com","media.net","appnexus.com","adbrite.com",
            "admob.com","adsonar.com","zergnet.com","revcontent.com","mgid.com","adblade.com","adcolony.com",
            "chartbeat.com","newrelic.com","pingdom.net","kissmetrics.com","tradedesk.com","turn.com",
            "adscale.com","bannerflow.com","nativeads.com","contentad.com","displayads.com",
            "smartadserver.com","openx.net","casalemedia.com","indexww.com","sharethrough.com",
            "33across.com","triplelift.com","sovrn.com","lijit.com","bidswitch.net","yieldmo.com",
            "teads.tv","spotxchange.com","springserve.com","contextweb.com","liveintent.com",
            "adtech.de","adform.net","serving-sys.com","adsafeprotected.com","moatads.com",
            // Facebook / Meta
            "connect.facebook.net","pixel.facebook.com","analytics.facebook.com","ads.facebook.com","an.facebook.com",
            // Twitter / X
            "ads-twitter.com","static.ads-twitter.com","analytics.twitter.com","ads-api.twitter.com","advertising.twitter.com",
            // Reddit
            "pixel.reddit.com","rereddit.com","ads.reddit.com","events.reddit.com","events.redditmedia.com","d.reddit.com",
            // LinkedIn
            "ads.linkedin.com","analytics.pointdrive.linkedin.com",
            // TikTok
            "analytics.tiktok.com","ads.tiktok.com","ads-sg.tiktok.com","analytics-sg.tiktok.com",
            // Pinterest
            "ads.pinterest.com","log.pinterest.com","ads-dev.pinterest.com","analytics.pinterest.com",
            "trk.pinterest.com","trk2.pinterest.com","widgets.pinterest.com",
            // Amazon
            "amazon-adsystem.com","advertising-api-eu.amazon.com","amazonaax.com","amazonclix.com","assoc-amazon.com",
            // YouTube
            "youtubeads.googleapis.com","ads.youtube.com","analytics.youtube.com","video-stats.video.google.com",
            "youtube.cleverads.vn",
            // Yahoo
            "advertising.yahoo.com","ads.yahoo.com","adserver.yahoo.com","global.adserver.yahoo.com",
            "adspecs.yahoo.com","analytics.yahoo.com","analytics.query.yahoo.com","comet.yahoo.com",
            "log.fc.yahoo.com","ganon.yahoo.com","gemini.yahoo.com","beap.gemini.yahoo.com",
            "geo.yahoo.com","marketingsolutions.yahoo.com","pclick.yahoo.com",
            "ads.yap.yahoo.com","m.yap.yahoo.com","partnerads.ysm.yahoo.com",
            // Yandex
            "appmetrica.yandex.com","yandexadexchange.net","adfox.yandex.ru","adsdk.yandex.ru",
            "an.yandex.ru","awaps.yandex.ru","awsync.yandex.ru","bs.yandex.ru","bs-meta.yandex.ru",
            "clck.yandex.ru","informer.yandex.ru","kiks.yandex.ru","mc.yandex.ru","metrika.yandex.ru",
            "share.yandex.ru","offerwall.yandex.net",
            // Hotjar / Session recording
            "hotjar.com","api-hotjar.com","hotjar-analytics.com","fullstory.com","mouseflow.com",
            "luckyorange.com","luckyorange.net","freshmarketer.com",
            // Segment / Analytics
            "segment.io","segment.com","stats.wp.com",
            // Error trackers
            "notify.bugsnag.com","sessions.bugsnag.com","api.bugsnag.com","app.bugsnag.com",
            "browser.sentry-cdn.com","app.getsentry.com",
            // FastClick
            "fastclick.com","fastclick.net",
            // Samsung
            "samsungadhub.com","samsungads.com","smetrics.samsung.com","nmetrics.samsung.com",
            "analytics.samsungknox.com","bigdata.ssp.samsung.com","config.samsungads.com",
            // Apple metrics
            "metrics.apple.com","securemetrics.apple.com","supportmetrics.apple.com",
            "metrics.icloud.com","metrics.mzstatic.com","books-analytics-events.apple.com",
            "stocks-analytics-events.apple.com",
            // Xiaomi
            "api.ad.xiaomi.com","data.mistat.xiaomi.com","sdkconfig.ad.xiaomi.com",
            "globalapi.ad.xiaomi.com","tracking.miui.com","tracking.intl.miui.com",
            // Huawei
            "metrics.data.hicloud.com","logservice.hicloud.com","logbak.hicloud.com",
            // OPPO / Realme / OnePlus
            "adsfs.oppomobile.com","bdapi-in-ads.realmemobile.com",
            "analytics.oneplus.cn","click.oneplus.cn","click.oneplus.com","open.oneplus.net",
            // Missing from d3ward test
            "events.hotjar.io","extmaps-api.yandex.net","metrics2.data.hicloud.com",
            "logservice1.hicloud.com","iot-eu-logser.realme.com","click.googleanalytics.com",
            "grs.hicloud.com","udcm.yahoo.com","auction.unityads.unity3d.com",
            "config.unityads.unity3d.com","adserver.unityads.unity3d.com","webview.unityads.unity3d.com",
            "adfstat.yandex.ru","iadsdk.apple.com","appmetrica.yandex.ru",
            "business-api.tiktok.com","log.byteoversea.com","ads-api.tiktok.com",
            "iot-logser.realme.com","tracking.rus.miui.com","adtech.yahooinc.com",
            "bdapi-ads.realmemobile.com","ck.ads.oppomobile.com","data.ads.oppomobile.com",
            "adx.ads.oppomobile.com","data.mistat.india.xiaomi.com","data.mistat.rus.xiaomi.com",
            "notes-analytics-events.apple.com","weather-analytics-events.apple.com",
            "api-adservices.apple.com","samsung-com.112.2o7.net","analytics-api.samsunghealthcn.com",
            "unityads.unity3d.com","byteoversea.com","yahooinc.com",
            // S3-hosted ad/analytics buckets
            "adtago.s3.amazonaws.com","analyticsengine.s3.amazonaws.com",
            "analytics.s3.amazonaws.com","advice-ads.s3.amazonaws.com",
            // Adult site ad networks
            "trafficjunky.com","trafficjunky.net","trafficstars.com","tsyndicate.com",
            "exoclick.com","exosrv.com","exoticads.com","juicyads.com","realsrv.com",
            "adsrv.org","padsdel.com","tsyndicate.com","syndication.exoclick.com",
            "main.exoclick.com","static.exoclick.com","ads.trafficjunky.net",
            "cdn.trafficjunky.net","adsrv.eacdn.com","a.realsrv.com",
            "mc.yandex.ru","syndication.realsrv.com","s.magsrv.com","magsrv.com",
            // Additional missing
            "sdkconfig.ad.intl.xiaomi.com","iot-eu-logser.realme.com","iot-logser.realme.com",
            "bdapi-ads.realmemobile.com","analytics-api.samsunghealthcn.com",
        };

        private static readonly HashSet<string> AdBlockWhitelist = new(StringComparer.OrdinalIgnoreCase)
        {
            "discord.com", "discordapp.com", "discordapp.net", "discord.gg", "discord.media",
            "cloudflare.com", "challenges.cloudflare.com", "cdnjs.cloudflare.com",
            "youtube-nocookie.com",
            "apple.com", "icloud.com",
            "ebay.com",
            "paypal.com",
            "mediafire.com",
            // Auth/OAuth providers
            "accounts.google.com", "accounts.youtube.com", "myaccount.google.com",
            "google.com", "www.google.com", "google.hr", "google.co.uk",
            "youtube.com", "www.youtube.com",
            "login.microsoftonline.com", "login.live.com", "login.microsoft.com",
            "appleid.apple.com", "idmsa.apple.com",
            "github.com", "auth0.com", "okta.com",
            "apis.google.com", "ssl.gstatic.com",
            "pay.google.com", "payments.google.com",
            "gog.com", "auth.gog.com", "login.gog.com",
            "suno.com", "suno.ai", "clerk.suno.com",
            // AI services
            "openai.com", "chat.openai.com", "chatgpt.com",
            "claude.ai", "anthropic.com",
            "gemini.google.com", "bard.google.com",
            "perplexity.ai", "you.com",
            "midjourney.com", "stability.ai",
            "huggingface.co", "replicate.com",
            "udio.com", "poe.com", "character.ai",
            "copilot.microsoft.com",
            // Banking & financial
            "chase.com", "bankofamerica.com", "wellsfargo.com", "citibank.com",
            "usbank.com", "capitalone.com", "discover.com", "americanexpress.com",
            "hsbc.com", "barclays.com", "natwest.com", "lloydsbank.com",
            "revolut.com", "wise.com", "transferwise.com", "stripe.com",
            "squareup.com", "venmo.com", "zelle.com", "cash.app",
            "ing.com", "raiffeisen.hr", "pbz.hr", "zaba.hr", "erstebank.hr",
            "n26.com", "monzo.com", "starlingbank.com",
            // Gaming clients & stores
            "steampowered.com", "store.steampowered.com", "steamcommunity.com",
            "epicgames.com", "unrealengine.com",
            "gog.com", "gogalaxy.com",
            "ea.com", "origin.com",
            "ubisoft.com", "ubi.com",
            "blizzard.com", "battle.net", "battlenet.com.cn",
            "riotgames.com", "leagueoflegends.com",
            "xbox.com", "xboxlive.com",
            "playstation.com", "sonyentertainmentnetwork.com",
            "nintendo.com", "nintendo.net",
            "humblebundle.com", "itch.io", "indiegala.com",
            "twitch.tv",
        };

        private static string BaseDomain(string host)
        {
            var p = host.Split('.');
            if (p.Length >= 3 && (p[p.Length - 1] == "uk" || p[p.Length - 1] == "au" || p[p.Length - 1] == "jp" || p[p.Length - 1] == "br" || p[p.Length - 1] == "za" || p[p.Length - 1] == "nz" || p[p.Length - 1] == "kr" || p[p.Length - 1] == "in"))
                return string.Join(".", p[p.Length - 3], p[p.Length - 2], p[p.Length - 1]);
            return p.Length >= 2 ? string.Join(".", p[p.Length - 2], p[p.Length - 1]) : host;
        }

        private static bool SameSite(string a, string b) =>
            string.Equals(BaseDomain(a), BaseDomain(b), StringComparison.OrdinalIgnoreCase);

        private static bool IsAdBlockWhitelisted(string host)
        {
            while (host.Contains('.'))
            {
                if (AdBlockWhitelist.Contains(host)) return true;
                int dot = host.IndexOf('.');
                host = host.Substring(dot + 1);
            }
            return false;
        }

        /// <summary>
        /// Checks if a URL points to a known ad/tracking domain.
        /// Used to block navigations and new windows to ad destinations.
        /// </summary>
        private bool IsAdUrl(string url)
        {
            try
            {
                var uri = new Uri(url.Contains("://") ? url : "https://" + url);
                var host = uri.Host.ToLower();
                // Don't block whitelisted domains
                if (IsAdBlockWhitelisted(host)) return false;
                // Check against blocklist
                var checkHost = host;
                while (checkHost.Contains('.'))
                {
                    if (BlockedAdDomains.Contains(checkHost)) return true;
                    int dot = checkHost.IndexOf('.');
                    checkHost = checkHost.Substring(dot + 1);
                }
                // Check common ad URL patterns
                if (url.Contains("/pagead/") || url.Contains("/adclick") ||
                    url.Contains("/aclk?") || url.Contains("googleadservices.com") ||
                    url.Contains("doubleclick.net") || url.Contains("googlesyndication.com"))
                    return true;
            }
            catch { }
            return false;
        }

        private int adsBlockedCount = 0;

        private async Task SetupAdBlocker(CoreWebView2 core)
        {
            // Track whether the current page is whitelisted — avoids per-request URI parsing
            bool pageIsWhitelisted = false;
            core.SourceChanged += (_, _) =>
            {
                try { pageIsWhitelisted = IsAdBlockWhitelisted(new Uri(core.Source ?? "").Host.ToLower()); }
                catch { pageIsWhitelisted = false; }
            };

            // Register filters for resource types that serve ads — NOT All, which would
            // intercept upload streams and add IPC overhead on every data chunk
            var adResourceTypes = new[]
            {
                CoreWebView2WebResourceContext.Script,
                CoreWebView2WebResourceContext.Image,
                CoreWebView2WebResourceContext.Stylesheet,
                CoreWebView2WebResourceContext.XmlHttpRequest,  // covers XHR, Fetch, EventSource
                CoreWebView2WebResourceContext.Media,
                CoreWebView2WebResourceContext.Font,
            };
            foreach (var resourceType in adResourceTypes)
                core.AddWebResourceRequestedFilter("*://*", resourceType);
            core.WebResourceRequested += (_, args) =>
            {
                try
                {
                    // Fast path: skip all checks when on a whitelisted page (GitHub, Discord, etc.)
                    if (pageIsWhitelisted) return;

                    var uri = new Uri(args.Request.Uri);
                    var host = uri.Host.ToLower();
                    // Skip whitelisted request hosts
                    if (IsAdBlockWhitelisted(host)) return;
                    // Same-site (first-party) requests are never blocked
                    try
                    {
                        var pageHost = new Uri(core.Source ?? "").Host.ToLower();
                        if (SameSite(host, pageHost)) return;
                    }
                    catch { }
                    // Check if the host or any parent domain is in the block list
                    var checkHost = host;
                    while (checkHost.Contains('.'))
                    {
                        if (BlockedAdDomains.Contains(checkHost))
                        {
                            args.Response = core.Environment.CreateWebResourceResponse(null, 403, "Blocked", "");
                            adsBlockedCount++;
                            return;
                        }
                        int dot = checkHost.IndexOf('.');
                        checkHost = checkHost.Substring(dot + 1);
                    }
                }
                catch { }
            };

            // YouTube ads live in ytInitialData / player JSON and must be stripped in the
            // MAIN world before page scripts run. Isolated-world <script> tags are blocked
            // by YouTube CSP, which is why ads came back after 0.6.8.
            //
            // The main-world script is installed ONCE, unconditionally, at tab setup via
            // Page.addScriptToEvaluateOnNewDocument. It is self-guarded: YouTubeMainWorldCode
            // bails immediately on any non-YouTube host and on auth/OAuth pages, so registering
            // it globally never tags Cloudflare forums as a bot. Installing it here (instead of
            // lazily on a cancellable top-level NavigationStarting) means it runs before page
            // scripts on EVERY document — including SPA soft-navigations (clicking a related
            // video), back/forward, and renderer recovery — so ad-blocking no longer depends on
            // the direction the user arrived at the video from.
            //
            // This is AWAITED (callers await SetupAdBlocker before the tab navigates) so the
            // CDP registration is in place BEFORE the first document loads. Previously this was
            // fire-and-forget, which lost a race when a YouTube video was opened directly (e.g.
            // clicked from a search-engine result into a new tab): the first document's
            // ytInitialData/ytInitialPlayerResponse loaded with ads intact because the JSON
            // stripper had not registered yet — hence "ads until you refresh".
            await InstallYouTubeMainWorld(core);

            // Inject fetch/XHR blocker into main world via DevTools Protocol
            core.NavigationCompleted += (_, _) => InjectMainWorldBlocker(core);
        }

        // Install the main-world YouTube ad blocker once per CoreWebView2, independent of
        // navigation. Page.addScriptToEvaluateOnNewDocument runs the script in the main world
        // before any page script on every subsequent document — top-level loads, SPA
        // soft-navigations, and back/forward alike. The script self-guards on hostname, so it
        // is inert everywhere except YouTube. Falls back to AddScriptToExecuteOnDocumentCreated
        // (isolated-world wrapper) if CDP is unavailable.
        private static async Task InstallYouTubeMainWorld(CoreWebView2 core)
        {
            try
            {
                string escapedJs = YouTubeMainWorldCode.Replace("\\", "\\\\").Replace("\"", "\\\"");
                string cdpParams = "{\"source\":\"" + escapedJs + "\"}";
                await core.CallDevToolsProtocolMethodAsync("Page.addScriptToEvaluateOnNewDocument", cdpParams);
            }
            catch
            {
                try { _ = core.AddScriptToExecuteOnDocumentCreatedAsync(YouTubeMainWorldInjectorJs); } catch { }
            }
        }

        private const string AdElementHiderJs = @"(function() {
            if (window.__ceprkacAdHider) return;
            window.__ceprkacAdHider = true;
            /* XenForo / Discourse: generic ad CSS and <article> scrapers blank the whole page. */
            var root = document.documentElement;
            if (root && (root.id === 'XF' || root.getAttribute('data-app') === 'public'
                || document.querySelector('.p-pageWrapper, [data-xf-init], #d-splash, .d-header')))
                return;

            var host = (location.hostname || '').toLowerCase();

            /* CSS-based hiding — catches ads before JS runs */
            var css = document.createElement('style');
            css.textContent = [
                'ins.adsbygoogle','[id*=""google_ads""]','[class*=""ad-slot""]','[class*=""advert""]',
                '[class*=""ad-banner""]','[class*=""ad-container""]','[class*=""ad-wrapper""]',
                '[class*=""ad-placement""]',
                '[data-adunit]','[data-ad-slot]','[data-google-query-id]',
                '.sponsored-content','.ad-banner','.ad-container','.ad-wrapper',
                '.native-ad','.ad-unit','.ad-zone','.ad-area','.ad-block','.ad-box','.ad-frame',
                '.ad-header','.ad-footer','.ad-leaderboard','.ad-sidebar','.ad-skyscraper',
                '.ad-rectangle','.ad-interstitial','.ad-overlay','.ad-popup','.ad-modal',
                'div[id*=""taboola""]','div[id*=""outbrain""]','div[class*=""taboola""]',
                'div[class*=""outbrain""]','div[id*=""zergnet""]','div[id*=""revcontent""]',
                'div[id*=""mgid""]','div[class*=""mgid""]',
                'iframe[src*=""doubleclick""]','iframe[src*=""googlesyndication""]',
                'iframe[src*=""googletagmanager""]','iframe[id*=""google_ads""]','iframe[id*=""aswift""]',
                'iframe[src*=""ad""][width]','iframe[data-ad]',
                '.video-ad-overlay','.preroll-ad','.midroll-ad',
                'a[href*=""doubleclick.net""]','a[href*=""googleadservices""]',
                'div[aria-label=""Advertisement""]','div[aria-label=""advertisement""]',
                'section[aria-label=""Sponsored""]',
                /* Pornhub / adult site ads */
                '.adBanner','.ad-banner','#hd-rightColAd','#pb_ad','.advertisement',
                '.mgbox','[class*=""mgbox""]','div[id*=""snigelAdStack""]',
                '.trafficStars','[class*=""trafficStars""]','[id*=""trafficStars""]',
                '[class*=""exoclick""]','[id*=""exoclick""]',
                'iframe[src*=""trafficstars""]','iframe[src*=""exoclick""]',
                'iframe[src*=""trafficjunky""]','iframe[src*=""adsrv""]',
                'iframe[src*=""juicyads""]','iframe[src*=""exosrv""]',
                'iframe[src*=""tsyndicate""]','iframe[src*=""realsrv""]',
                'div[class*=""abovePlayer""]',
                /* DuckDuckGo sponsored results and self-promo */
                '.result--ad','.is-ad','[data-testid=""ad""]','[data-testid=""result--ad""]',
                '.badge--ad','.result__extras__url--ad',
                '.ddg-extension-hide','.js-sidebar-ads','.sidebar-modules--ads',
                '.header-aside',
                /* Google sponsored results */
                '#tads','#tadsb','#bottomads','.commercial-unit-desktop-top',
                '.commercial-unit-desktop-rhs','.cu-container',
                'div[data-text-ad]','div[data-hveid] .uEierd',
                /* Bing sponsored results */
                '.b_ad','.b_adSlug','li.b_ad','#b_results > .b_ad',
                /* Yahoo sponsored results */
                '.searchCenterTopAds','.searchCenterBottomAds','.compDlink',
                /* Reddit promoted posts (GSecurity Ad Shield) */
                'shreddit-ad-post','[data-testid=""ad-post""]','[data-testid=""promoted-post""]',
                'div[data-promoted=""true""]','.promotedlink','.sponsorshipbox','.sponsor-logo',
                'faceplate-tracker[source=""ad""]','faceplate-tracker[noun=""ad""]',
                '[data-testid=""sidebar-ad""]','[data-testid=""subreddit-sidebar-ad""]',
                '.sidebar-ad','div[class*=""promotedlink""]','.premium-banner-outer',
                '[data-testid=""premium-upsell""]',
                'shreddit-experience-tree[bundlename*=""ad""]','shreddit-experience-tree[bundlename*=""Ad""]',
                '.thing.promoted','.thing.stickied.promotedlink',
                /* LinkedIn ads */
                '[data-ad-banner-id]','[data-is-sponsored=""true""]',
                '.ad-banner-container','.ads-container',
                /* Twitch ads */
                '[data-a-target=""video-ad-label""]','.video-ad','.advertisement-banner',
                '[data-test-selector=""ad-banner-default-id""]','.stream-display-ad',
                /* TikTok ads */
                '[class*=""DivAdBanner""]','[data-e2e=""ad""]'
            ].join(',') + '{display:none!important;height:0!important;min-height:0!important;overflow:hidden!important}';
            (document.head || document.documentElement).appendChild(css);

            /* DOM removal selectors */
            var sels = [
                'ins.adsbygoogle','iframe[src*=""doubleclick""]','iframe[src*=""googlesyndication""]',
                'iframe[src*=""googletagmanager""]','iframe[id*=""google_ads""]','iframe[id*=""aswift""]',
                'iframe[src*=""ad""][width]','iframe[data-ad]',
                '[id*=""google_ads""]','[class*=""ad-slot""]','[class*=""advert""]','[class*=""ad-banner""]',
                '[class*=""ad-container""]','[class*=""ad-wrapper""]',
                '[class*=""ad-placement""]',
                '[data-adunit]','[data-ad-slot]','[data-google-query-id]',
                '.sponsored-content','.ad-banner','.ad-container','.ad-wrapper',
                '.native-ad','.ad-unit','.ad-zone','.ad-area','.ad-block','.ad-box','.ad-frame',
                '.ad-header','.ad-footer','.ad-leaderboard','.ad-sidebar','.ad-skyscraper',
                '.ad-rectangle','.ad-interstitial','.ad-overlay','.ad-popup','.ad-modal',
                'div[id*=""taboola""]','div[id*=""outbrain""]','div[class*=""taboola""]',
                'div[class*=""outbrain""]','div[id*=""zergnet""]','div[id*=""revcontent""]',
                'div[id*=""mgid""]','div[class*=""mgid""]',
                '.video-ad-overlay','.preroll-ad','.midroll-ad',
                'div[aria-label=""Advertisement""]','div[aria-label=""advertisement""]',
                /* Search engine sponsored results */
                '.result--ad','.is-ad','[data-testid=""ad""]','[data-testid=""result--ad""]',
                '.badge--ad','.ddg-extension-hide','.js-sidebar-ads','.header-aside',
                '#tads','#tadsb','#bottomads','.commercial-unit-desktop-top',
                '.commercial-unit-desktop-rhs','div[data-text-ad]',
                '.b_ad','.b_adSlug','li.b_ad',
                '.searchCenterTopAds','.searchCenterBottomAds',
                /* Reddit (GSecurity Ad Shield) */
                'shreddit-ad-post','[data-testid=""ad-post""]','[data-testid=""promoted-post""]',
                'div[data-promoted=""true""]','.promotedlink','.sponsorshipbox','.sponsor-logo',
                '#ad-frame','#ad_main',
                'faceplate-tracker[source=""ad""]','faceplate-tracker[noun=""ad""]',
                '[data-testid=""sidebar-ad""]','[data-testid=""subreddit-sidebar-ad""]',
                'shreddit-experience-tree[bundlename*=""ad""]','shreddit-experience-tree[bundlename*=""Ad""]',
                '.premium-banner-outer','[data-testid=""premium-upsell""]',
                /* LinkedIn */
                '[data-ad-banner-id]','[data-is-sponsored=""true""]',
                '.ad-banner-container','.ads-container',
                /* Twitch */
                '[data-a-target=""video-ad-label""]','.video-ad','.advertisement-banner',
                '[data-test-selector=""ad-banner-default-id""]','.stream-display-ad',
                /* TikTok */
                '[class*=""DivAdBanner""]','[data-e2e=""ad""]'
            ];
            function scrub() {
                for (var i = 0; i < sels.length; i++) {
                    try {
                        var els = document.querySelectorAll(sels[i]);
                        for (var j = 0; j < els.length; j++) {
                            if (els[j] && els[j].parentElement) els[j].remove();
                        }
                    } catch(e) {}
                }
                /* Reddit / Facebook / X / Instagram only — XenForo posts are <article> */
                if (/(^|\.)reddit\.com$|(^|\.)redditmedia\.com$/.test(host)) {
                    try {
                        document.querySelectorAll('article, [data-testid=""post-container""], .thing').forEach(function(post) {
                            var badges = post.querySelectorAll('span, faceplate-tracker, [slot=""credit-bar""], .tagline');
                            for (var k = 0; k < badges.length; k++) {
                                var text = (badges[k].textContent || '').trim().toLowerCase();
                                if (text === 'promoted' || text === 'sponsored') { post.remove(); break; }
                            }
                        });
                        document.querySelectorAll('shreddit-post').forEach(function(post) {
                            if (post.hasAttribute('is-promoted') || post.getAttribute('post-type') === 'promoted') post.remove();
                        });
                    } catch(e) {}
                }
                if (/(^|\.)facebook\.com$|(^|\.)fb\.com$/.test(host)) {
                    try {
                        document.querySelectorAll('div[role=""article""], div[role=""feed""] > div').forEach(function(article) {
                            var spans = article.querySelectorAll('span');
                            for (var k = 0; k < spans.length; k++) {
                                if ((spans[k].textContent || '').trim().toLowerCase() === 'sponsored') {
                                    article.style.display = 'none'; break;
                                }
                            }
                        });
                    } catch(e) {}
                }
                if (/(^|\.)twitter\.com$|(^|\.)x\.com$/.test(host)) {
                    try {
                        document.querySelectorAll('article, [data-testid=""placementTracking""]').forEach(function(el) {
                            var text = (el.textContent || '').toLowerCase();
                            if (/\bpromoted\b/.test(text) || /\bad\s*·/.test(text) || el.matches('[data-testid=""placementTracking""]')) {
                                el.style.display = 'none';
                            }
                        });
                    } catch(e) {}
                }
                if (/(^|\.)instagram\.com$/.test(host)) {
                    try {
                        document.querySelectorAll('article').forEach(function(a) {
                            if (/\bsponsored\b/i.test(a.textContent || '')) a.style.display = 'none';
                        });
                        document.querySelectorAll('[data-testid=""reel-ad""]').forEach(function(el) { el.remove(); });
                    } catch(e) {}
                }
            }
            scrub();
            setInterval(scrub, 1500);
            new MutationObserver(scrub).observe(document.documentElement, {childList:true, subtree:true});
        })()";

        private const string YouTubeAdBlockerJs = @"(function() {
            if (window.__ceprkacYtAdBlock) return;
            window.__ceprkacYtAdBlock = true;
            var s = document.createElement('style');
            s.textContent = 'ytd-display-ad-renderer,ytd-ad-slot-renderer,ytd-promoted-video-renderer,ytd-promoted-sparkles-web-renderer,ytd-promoted-sparkles-text-search-renderer,ytd-banner-promo-renderer,ytd-statement-banner-renderer,ytd-in-feed-ad-layout-renderer,ytd-masthead-ad-renderer,ytd-primetime-promo-renderer,ytd-compact-promoted-video-renderer,ytd-action-companion-ad-renderer,ytd-mealbar-promo-renderer,ytd-enforcement-message-view-model,ytd-engagement-panel-section-list-renderer[target-id=engagement-panel-ads],#masthead-ad,#player-ads,.video-ads,.ytp-ad-module,.ytp-ad-overlay-container,.ytp-ad-player-overlay,.ytp-ad-action-interstitial,.ytp-ad-image-overlay,.ytp-ad-text-overlay,.ytp-ad-skip-ad-slot,.ad-showing .ytp-ad-module,ytd-search-pyv-renderer,ytd-movie-offer-module-renderer,tp-yt-paper-dialog:has(#dismiss-button),ytd-popup-container:has(a[href*=""/premium""]),ytd-rich-item-renderer:has(ytd-ad-slot-renderer),ytd-rich-item-renderer:has(ytd-display-ad-renderer),ytd-rich-item-renderer:has(ytd-promoted-video-renderer),ytd-rich-item-renderer:has(ytd-promoted-sparkles-web-renderer),ytd-rich-section-renderer:has(ytd-ad-slot-renderer){display:none!important}';
            (document.head||document.documentElement).appendChild(s);
            var adKeys=['adPlacements','adSlots','playerAds','adBreakHeartbeatParams','ad3Module','adSafetyReason','adLoggingData','showAdSlots','adBreakParams','adBreakStatus','adVideoId','adLayoutLoggingData','instreamAdPlayerOverlayRenderer','adPlacementConfig','adVideoStitcherConfig','promotedSparklesWebRenderer','promotedSparklesTextSearchRenderer','promotedVideoRenderer','sponsoredCardRenderer','adSlotRenderer','displayAdRenderer','inFeedAdLayoutRenderer','mastheadAdRenderer','compactPromotedVideoRenderer','actionCompanionAdRenderer','bannerPromoRenderer','statementBannerRenderer','primeTimePromoRenderer','searchPyvRenderer','movieOfferModuleRenderer','adPlacementRenderer','sparklesAdRenderer'];
            function stripAds(o,d){if(!o||typeof o!=='object'||d>12)return;for(var i=0;i<adKeys.length;i++)if(o.hasOwnProperty(adKeys[i]))delete o[adKeys[i]];var k=Object.keys(o);for(var j=0;j<k.length;j++){var key=k[j],val=o[key];if(Array.isArray(val)){for(var m=val.length-1;m>=0;m--){var item=val[m];if(item&&typeof item==='object'){var ik=Object.keys(item);for(var n=0;n<ik.length;n++){if(/^(ad|promoted|sponsor)/i.test(ik[n])){val.splice(m,1);break;}}}}}else if(val&&typeof val==='object')stripAds(val,d+1);}}
            var op=JSON.parse;JSON.parse=function(){var r=op.apply(this,arguments);try{if(r&&typeof r==='object')stripAds(r,0);}catch(e){}return r;};
            ['ytInitialPlayerResponse','ytInitialData','ytcfg'].forEach(function(p){var v=window[p];try{Object.defineProperty(window,p,{configurable:true,get:function(){return v;},set:function(n){if(n&&typeof n==='object')stripAds(n,0);v=n;}});if(v)window[p]=v;}catch(e){}});
            var adS=['.video-ads','.ytp-ad-module','.ytp-ad-overlay-container','.ytp-ad-player-overlay','.ytp-ad-action-interstitial','.ytp-ad-image-overlay','.ytp-ad-text-overlay','#player-ads','#masthead-ad','ytd-display-ad-renderer','ytd-ad-slot-renderer','ytd-promoted-video-renderer','ytd-promoted-sparkles-web-renderer','ytd-banner-promo-renderer','ytd-in-feed-ad-layout-renderer','ytd-mealbar-promo-renderer','ytd-enforcement-message-view-model','ytd-search-pyv-renderer','ytd-movie-offer-module-renderer','ytd-compact-promoted-video-renderer','ytd-action-companion-ad-renderer','ytd-primetime-promo-renderer','ytd-masthead-ad-renderer'];
            var skS=['.ytp-ad-skip-button','.ytp-skip-ad-button','.ytp-ad-skip-button-modern','.ytp-skip-ad-button__text','button[class*=""skip""]','.ytp-ad-overlay-close-button','.ytp-ad-skip-button-slot'];
            /* Localized sponsored/ad badge words — covers major YouTube UI languages */
            var sponsorWords=['sponsored','sponzorirano','gesponsert','sponsorisé','patrocinado','sponsorizzato','gesponsord','спонсируемая','スポンサー','赞助','광고','reklam','promowane','sponzorované','szponzorált','annonce','reklama','hirdetés','реклама','commandité','gesponsord','publicidad','pubblicità','anúncio','reklame','sponzorováno','sponzorované','sponzorirane','спонзорирано'];
            function isSponsoredText(t){t=t.trim().toLowerCase();for(var i=0;i<sponsorWords.length;i++){if(t===sponsorWords[i])return true;}return false;}
            function scrub(){for(var i=0;i<adS.length;i++)document.querySelectorAll(adS[i]).forEach(function(e){var p=e.closest('ytd-rich-item-renderer,ytd-rich-section-renderer,ytd-reel-shelf-renderer');if(p)p.remove();else e.remove();});for(var j=0;j<skS.length;j++)document.querySelectorAll(skS[j]).forEach(function(b){if(b.click)b.click();});/* Walk homepage rich grid items and remove sponsored cards by badge text */try{document.querySelectorAll('ytd-rich-item-renderer,ytd-rich-section-renderer').forEach(function(item){if(item.querySelector('ytd-ad-slot-renderer,ytd-display-ad-renderer,ytd-promoted-video-renderer,ytd-promoted-sparkles-web-renderer,ytd-in-feed-ad-layout-renderer')){item.remove();return;}var badges=item.querySelectorAll('span.ytd-badge-supported-renderer,ytd-badge-supported-renderer span,div.ytd-badge-supported-renderer,ytd-badge-supported-renderer,[class*=""badge""],.badge,.badge-style-type-ad,span[aria-label]');for(var k=0;k<badges.length;k++){if(isSponsoredText(badges[k].textContent||'')){item.remove();return;}}/* Check inline-block ad metadata text */var metas=item.querySelectorAll('#metadata-line span,#byline-container span,yt-formatted-string.ytd-channel-name');for(var m=0;m<metas.length;m++){if(isSponsoredText(metas[m].textContent||'')){item.remove();return;}}});}catch(e){}/* Walk search results for promoted items */try{document.querySelectorAll('ytd-video-renderer,ytd-compact-video-renderer').forEach(function(item){var badges=item.querySelectorAll('span.ytd-badge-supported-renderer,ytd-badge-supported-renderer span,[class*=""badge""]');for(var k=0;k<badges.length;k++){if(isSponsoredText(badges[k].textContent||'')){item.remove();return;}}});}catch(e){}var p=document.querySelector('.html5-video-player'),v=document.querySelector('video');if(p&&v&&(p.classList.contains('ad-showing')||p.classList.contains('ad-interrupting'))){if(Number.isFinite(v.duration)&&v.duration>0){v.currentTime=Math.max(0,v.duration-0.1);}v.muted=true;v.playbackRate=16;try{v.play();}catch(e){}p.classList.remove('ad-showing');p.classList.remove('ad-interrupting');p.classList.remove('ad-created');document.querySelectorAll('.ytp-ad-skip-button,.ytp-skip-ad-button,.ytp-ad-skip-button-modern').forEach(function(b){b.click();});setTimeout(function(){v.muted=false;v.playbackRate=1;},500);}document.querySelectorAll('ytd-rich-item-renderer').forEach(function(el){var hasAd=!!el.querySelector('ytd-ad-slot-renderer,ytd-display-ad-renderer,ytd-promoted-video-renderer,ytd-promoted-sparkles-web-renderer');if(hasAd){el.remove();return;}});document.querySelectorAll('tp-yt-paper-dialog').forEach(function(d){var t=(d.textContent||'').toLowerCase();if(t.includes('ad blocker')||t.includes('allow ads')){var b=d.querySelector('#dismiss-button,.dismiss-button,button');if(b&&b.click)b.click();d.remove();}});}
            scrub();setInterval(scrub,200);new MutationObserver(scrub).observe(document.documentElement,{childList:true,subtree:true});
        })()";

        // Main-world YouTube ad blocker — built at runtime to handle nested quotes cleanly
        private static readonly string YouTubeMainWorldCode = BuildYouTubeMainWorldCode();
        // Hostname-guarded <script> injector for AddScriptToExecuteOnDocumentCreatedAsync
        private static readonly string YouTubeMainWorldInjectorJs = BuildYouTubeInjector();

        private static string BuildYouTubeMainWorldCode()
        {
            return
                "(function(){" +
                // Strict YouTube-only guard — never run on auth/OAuth domains
                "var h=location.hostname.toLowerCase();" +
                "if(h!=='youtube.com'&&h!=='www.youtube.com'&&h!=='m.youtube.com'&&h!=='music.youtube.com'&&!h.endsWith('.youtube.com'))return;" +
                // Extra safety: bail on any auth/OAuth page that might be in a YouTube subdomain
                "if(/accounts\\.google|login\\.microsoft|appleid\\.apple|auth0\\.com|clerk\\.|oauth/.test(h))return;" +
                "if(window.__ceprkacYtMain)return;window.__ceprkacYtMain=true;" +
                // Extended ad keys list
                "var adKeys=['adPlacements','adSlots','playerAds','adBreakHeartbeatParams','ad3Module'," +
                "'adSafetyReason','adLoggingData','showAdSlots','adBreakParams','adBreakStatus'," +
                "'adVideoId','adLayoutLoggingData','instreamAdPlayerOverlayRenderer'," +
                "'adPlacementConfig','adVideoStitcherConfig'," +
                "'promotedSparklesWebRenderer','promotedSparklesTextSearchRenderer'," +
                "'promotedVideoRenderer','sponsoredCardRenderer','adSlotRenderer'," +
                "'displayAdRenderer','inFeedAdLayoutRenderer','mastheadAdRenderer'," +
                "'compactPromotedVideoRenderer','actionCompanionAdRenderer'," +
                "'bannerPromoRenderer','statementBannerRenderer','primeTimePromoRenderer'," +
                "'searchPyvRenderer','movieOfferModuleRenderer','adPlacementRenderer','sparklesAdRenderer'];" +
                // Recursive strip function — deletes ad keys and splices ad items from arrays
                "function strip(o,d){if(!o||typeof o!=='object'||d>15)return;" +
                "for(var i=0;i<adKeys.length;i++)if(o.hasOwnProperty(adKeys[i]))delete o[adKeys[i]];" +
                "var k=Object.keys(o);for(var j=0;j<k.length;j++){" +
                "var key=k[j],val=o[key];" +
                "if(Array.isArray(val)){for(var m=val.length-1;m>=0;m--){" +
                "var item=val[m];if(item&&typeof item==='object'){" +
                "var ik=Object.keys(item);var isAd=false;" +
                "for(var n=0;n<ik.length;n++){" +
                "if(/^(ad|promoted|sponsor)/i.test(ik[n])){isAd=true;break;}}" +
                // Also check for adSlotRenderer or promotedVideoRenderer nested inside richItemRenderer
                "if(!isAd&&item.richItemRenderer&&item.richItemRenderer.content){" +
                "var ck=Object.keys(item.richItemRenderer.content);" +
                "for(var c=0;c<ck.length;c++){if(/^(ad|promoted|sponsor)/i.test(ck[c])){isAd=true;break;}}}" +
                // Check for badge text indicating sponsored content (BADGE_STYLE_TYPE_AD or localized label)
                "if(!isAd){try{var js=JSON.stringify(item);" +
                "if(/\"style\":\"BADGE_STYLE_TYPE_AD\"/.test(js)||" +
                "/\"label\":\"(?:Sponsored|Sponzorirano|Gesponsert|Sponsorisé|Patrocinado|Sponsorizzato|Gesponsord|Реклама|Рекламa|スポンサー|赞助|광고|Reklam|Promowane|Sponzorované|Szponzorált|Annonce|Reklama|Hirdetés|Commandité|Publicidad|Pubblicità|Anúncio|Reklame|Sponzorováno|Sponzorirane|Спонзорирано)\"/.test(js))" +
                "{isAd=true;}}catch(e){}}" +
                "if(isAd){val.splice(m,1);}" +
                "else{strip(item,d+1);}" +
                "}}" +
                "}else if(val&&typeof val==='object')strip(val,d+1);}}" +
                // Intercept JSON.parse — catches ytInitialData embedded in <script> tags
                "var op=JSON.parse;JSON.parse=function(){var r=op.apply(this,arguments);" +
                "try{if(r&&typeof r==='object')strip(r,0);}catch(e){}return r;};" +
                // Intercept ytInitialPlayerResponse, ytInitialData — catches direct assignments
                "['ytInitialPlayerResponse','ytInitialData'].forEach(function(p){var v=window[p];" +
                "try{Object.defineProperty(window,p,{configurable:true," +
                "get:function(){return v;},set:function(n){if(n&&typeof n==='object')strip(n,0);v=n;}});" +
                "if(v)window[p]=v;}catch(e){}});" +
                // Intercept fetch responses for YouTube API calls (browse/search/next/player)
                "var oFetch=window.fetch;window.fetch=function(){var args=arguments;" +
                "var url=typeof args[0]==='string'?args[0]:(args[0]&&args[0].url?args[0].url:'');" +
                "if(!/youtubei\\/v1\\/(browse|search|next|player|reel)/.test(url))return oFetch.apply(this,args);" +
                "return oFetch.apply(this,args).then(function(resp){" +
                "if(!resp||!resp.ok)return resp;" +
                "return resp.clone().text().then(function(txt){" +
                "try{var data=op.call(JSON,txt);strip(data,0);" +
                "return new Response(JSON.stringify(data),{status:resp.status,statusText:resp.statusText,headers:resp.headers});" +
                "}catch(e){return resp;}});});};" +
                "})()";
        }

        // Fallback injector — wraps the main world code in a <script> tag for AddScriptToExecuteOnDocumentCreatedAsync
        private static string BuildYouTubeInjector()
        {
            string escaped = YouTubeMainWorldCode.Replace("\\", "\\\\").Replace("'", "\\'");
            return "(function(){if(location.hostname.indexOf('youtube')===-1)return;" +
                   "var sc=document.createElement('script');" +
                   "sc.textContent='" + escaped + "';" +
                   "(document.head||document.documentElement).appendChild(sc);sc.remove();})()";
        }

        private async Task LoadOrUpdateBlocklistAsync()
        {
            // Load bundled blocklist from app directory
            var bundledList = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "blocklist.txt");
            if (File.Exists(bundledList))
            {
                int count = 0;
                foreach (var line in File.ReadAllLines(bundledList))
                {
                    var domain = line.Trim();
                    if (!string.IsNullOrEmpty(domain) && !domain.StartsWith("#") && domain.Contains('.'))
                    {
                        BlockedAdDomains.Add(domain);
                        count++;
                    }
                }
                statusLabel.Text = $"Ad blocker: {BlockedAdDomains.Count} domains loaded.";
            }

            // Also try to load/update from appdata (user can drop a custom blocklist.txt there)
            var userList = Path.Combine(appDataFolder, "blocklist.txt");
            if (File.Exists(userList))
            {
                foreach (var line in File.ReadAllLines(userList))
                {
                    var domain = line.Trim();
                    if (!string.IsNullOrEmpty(domain) && !domain.StartsWith("#") && domain.Contains('.'))
                        BlockedAdDomains.Add(domain);
                }
            }
            await Task.CompletedTask;
        }

        private static bool IsChallengePage(CoreWebView2 core)
        {
            try
            {
                var src = (core.Source ?? "").ToLowerInvariant();
                if (src.Contains("cdn-cgi/") || src.Contains("__cf_chl") || src.Contains("challenges.cloudflare"))
                    return true;
                var title = (core.DocumentTitle ?? "").ToLowerInvariant();
                if (title.Contains("just a moment") || title.Contains("attention required") ||
                    title.Contains("checking your browser") || title.Contains("please wait"))
                    return true;
            }
            catch { }
            return false;
        }

        private async void InjectMainWorldBlocker(CoreWebView2 core)
        {
            if (BlockedAdDomains.Count == 0) return;
            if (IsChallengePage(core)) return;
            // Skip YouTube — it gets its own dedicated main-world injection
            try
            {
                var pageHost = new Uri(core.Source ?? "").Host.ToLower();
                if (pageHost == "www.youtube.com" || pageHost == "youtube.com" ||
                    pageHost == "m.youtube.com" || pageHost == "music.youtube.com" ||
                    pageHost.EndsWith(".youtube.com"))
                    return;
            }
            catch { }
            try
            {
                var xf = await core.ExecuteScriptAsync(
                    "(document.documentElement&&(document.documentElement.id==='XF'||document.documentElement.getAttribute('data-app')==='public'||!!document.querySelector('.p-pageWrapper,[data-xf-init]')))");
                if (xf != null && xf.IndexOf("true", StringComparison.OrdinalIgnoreCase) >= 0)
                    return;
            }
            catch { }
            try
            {
                // Build the blocker JS
                var topDomains = BlockedAdDomains
                    .Where(d => !d.Contains('*') && d.Length > 3 && d.Length < 60)
                    .Take(15000)
                    .ToList();

                var sb = new System.Text.StringBuilder();
                sb.Append("(function(){if(window.__cFB)return;window.__cFB=1;var b=new Set([");
                bool first = true;
                foreach (var d in topDomains)
                {
                    if (!first) sb.Append(',');
                    sb.Append('"');
                    sb.Append(d.Replace("\"", "").Replace("\\", ""));
                    sb.Append('"');
                    first = false;
                }
                sb.Append("]);");
                sb.Append("var wl=new Set(['google.com','youtube.com','accounts.google.com','apis.google.com','ssl.gstatic.com','gstatic.com','discord.com','discordapp.com','github.com','paypal.com','ebay.com','apple.com','icloud.com','mediafire.com','login.microsoftonline.com','login.live.com','pay.google.com','gog.com','steampowered.com','steamcommunity.com','epicgames.com','ea.com','origin.com','ubisoft.com','blizzard.com','battle.net','riotgames.com','xbox.com','playstation.com','nintendo.com','twitch.tv','chase.com','bankofamerica.com','wellsfargo.com','citibank.com','capitalone.com','revolut.com','wise.com','stripe.com','n26.com','cloudflare.com','challenges.cloudflare.com']);");
                sb.Append("function isWl(h){while(h){if(wl.has(h))return 1;var i=h.indexOf('.');if(i<0)break;h=h.substr(i+1);}return 0};");
                sb.Append("function chk(u){try{if(isWl(location.hostname))return 0;var l=u.toLowerCase();var h=new URL(l).hostname;if(isWl(h))return 0;while(h){if(b.has(h))return 1;var i=h.indexOf('.');if(i<0)break;h=h.substr(i+1);}");
                sb.Append("if(/(\\/ads?\\/|\\/ad[sx]?\\b|\\/pagead\\/|\\/ptracking|\\/advert|\\/sponsored|\\/promotion|\\/tracking\\/|\\/analytics\\/|\\/collect\\?|\\/beacon|\\/pixel|\\/imp\\?|\\/impression|\\/click\\?|ad_banner|ad_frame|sponsored_content|promo_banner|[?&](ad|ads|adunit|adformat|adtag)=)/i.test(l))return 1;");
                sb.Append("if(/(?:\\/(?:adcontent|img\\/adv|web-ad|iframead|contentad|ad\\/image|video-ad|stats\\/event|xtclicks|adscript|bannerad|googlead|adhandler|adimages|adconfig|tracking\\/track|tracker\\/track|adrequest|nativead|adman|advertisement|adframe|adcontrol|adoverlay|adserver|adsense|google-ads|ad-banner|banner-ad|adplacement|adblockdetect|advertising|admanagement|adprovider|adrotation|adunit|adcall|adlog|adcount|adserve|adsrv|adsys|adtrack|adview|adwidget|adzone|sidebar-ads|footer-ads|top-ads|bottom-ads|ads\\.php|ad\\.js|ad\\.css))/i.test(l))return 1;");
                sb.Append("if(/\\/api\\/stats\\/(ads|atr)/i.test(l))return 1;");
                sb.Append("var hh=new URL(l).hostname;");
                sb.Append("if(/^(?:.*[-_.])?(ads?|adv(ert(s|ising)?)?|banners?|track(er|ing|s)?|beacons?|doubleclick|adservice|adnxs|adtech|googleads|gads|adwords|partner|sponsor(ed)?|click(s|bank|tale|through)?|pop(up|under)s?|promo(tion)?|market(ing|er)?|affiliates?|metrics?|stat(s|counter|istics)?|analytics?|pixels?|campaign|traff(ic|iq)|monetize|syndicat(e|ion)|revenue|yield|impress(ion)?s?|conver(sion|t)?|audience|target(ing)?|behavior|profil(e|ing)|telemetry|survey|outbrain|taboola|quantcast|scorecard|omniture|comscore|krux|bluekai|exelate|adform|adroll|rubicon|vungle|inmobi|flurry|mixpanel|heap|amplitude|optimizely|bizible|pardot|hubspot|marketo|eloqua|media(math|net)|criteo|appnexus|turn|adbrite|admob|adsonar|adscale|zergnet|revcontent|mgid|nativeads|contentad|displayads|bannerflow|adblade|adcolony|chartbeat|newrelic|pingdom|kissmetrics|tradedesk|bidder|auction|rtb|programmatic|interstitial|overlay|trafficjunky|trafficstars|exoclick|juicyads|realsrv|magsrv)\\./i.test(hh))return 1;");
                sb.Append("if(/^(?:adcreative(s)?|imageserv|media(mgr)?|stats|switch|track(2|er)?|view|ads?\\d{0,3}|banners?\\d{0,3}|clicks?\\d{0,3}|count(er)?\\d{0,3}|servedby\\d{0,3}|toolbar\\d{0,3}|pageads\\d{0,3}|pops\\d{0,3}|promos?\\d{0,3})\\./i.test(hh))return 1;");
                sb.Append("if(/(?:\\/(1|blank|b|clear|pixel|transp|spacer)\\.gif|\\.swf)$/i.test(l))return 1;");
                sb.Append("return 0}catch(e){return 0}};");
                sb.Append("var F=fetch;window.fetch=function(a,o){var u=typeof a==='string'?a:a&&a.url?a.url:'';if(chk(u))return Promise.reject(new TypeError('blocked'));return F.apply(this,arguments)};");
                sb.Append("var X=XMLHttpRequest.prototype.open;XMLHttpRequest.prototype.open=function(){var u=arguments[1]||'';if(typeof u==='string'&&chk(u)){this.__blk=1;return}return X.apply(this,arguments)};");
                sb.Append("var S=XMLHttpRequest.prototype.send;XMLHttpRequest.prototype.send=function(){if(this.__blk)return;return S.apply(this,arguments)};");
                sb.Append("})()");

                // Use DevTools Protocol to inject into main world — bypasses CSP
                string escapedJs = sb.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"");
                string cdpParams = "{\"expression\":\"" + escapedJs + "\",\"allowUnsafeEvalBlockedByCSP\":true}";
                await core.CallDevToolsProtocolMethodAsync("Runtime.evaluate", cdpParams);
            }
            catch { }
        }

        private async void InjectAdElementHider(BrowserTab tab)
        {
            try
            {
                var core = tab.WebView.CoreWebView2;
                if (core == null) return;
                if (IsChallengePage(core)) return;
                var url = core.Source ?? "";
                string pageHost = "";
                try { pageHost = new Uri(url).Host.ToLower(); } catch { }

                // YouTube gets its own dedicated ad blocking — DevTools main-world injection
                // handles JSON stripping, and YouTubeAdBlockerJs handles DOM scrubbing
                bool isYouTube = pageHost == "www.youtube.com" || pageHost == "youtube.com" ||
                    pageHost == "m.youtube.com" || pageHost == "music.youtube.com" ||
                    pageHost.EndsWith(".youtube.com") || pageHost.EndsWith(".youtube-nocookie.com");

                if (isYouTube)
                {
                    await core.ExecuteScriptAsync(YouTubeAdBlockerJs);
                    return;
                }

                // Skip generic element hiding on whitelisted sites (non-YouTube)
                if (IsAdBlockWhitelisted(pageHost)) return;

                await core.ExecuteScriptAsync(AdElementHiderJs);
            }
            catch { }
        }

        // ── Password Manager ──
        private void LoadPasswords()
        {
            if (!File.Exists(passwordsFile)) return;
            try
            {
                var encrypted = File.ReadAllBytes(passwordsFile);
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(decrypted);
                savedPasswords.Clear();
                // Simple JSON array parse: [{"u":"url","n":"username","p":"password"},...]
                foreach (var entry in ParseCredentialJson(json))
                    savedPasswords.Add(entry);
            }
            catch { /* corrupted or wrong user — ignore */ }
        }

        private void SavePasswords()
        {
            try
            {
                var sb = new StringBuilder("[");
                for (int i = 0; i < savedPasswords.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var c = savedPasswords[i];
                    sb.Append($"{{\"u\":\"{EscapeJson(c.Url)}\",\"n\":\"{EscapeJson(c.Username)}\",\"p\":\"{EscapeJson(c.Password)}\"}}");
                }
                sb.Append(']');
                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(passwordsFile, encrypted);
            }
            catch { }
        }

        // ── Payment methods (cards) — DPAPI at rest, same scheme as passwords ──
        private void LoadCards()
        {
            if (!File.Exists(cardsFile)) return;
            try
            {
                var decrypted = ProtectedData.Unprotect(File.ReadAllBytes(cardsFile), null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(decrypted);
                savedCards.Clear();
                foreach (var c in ParseCardJson(json)) savedCards.Add(c);
            }
            catch { /* corrupted or wrong user — ignore */ }
        }

        private void SaveCards()
        {
            try
            {
                var sb = new StringBuilder("[");
                for (int i = 0; i < savedCards.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var c = savedCards[i];
                    sb.Append('{');
                    sb.Append($"\"label\":\"{EscapeJson(c.Label)}\",");
                    sb.Append($"\"name\":\"{EscapeJson(c.CardholderName)}\",");
                    sb.Append($"\"num\":\"{EscapeJson(c.Number)}\",");
                    sb.Append($"\"em\":\"{EscapeJson(c.ExpMonth)}\",");
                    sb.Append($"\"ey\":\"{EscapeJson(c.ExpYear)}\",");
                    sb.Append($"\"cvc\":\"{EscapeJson(c.Cvc)}\"");
                    sb.Append('}');
                }
                sb.Append(']');
                var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(sb.ToString()), null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(cardsFile, encrypted);
            }
            catch { }
        }

        // ── Addresses / contact profiles — DPAPI at rest ──
        private void LoadAddresses()
        {
            if (!File.Exists(addressesFile)) return;
            try
            {
                var decrypted = ProtectedData.Unprotect(File.ReadAllBytes(addressesFile), null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(decrypted);
                savedAddresses.Clear();
                foreach (var a in ParseAddressJson(json)) savedAddresses.Add(a);
            }
            catch { /* corrupted or wrong user — ignore */ }
        }

        private void SaveAddresses()
        {
            try
            {
                var sb = new StringBuilder("[");
                for (int i = 0; i < savedAddresses.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var a = savedAddresses[i];
                    sb.Append('{');
                    sb.Append($"\"label\":\"{EscapeJson(a.Label)}\",");
                    sb.Append($"\"name\":\"{EscapeJson(a.FullName)}\",");
                    sb.Append($"\"email\":\"{EscapeJson(a.Email)}\",");
                    sb.Append($"\"phone\":\"{EscapeJson(a.Phone)}\",");
                    sb.Append($"\"l1\":\"{EscapeJson(a.Line1)}\",");
                    sb.Append($"\"l2\":\"{EscapeJson(a.Line2)}\",");
                    sb.Append($"\"city\":\"{EscapeJson(a.City)}\",");
                    sb.Append($"\"state\":\"{EscapeJson(a.State)}\",");
                    sb.Append($"\"zip\":\"{EscapeJson(a.PostalCode)}\",");
                    sb.Append($"\"country\":\"{EscapeJson(a.Country)}\"");
                    sb.Append('}');
                }
                sb.Append(']');
                var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(sb.ToString()), null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(addressesFile, encrypted);
            }
            catch { }
        }

        private static List<SavedCard> ParseCardJson(string json)
        {
            var list = new List<SavedCard>();
            int pos = 0;
            while (pos < json.Length)
            {
                int objStart = json.IndexOf('{', pos);
                if (objStart < 0) break;
                int objEnd = json.IndexOf('}', objStart);
                if (objEnd < 0) break;
                string obj = json.Substring(objStart + 1, objEnd - objStart - 1);
                var card = new SavedCard
                {
                    Label = ExtractJsonValue(obj, "label"),
                    CardholderName = ExtractJsonValue(obj, "name"),
                    Number = ExtractJsonValue(obj, "num"),
                    ExpMonth = ExtractJsonValue(obj, "em"),
                    ExpYear = ExtractJsonValue(obj, "ey"),
                    Cvc = ExtractJsonValue(obj, "cvc"),
                };
                if (!string.IsNullOrEmpty(card.Number)) list.Add(card);
                pos = objEnd + 1;
            }
            return list;
        }

        private static List<SavedAddress> ParseAddressJson(string json)
        {
            var list = new List<SavedAddress>();
            int pos = 0;
            while (pos < json.Length)
            {
                int objStart = json.IndexOf('{', pos);
                if (objStart < 0) break;
                int objEnd = json.IndexOf('}', objStart);
                if (objEnd < 0) break;
                string obj = json.Substring(objStart + 1, objEnd - objStart - 1);
                var addr = new SavedAddress
                {
                    Label = ExtractJsonValue(obj, "label"),
                    FullName = ExtractJsonValue(obj, "name"),
                    Email = ExtractJsonValue(obj, "email"),
                    Phone = ExtractJsonValue(obj, "phone"),
                    Line1 = ExtractJsonValue(obj, "l1"),
                    Line2 = ExtractJsonValue(obj, "l2"),
                    City = ExtractJsonValue(obj, "city"),
                    State = ExtractJsonValue(obj, "state"),
                    PostalCode = ExtractJsonValue(obj, "zip"),
                    Country = ExtractJsonValue(obj, "country"),
                };
                if (!string.IsNullOrEmpty(addr.FullName) || !string.IsNullOrEmpty(addr.Line1)) list.Add(addr);
                pos = objEnd + 1;
            }
            return list;
        }

        private void ImportPasswordsCsv()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Import Passwords (Chrome/Edge CSV format)",
                Filter = "CSV Files (*.csv)|*.csv|All Files|*.*",
                RestoreDirectory = true,
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var lines = File.ReadAllLines(dlg.FileName);
                int count = 0;
                // Chrome CSV format: name,url,username,password
                // Skip header row
                for (int i = 1; i < lines.Length; i++)
                {
                    var fields = ParseCsvLine(lines[i]);
                    if (fields.Count < 4) continue;
                    string url = fields[1].Trim();
                    string username = fields[2].Trim();
                    string password = fields[3].Trim();
                    if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(username)) continue;

                    // Avoid duplicates
                    if (!savedPasswords.Any(p => string.Equals(p.Url, url, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(p.Username, username, StringComparison.OrdinalIgnoreCase)))
                    {
                        savedPasswords.Add(new SavedCredential { Url = url, Username = username, Password = password });
                        count++;
                    }
                }
                SavePasswords();
                statusLabel.Text = $"Imported {count} passwords.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Import failed:\r\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearPasswords()
        {
            if (MessageBox.Show(this, "Clear all saved passwords?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            savedPasswords.Clear();
            SavePasswords();
            statusLabel.Text = "Passwords cleared.";
        }

        // ═══════════════════════════════════════════════════════════════
        // Payment / Address managers (list + add/edit/delete dialogs)
        // ═══════════════════════════════════════════════════════════════

        private void ManageCards()
        {
            using var dlg = new ListManagerDialog<SavedCard>(
                "Payment Methods",
                savedCards,
                c => c.Display,
                () => EditCardDialog(new SavedCard()),
                existing => EditCardDialog(existing));
            dlg.Font = _bookmarkFont ?? Font;
            dlg.ShowDialog(this);
            SaveCards();
            statusLabel.Text = $"{savedCards.Count} payment method(s) saved.";
        }

        private void ManageAddresses()
        {
            using var dlg = new ListManagerDialog<SavedAddress>(
                "Addresses",
                savedAddresses,
                a => a.Display,
                () => EditAddressDialog(new SavedAddress()),
                existing => EditAddressDialog(existing));
            dlg.Font = _bookmarkFont ?? Font;
            dlg.ShowDialog(this);
            SaveAddresses();
            statusLabel.Text = $"{savedAddresses.Count} address(es) saved.";
        }

        /// <summary>Modal editor for a card. Returns the edited card or null if cancelled.</summary>
        private SavedCard? EditCardDialog(SavedCard card)
        {
            using var form = new FieldEditorForm("Payment Method");
            var label = form.AddField("Nickname (optional)", card.Label);
            var name = form.AddField("Cardholder name", card.CardholderName);
            var number = form.AddField("Card number", card.Number);
            var month = form.AddField("Expiry month (MM)", card.ExpMonth);
            var year = form.AddField("Expiry year (YYYY)", card.ExpYear);
            var cvc = form.AddField("CVC", card.Cvc, isPassword: true);
            form.Build();
            if (form.ShowDialog(this) != DialogResult.OK) return null;

            card.Label = label.Text.Trim();
            card.CardholderName = name.Text.Trim();
            card.Number = new string(number.Text.Where(char.IsDigit).ToArray());
            card.ExpMonth = month.Text.Trim();
            card.ExpYear = year.Text.Trim();
            card.Cvc = cvc.Text.Trim();
            if (card.Number.Length < 12)
            {
                MessageBox.Show(this, "Card number looks too short.", "Ceprkac", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            return card;
        }

        /// <summary>Modal editor for an address. Returns the edited address or null if cancelled.</summary>
        private SavedAddress? EditAddressDialog(SavedAddress addr)
        {
            using var form = new FieldEditorForm("Address");
            var label = form.AddField("Nickname (optional)", addr.Label);
            var name = form.AddField("Full name", addr.FullName);
            var email = form.AddField("Email", addr.Email);
            var phone = form.AddField("Phone", addr.Phone);
            var l1 = form.AddField("Address line 1", addr.Line1);
            var l2 = form.AddField("Address line 2", addr.Line2);
            var city = form.AddField("City", addr.City);
            var state = form.AddField("State / Region", addr.State);
            var zip = form.AddField("Postal code", addr.PostalCode);
            var country = form.AddField("Country", addr.Country);
            form.Build();
            if (form.ShowDialog(this) != DialogResult.OK) return null;

            addr.Label = label.Text.Trim();
            addr.FullName = name.Text.Trim();
            addr.Email = email.Text.Trim();
            addr.Phone = phone.Text.Trim();
            addr.Line1 = l1.Text.Trim();
            addr.Line2 = l2.Text.Trim();
            addr.City = city.Text.Trim();
            addr.State = state.Text.Trim();
            addr.PostalCode = zip.Text.Trim();
            addr.Country = country.Text.Trim();
            if (string.IsNullOrWhiteSpace(addr.FullName) && string.IsNullOrWhiteSpace(addr.Line1))
            {
                MessageBox.Show(this, "Enter at least a name or a street address.", "Ceprkac", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            return addr;
        }

        // ═══════════════════════════════════════════════════════════════
        // Checkout autofill — card + address
        // ═══════════════════════════════════════════════════════════════

        private async void TryAutoFillPaymentAndAddress(BrowserTab tab)
        {
            if (savedCards.Count == 0 && savedAddresses.Count == 0) return;
            // Debounce
            if ((DateTime.Now - tab.LastAutoFillFormsAttempt).TotalSeconds < 3) return;
            tab.LastAutoFillFormsAttempt = DateTime.Now;

            var core = tab.WebView.CoreWebView2;
            if (core == null) return;
            string pageUrl = core.Source ?? "";
            if (string.IsNullOrEmpty(pageUrl)) return;

            string pathLower = "";
            try { pathLower = (new Uri(pageUrl).PathAndQuery + " " + pageUrl).ToLowerInvariant(); } catch { pathLower = pageUrl.ToLowerInvariant(); }
            bool looksLikeCheckout = pathLower.Contains("checkout") || pathLower.Contains("payment") || pathLower.Contains("billing")
                || pathLower.Contains("shipping") || pathLower.Contains("address") || pathLower.Contains("cart")
                || pathLower.Contains("order") || pathLower.Contains("pay");

            // Detect the presence of card / address fields even when the URL is opaque.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                await Task.Delay(700 + attempt * 500);
                if (tab.WebView.IsDisposed || tab.WebView.CoreWebView2 == null) return;
                core = tab.WebView.CoreWebView2;

                string detectJs = @"(function(){
                    function has(sel){ try { return !!document.querySelector(sel); } catch(e){ return false; } }
                    var card = has('input[autocomplete=""cc-number""], input[name*=""card"" i][name*=""num"" i], input[id*=""card"" i][id*=""num"" i], input[autocomplete=""cc-csc""]');
                    var addr = has('input[autocomplete=""street-address""], input[autocomplete=""address-line1""], input[name*=""address"" i], input[id*=""address"" i], input[autocomplete=""postal-code""], input[name*=""zip"" i], input[name*=""postal"" i]');
                    return (card?'card':'') + '|' + (addr?'addr':'');
                })()";

                string result;
                try { result = (await core.ExecuteScriptAsync(detectJs)).Trim('"'); }
                catch { continue; }

                bool hasCardFields = result.StartsWith("card");
                bool hasAddrFields = result.EndsWith("addr");
                if (!hasCardFields && !hasAddrFields)
                {
                    if (!looksLikeCheckout) return; // nothing to fill and not a checkout — stop
                    continue;
                }

                // Fill address first (billing/shipping usually precedes card entry).
                if (hasAddrFields && savedAddresses.Count > 0)
                {
                    if (savedAddresses.Count == 1) await FillAddress(core, savedAddresses[0]);
                    else Invoke(() => ShowAddressPicker(tab));
                }
                if (hasCardFields && savedCards.Count > 0)
                {
                    if (savedCards.Count == 1) await FillCard(core, savedCards[0]);
                    else Invoke(() => ShowCardPicker(tab));
                }
                Invoke(() => statusLabel.Text = "Autofilled saved details.");
                return;
            }
        }

        private static string JsStr(string s) =>
            "'" + (s ?? "").Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "").Replace("\n", "") + "'";

        private async Task FillCard(CoreWebView2 core, SavedCard c)
        {
            string js = $@"(function(){{
                function setVal(el, val){{
                    if(!el) return;
                    var setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype,'value').set;
                    setter.call(el, val);
                    el.dispatchEvent(new Event('input',{{bubbles:true}}));
                    el.dispatchEvent(new Event('change',{{bubbles:true}}));
                }}
                function pick(){{ for(var i=0;i<arguments.length;i++){{ try{{ var e=document.querySelector(arguments[i]); if(e) return e; }}catch(x){{}} }} return null; }}
                setVal(pick('input[autocomplete=""cc-number""]','input[name*=""cardnumber"" i]','input[name*=""card"" i][name*=""num"" i]','input[id*=""card"" i][id*=""num"" i]'), {JsStr(c.Number)});
                setVal(pick('input[autocomplete=""cc-name""]','input[name*=""cardholder"" i]','input[name*=""ccname"" i]','input[id*=""cardname"" i]'), {JsStr(c.CardholderName)});
                setVal(pick('input[autocomplete=""cc-csc""]','input[name*=""cvc"" i]','input[name*=""cvv"" i]','input[id*=""cvc"" i]','input[id*=""cvv"" i]'), {JsStr(c.Cvc)});
                // Combined MM/YY field
                var exp = pick('input[autocomplete=""cc-exp""]','input[name*=""exp"" i]','input[id*=""exp"" i]');
                if(exp) setVal(exp, {JsStr(c.ExpMonth + "/" + (c.ExpYear.Length >= 2 ? c.ExpYear.Substring(c.ExpYear.Length - 2) : c.ExpYear))});
                setVal(pick('input[autocomplete=""cc-exp-month""]','select[autocomplete=""cc-exp-month""]','input[name*=""expmonth"" i]','[id*=""expmonth"" i]'), {JsStr(c.ExpMonth)});
                setVal(pick('input[autocomplete=""cc-exp-year""]','select[autocomplete=""cc-exp-year""]','input[name*=""expyear"" i]','[id*=""expyear"" i]'), {JsStr(c.ExpYear)});
            }})()";
            try { await core.ExecuteScriptAsync(js); } catch { }
        }

        private async Task FillAddress(CoreWebView2 core, SavedAddress a)
        {
            string js = $@"(function(){{
                function setVal(el, val){{
                    if(!el || !val) return;
                    var setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype,'value').set
                              || Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype,'value').set;
                    setter.call(el, val);
                    el.dispatchEvent(new Event('input',{{bubbles:true}}));
                    el.dispatchEvent(new Event('change',{{bubbles:true}}));
                }}
                function pick(){{ for(var i=0;i<arguments.length;i++){{ try{{ var e=document.querySelector(arguments[i]); if(e) return e; }}catch(x){{}} }} return null; }}
                setVal(pick('input[autocomplete=""name""]','input[name*=""fullname"" i]','input[name=""name""]','input[id*=""fullname"" i]'), {JsStr(a.FullName)});
                setVal(pick('input[autocomplete=""email""]','input[type=""email""]','input[name*=""email"" i]'), {JsStr(a.Email)});
                setVal(pick('input[autocomplete=""tel""]','input[type=""tel""]','input[name*=""phone"" i]'), {JsStr(a.Phone)});
                setVal(pick('input[autocomplete=""address-line1""]','input[autocomplete=""street-address""]','input[name*=""address1"" i]','input[name*=""street"" i]','input[id*=""address1"" i]'), {JsStr(a.Line1)});
                setVal(pick('input[autocomplete=""address-line2""]','input[name*=""address2"" i]','input[id*=""address2"" i]'), {JsStr(a.Line2)});
                setVal(pick('input[autocomplete=""address-level2""]','input[name*=""city"" i]','input[id*=""city"" i]'), {JsStr(a.City)});
                setVal(pick('input[autocomplete=""address-level1""]','input[name*=""state"" i]','input[name*=""region"" i]','input[id*=""state"" i]'), {JsStr(a.State)});
                setVal(pick('input[autocomplete=""postal-code""]','input[name*=""zip"" i]','input[name*=""postal"" i]','input[id*=""zip"" i]','input[id*=""postal"" i]'), {JsStr(a.PostalCode)});
                setVal(pick('input[autocomplete=""country""]','input[name*=""country"" i]','select[name*=""country"" i]','[id*=""country"" i]'), {JsStr(a.Country)});
            }})()";
            try { await core.ExecuteScriptAsync(js); } catch { }
        }

        private void ShowCardPicker(BrowserTab tab)
        {
            var picker = new ContextMenuStrip { BackColor = Theme.ActiveTab, ForeColor = Color.White, ShowImageMargin = false };
            picker.Items.Add(new ToolStripMenuItem("Choose a card:") { Enabled = false, ForeColor = Theme.ForeDim });
            picker.Items.Add(new ToolStripSeparator());
            foreach (var card in savedCards)
            {
                var c = card;
                var item = new ToolStripMenuItem(c.Display) { ForeColor = Color.White, BackColor = Theme.ActiveTab };
                item.Click += async (_, _) =>
                {
                    picker.Close();
                    var core = tab.WebView.CoreWebView2;
                    if (core != null) { await FillCard(core, c); statusLabel.Text = $"Filled card •••• {c.Last4}"; }
                };
                picker.Items.Add(item);
            }
            var pt = webViewPanel.PointToScreen(new Point(webViewPanel.Width / 2 - 100, 10));
            picker.Show(pt);
        }

        private void ShowAddressPicker(BrowserTab tab)
        {
            var picker = new ContextMenuStrip { BackColor = Theme.ActiveTab, ForeColor = Color.White, ShowImageMargin = false };
            picker.Items.Add(new ToolStripMenuItem("Choose an address:") { Enabled = false, ForeColor = Theme.ForeDim });
            picker.Items.Add(new ToolStripSeparator());
            foreach (var address in savedAddresses)
            {
                var a = address;
                var item = new ToolStripMenuItem(a.Display) { ForeColor = Color.White, BackColor = Theme.ActiveTab };
                item.Click += async (_, _) =>
                {
                    picker.Close();
                    var core = tab.WebView.CoreWebView2;
                    if (core != null) { await FillAddress(core, a); statusLabel.Text = $"Filled address for {a.FullName}"; }
                };
                picker.Items.Add(item);
            }
            var pt = webViewPanel.PointToScreen(new Point(webViewPanel.Width / 2 - 100, 10));
            picker.Show(pt);
        }

        private async void TryAutoFillCredentials(BrowserTab tab)
        {
            if (savedPasswords.Count == 0) return;
            var core = tab.WebView.CoreWebView2;
            if (core == null) return;

            string pageUrl = core.Source ?? "";
            if (string.IsNullOrEmpty(pageUrl)) return;

            // Per-URL de-dupe: if a loop is already running for THIS exact URL, skip.
            // But a genuinely different URL (identifier -> password step) always proceeds
            // even while an older loop is still retrying — the older loop self-cancels when
            // it notices core.Source moved on. This is what removes both the "need to
            // refresh" symptom and the stuck-on-email-page symptom.
            if (tab.AutoFillInProgress
                && string.Equals(tab.LastAutoFillUrl, pageUrl, StringComparison.OrdinalIgnoreCase))
                return;
            if (!tab.AutoFillInProgress
                && string.Equals(tab.LastAutoFillUrl, pageUrl, StringComparison.OrdinalIgnoreCase)
                && (DateTime.Now - tab.LastAutoFillAttempt).TotalSeconds < 3)
                return;

            string? pageDomain = null;
            try { pageDomain = new Uri(pageUrl).Host.ToLower(); } catch { return; }

            var matches = savedPasswords.Where(p =>
            {
                try
                {
                    var savedHost = new Uri(p.Url).Host.ToLower();
                    // Match exact host or registrable-domain suffix so accounts.google.com
                    // credentials fill on the google.com password step and vice versa.
                    return savedHost == pageDomain
                        || pageDomain!.EndsWith("." + savedHost, StringComparison.Ordinal)
                        || savedHost.EndsWith("." + pageDomain, StringComparison.Ordinal);
                }
                catch { return false; }
            }).ToList();

            if (matches.Count == 0) return;

            // Login-like page heuristic. A page that actually contains a password field is
            // always treated as a login page even if the path has no login keyword — this
            // covers Google's /signin/v2/challenge/pwd and similar password-only steps.
            string pathLower = "";
            try { pathLower = new Uri(pageUrl).PathAndQuery.ToLower(); } catch { }
            bool isLoginPage = pathLower.Contains("login") || pathLower.Contains("signin") || pathLower.Contains("sign-in")
                || pathLower.Contains("auth") || pathLower.Contains("account") || pathLower.Contains("sso")
                || pathLower.Contains("challenge") || pathLower.Contains("pwd") || pathLower.Contains("identifier")
                || pathLower.Contains("register") || pathLower.Contains("signup") || pathLower.Contains("sign-up");

            // Mark this URL as claimed up front so a concurrent NavigationCompleted /
            // SourceChanged pair does not run two loops against the same page.
            tab.LastAutoFillAttempt = DateTime.Now;
            tab.LastAutoFillUrl = pageUrl;
            tab.AutoFillInProgress = true;
            long myToken = ++tab.AutoFillToken;
            try
            {
            // Retry up to 6 times with increasing delays for SPA pages
            for (int attempt = 0; attempt < 6; attempt++)
            {
                await Task.Delay(800 + (attempt * 600));

                if (tab.WebView.IsDisposed || tab.WebView.CoreWebView2 == null) return;
                core = tab.WebView.CoreWebView2;

                // Self-cancel if a newer autofill invocation superseded this one, or the
                // page navigated away from the URL this loop started for (email -> password
                // step). Either way the newer invocation owns the current page.
                if (tab.AutoFillToken != myToken) return;
                if (!string.Equals(core.Source ?? "", pageUrl, StringComparison.OrdinalIgnoreCase))
                    return;

                // Check for ANY input fields — password, email, text, tel
                string checkJs = @"(function() {
                    var pw = document.querySelector('input[type=""password""]');
                    var emailOrUser = document.querySelector(
                        'input[type=""email""], input[type=""tel""], input[name=""email""], input[name=""username""], ' +
                        'input[name=""login""], input[name=""user""], input[autocomplete=""username""], ' +
                        'input[autocomplete=""email""], input[aria-label*=""mail"" i], input[aria-label*=""user"" i], ' +
                        'input[aria-label*=""phone"" i], input[aria-label*=""login"" i], input[aria-label*=""Email""], ' +
                        'input[aria-label*=""Phone""]'
                    );
                    if (!emailOrUser) {
                        var all = document.querySelectorAll('input[type=""text""], input:not([type])');
                        for (var i = 0; i < all.length; i++) {
                            if (all[i].offsetParent !== null && all[i].offsetWidth > 0) { emailOrUser = all[i]; break; }
                        }
                    }
                    if (pw && emailOrUser) return 'both';
                    if (pw) return 'pwonly';
                    if (emailOrUser) return 'useronly';
                    return 'none';
                })()";

                try
                {
                    var result = await core.ExecuteScriptAsync(checkJs);
                    var fieldStatus = result.Trim('"');

                    if (fieldStatus == "none") continue;

                    // A password field present means the page is a login step regardless of the
                    // URL path — this is what makes Google's separate password page work.
                    if (fieldStatus == "pwonly")
                    {
                        // Password-only step (Google identifier -> pwd, or a re-auth prompt).
                        // Do NOT hunt for a username field: it is hidden and Google ignores
                        // writes to it. Fill the password only.
                        if (matches.Count == 1)
                        {
                            await FillPasswordOnly(core, matches[0].Password);
                            Invoke(() => statusLabel.Text = $"Auto-filled password for {pageDomain}");
                        }
                        else
                            Invoke(() => ShowCredentialPicker(tab, matches, passwordOnly: true));
                        return;
                    }

                    if (fieldStatus == "both")
                    {
                        if (matches.Count == 1)
                        {
                            await FillCredentials(core, matches[0].Username, matches[0].Password);
                            Invoke(() => statusLabel.Text = $"Auto-filled credentials for {pageDomain}");
                        }
                        else
                            Invoke(() => ShowCredentialPicker(tab, matches));
                        return;
                    }

                    if (fieldStatus == "useronly")
                    {
                        // Username/email-only step (Google identifier page, or a page that has
                        // not yet revealed the password field). Only fill on a real login page.
                        if (!isLoginPage) return;
                        if (matches.Count == 1)
                        {
                            await FillUsernameOnly(core, matches[0].Username);
                            Invoke(() => statusLabel.Text = $"Filled username for {pageDomain} (continue to password step)");
                        }
                        else
                            Invoke(() => ShowCredentialPicker(tab, matches));
                        return;
                    }
                }
                catch { }
            }
            try { Invoke(() => statusLabel.Text = $"No login fields detected on {pageDomain}"); } catch { }
            }
            finally
            {
                // Only clear the guard if we are still the current loop; a newer invocation
                // may already own it.
                if (tab.AutoFillToken == myToken)
                    tab.AutoFillInProgress = false;
            }
        }

        private async Task FillUsernameOnly(CoreWebView2 core, string username)
        {
            string safeUser = username.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "");
            string js = $@"(function() {{
                var user = document.querySelector(
                    'input[type=""email""], input[type=""tel""], input[name=""email""], input[name=""username""], ' +
                    'input[name=""login""], input[name=""user""], input[autocomplete=""username""], ' +
                    'input[autocomplete=""email""], input[aria-label*=""mail"" i], input[aria-label*=""user"" i], ' +
                    'input[aria-label*=""phone"" i], input[aria-label*=""login"" i], input[aria-label*=""Email""], ' +
                    'input[aria-label*=""Phone""]'
                );
                if (!user) {{
                    var all = document.querySelectorAll('input[type=""text""], input:not([type])');
                    for (var i = 0; i < all.length; i++) {{
                        if (all[i].offsetParent !== null && all[i].offsetWidth > 0) {{ user = all[i]; break; }}
                    }}
                }}
                if (user) {{
                    var nativeSet = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
                    nativeSet.call(user, '{safeUser}');
                    user.dispatchEvent(new Event('input', {{bubbles:true}}));
                    user.dispatchEvent(new Event('change', {{bubbles:true}}));
                    user.dispatchEvent(new Event('blur', {{bubbles:true}}));
                }}
            }})()";
            await core.ExecuteScriptAsync(js);
        }

        private async Task FillPasswordOnly(CoreWebView2 core, string password)
        {
            // Fill ONLY the visible password field. Password-only steps (Google's
            // /signin/v2/challenge/pwd, re-auth prompts) carry a hidden username input
            // that the site populates itself; writing to it can break the flow.
            string safePwd = password.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "");
            string js = $@"(function() {{
                var pws = document.querySelectorAll('input[type=""password""]');
                var pw = null;
                for (var i = 0; i < pws.length; i++) {{
                    // Prefer a visible password field over a hidden/offscreen one.
                    if (pws[i].offsetParent !== null && pws[i].offsetWidth > 0) {{ pw = pws[i]; break; }}
                }}
                if (!pw && pws.length) pw = pws[0];
                if (!pw) return;
                var nativeSet = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
                nativeSet.call(pw, '{safePwd}');
                pw.dispatchEvent(new Event('input', {{bubbles:true}}));
                pw.dispatchEvent(new Event('change', {{bubbles:true}}));
                pw.dispatchEvent(new Event('blur', {{bubbles:true}}));
            }})()";
            await core.ExecuteScriptAsync(js);
        }

        private async Task FillCredentials(CoreWebView2 core, string username, string password)
        {
            string safeUser = username.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "");
            string safePwd = password.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "");

            string fillJs = $@"(function() {{
                var pw = document.querySelector('input[type=""password""]');
                if (!pw) return;
                var form = pw.closest('form') || document.body;
                var user = form.querySelector([
                    'input[type=""email""]',
                    'input[name=""email""]',
                    'input[name=""username""]',
                    'input[name=""login""]',
                    'input[name=""user""]',
                    'input[autocomplete=""username""]',
                    'input[autocomplete=""email""]',
                    'input[type=""text""][name*=""user""]',
                    'input[type=""text""][name*=""login""]',
                    'input[type=""text""][name*=""email""]',
                    'input[type=""text""][autocomplete*=""user""]',
                    'input[aria-label*=""mail""]',
                    'input[aria-label*=""user""]',
                    'input[aria-label*=""login""]',
                    'input[aria-label*=""phone""]'
                ].join(', '));
                if (!user) {{
                    var inputs = form.querySelectorAll('input[type=""text""], input[type=""email""], input:not([type])');
                    for (var i = 0; i < inputs.length; i++) {{
                        var inp = inputs[i];
                        if (inp !== pw && inp.offsetParent !== null) {{ user = inp; break; }}
                    }}
                }}
                if (user) {{
                    var nativeSet = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
                    nativeSet.call(user, '{safeUser}');
                    user.dispatchEvent(new Event('input', {{bubbles:true}}));
                    user.dispatchEvent(new Event('change', {{bubbles:true}}));
                }}
                var nativeSet2 = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
                nativeSet2.call(pw, '{safePwd}');
                pw.dispatchEvent(new Event('input', {{bubbles:true}}));
                pw.dispatchEvent(new Event('change', {{bubbles:true}}));
            }})()";

            await core.ExecuteScriptAsync(fillJs);
        }

        private void ShowCredentialPicker(BrowserTab tab, List<SavedCredential> matches, bool passwordOnly = false)
        {
            var picker = new ContextMenuStrip { BackColor = Theme.ActiveTab, ForeColor = Color.White, ShowImageMargin = false };
            picker.Items.Add(new ToolStripMenuItem("Select account:") { Enabled = false, ForeColor = Theme.ForeDim });
            picker.Items.Add(new ToolStripSeparator());

            foreach (var cred in matches)
            {
                var c = cred; // capture
                var item = new ToolStripMenuItem(c.Username) { ForeColor = Color.White, BackColor = Theme.ActiveTab };
                item.Click += async (_, _) =>
                {
                    picker.Close();
                    var core = tab.WebView.CoreWebView2;
                    if (core != null)
                    {
                        if (passwordOnly)
                        {
                            // Password-only step (e.g. Google's separate password page):
                            // fill the password field only, leave the hidden username alone.
                            await FillPasswordOnly(core, c.Password);
                            statusLabel.Text = $"Filled password for {c.Username}";
                        }
                        else
                        {
                            await FillCredentials(core, c.Username, c.Password);
                            statusLabel.Text = $"Filled credentials for {c.Username}";
                        }
                    }
                };
                picker.Items.Add(item);
            }

            // Show near the top-left of the webview
            var pt = webViewPanel.PointToScreen(new Point(webViewPanel.Width / 2 - 80, 10));
            picker.Show(pt);
        }

        // ── CSV/JSON helpers ──
        private static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (c == ',' && !inQuotes) { fields.Add(current.ToString()); current.Clear(); continue; }
                current.Append(c);
            }
            fields.Add(current.ToString());
            return fields;
        }

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        private static List<SavedCredential> ParseCredentialJson(string json)
        {
            var list = new List<SavedCredential>();
            // Minimal JSON array parser for our known format
            int pos = 0;
            while (pos < json.Length)
            {
                int objStart = json.IndexOf('{', pos);
                if (objStart < 0) break;
                int objEnd = json.IndexOf('}', objStart);
                if (objEnd < 0) break;
                string obj = json.Substring(objStart + 1, objEnd - objStart - 1);

                string url = ExtractJsonValue(obj, "u");
                string user = ExtractJsonValue(obj, "n");
                string pwd = ExtractJsonValue(obj, "p");
                if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(user))
                    list.Add(new SavedCredential { Url = url, Username = user, Password = pwd });

                pos = objEnd + 1;
            }
            return list;
        }

        private static string ExtractJsonValue(string obj, string key)
        {
            string search = $"\"{key}\":\"";
            int start = obj.IndexOf(search, StringComparison.Ordinal);
            if (start < 0) return "";
            start += search.Length;
            var sb = new StringBuilder();
            for (int i = start; i < obj.Length; i++)
            {
                if (obj[i] == '\\' && i + 1 < obj.Length) { sb.Append(obj[i + 1]); i++; continue; }
                if (obj[i] == '"') break;
                sb.Append(obj[i]);
            }
            return sb.ToString();
        }
    }

    internal sealed class SavedCredential
    {
        public string Url { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    /// <summary>
    /// A stored payment method. Encrypted at rest with DPAPI (CurrentUser), same as passwords.
    /// The card number and CVC are sensitive; they never leave the local encrypted store.
    /// </summary>
    internal sealed class SavedCard
    {
        public string Label { get; set; } = "";        // user-friendly nickname e.g. "Personal Visa"
        public string CardholderName { get; set; } = "";
        public string Number { get; set; } = "";        // digits only
        public string ExpMonth { get; set; } = "";      // "01".."12"
        public string ExpYear { get; set; } = "";        // 4-digit
        public string Cvc { get; set; } = "";

        /// <summary>Last 4 digits for display without exposing the full number.</summary>
        public string Last4 => Number.Length >= 4 ? Number.Substring(Number.Length - 4) : Number;
        public string Display => string.IsNullOrWhiteSpace(Label)
            ? $"•••• {Last4}  ({ExpMonth}/{ExpYear})"
            : $"{Label} — •••• {Last4}  ({ExpMonth}/{ExpYear})";
    }

    /// <summary>
    /// A stored postal address / contact profile for checkout autofill. DPAPI-encrypted at rest.
    /// </summary>
    internal sealed class SavedAddress
    {
        public string Label { get; set; } = "";        // e.g. "Home", "Work"
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Line1 { get; set; } = "";
        public string Line2 { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string PostalCode { get; set; } = "";
        public string Country { get; set; } = "";

        public string Display => string.IsNullOrWhiteSpace(Label)
            ? $"{FullName} — {Line1}, {City}"
            : $"{Label}: {FullName} — {Line1}, {City}";
    }

    /// <summary>
    /// Dark-themed vertical field editor. Call AddField() for each row, then Finalize(),
    /// then ShowDialog(). Returns DialogResult.OK when the user clicks Save.
    /// </summary>
    internal sealed class FieldEditorForm : Form
    {
        private readonly TableLayoutPanel _layout;
        private int _row;

        public FieldEditorForm(string title)
        {
            Text = title;
            BackColor = Theme.TitleBar;
            ForeColor = Theme.ForeLight;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(380, 100);
            _layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(12),
                BackColor = Theme.TitleBar,
            };
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(_layout);
        }

        public TextBox AddField(string label, string value, bool isPassword = false)
        {
            var lbl = new Label
            {
                Text = label,
                ForeColor = Theme.ForeLight,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Height = 26,
            };
            var box = new TextBox
            {
                Text = value ?? "",
                BackColor = Theme.AddressBox,
                ForeColor = Theme.ForeLight,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                UseSystemPasswordChar = isPassword,
            };
            _layout.Controls.Add(lbl, 0, _row);
            _layout.Controls.Add(box, 1, _row);
            _row++;
            return box;
        }

        public void Build()
        {
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 44,
                Padding = new Padding(8),
                BackColor = Theme.TitleBar,
            };
            var save = new Button { Text = "Save", DialogResult = DialogResult.OK, BackColor = Theme.ActiveTab, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Width = 90 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, BackColor = Theme.InactiveTab, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Width = 90 };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            Controls.Add(buttons);
            AcceptButton = save;
            CancelButton = cancel;
            // Size to content
            ClientSize = new Size(Math.Max(380, _layout.PreferredSize.Width + 24), _layout.PreferredSize.Height + buttons.Height + 8);
        }
    }

    /// <summary>
    /// Dark-themed list manager for a collection of items: shows items, and Add / Edit / Delete
    /// buttons. addNew returns a new item (or null if cancelled); editExisting mutates/returns the
    /// edited item (or null if cancelled). The backing list is mutated in place.
    /// </summary>
    internal sealed class ListManagerDialog<T> : Form where T : class
    {
        private readonly List<T> _items;
        private readonly Func<T, string> _display;
        private readonly Func<T?> _addNew;
        private readonly Func<T, T?> _editExisting;
        private readonly ListBox _list;

        public ListManagerDialog(string title, List<T> items, Func<T, string> display, Func<T?> addNew, Func<T, T?> editExisting)
        {
            _items = items;
            _display = display;
            _addNew = addNew;
            _editExisting = editExisting;

            Text = title;
            BackColor = Theme.TitleBar;
            ForeColor = Theme.ForeLight;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            ClientSize = new Size(460, 320);
            MinimumSize = new Size(360, 240);

            _list = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.AddressBox,
                ForeColor = Theme.ForeLight,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false,
            };
            _list.DoubleClick += (_, _) => EditSelected();

            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.LeftToRight,
                Height = 46,
                Padding = new Padding(8),
                BackColor = Theme.TitleBar,
            };
            Button Mk(string t) => new Button { Text = t, BackColor = Theme.ActiveTab, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Width = 90, Height = 28 };
            var add = Mk("Add");
            var edit = Mk("Edit");
            var del = Mk("Delete");
            var close = Mk("Close");
            add.Click += (_, _) => { var n = _addNew(); if (n != null) { _items.Add(n); Refresh(); } };
            edit.Click += (_, _) => EditSelected();
            del.Click += (_, _) =>
            {
                if (_list.SelectedIndex >= 0 && _list.SelectedIndex < _items.Count)
                {
                    _items.RemoveAt(_list.SelectedIndex);
                    Refresh();
                }
            };
            close.Click += (_, _) => Close();
            bar.Controls.Add(add);
            bar.Controls.Add(edit);
            bar.Controls.Add(del);
            bar.Controls.Add(close);

            Controls.Add(_list);
            Controls.Add(bar);
            Refresh();
        }

        private void EditSelected()
        {
            int idx = _list.SelectedIndex;
            if (idx < 0 || idx >= _items.Count) return;
            var edited = _editExisting(_items[idx]);
            if (edited != null) { _items[idx] = edited; Refresh(); }
        }

        private new void Refresh()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var item in _items) _list.Items.Add(_display(item));
            _list.EndUpdate();
        }
    }
}
