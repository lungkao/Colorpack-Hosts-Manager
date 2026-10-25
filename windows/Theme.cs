// Hosts Manager (Windows) — ธีม Colorpack: สี, ฟอนต์ Kanit, ปุ่มมน, แถบหัว และเครดิตบริษัท
// Colorpack theme: colours, embedded Kanit font, rounded buttons, header band and company credit.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HostsManager
{
    public static class CP
    {
        public const string Version = "1.4";
        public const string RepoUrl = "https://github.com/lungkao/Colorpack-Hosts-Manager";

        static Color Hex(int v) => Color.FromArgb((v >> 16) & 255, (v >> 8) & 255, v & 255);
        public static readonly Color Blue = Hex(0x3B8FE0), BlueHover = Hex(0x2F7AC4), BlueDeep = Hex(0x1F5FA8), BlueTint = Hex(0xE3EFFB);
        public static readonly Color Green = Hex(0x6CC04A), GreenDeep = Hex(0x2F7A1A), GreenTint = Hex(0xE8F5E1);
        public static readonly Color Orange = Hex(0xF7941C), OrangeDeep = Hex(0x9A5300), OrangeTint = Hex(0xFEF0DC);
        public static readonly Color Red = Hex(0xEF2B2C), RedDeep = Hex(0xB81B1C), RedTint = Hex(0xFDE8E8);
        public static readonly Color Gray = Hex(0x666666), Ink = Hex(0x111111), Paper = Color.White;
        public static readonly Color Ground = Hex(0xF4F6F8), Line = Hex(0xE1E6EB);

        // ---------- fonts ----------
        static readonly PrivateFontCollection pfc = new PrivateFontCollection();
        static readonly List<IntPtr> gdiHandles = new List<IntPtr>();
        static bool loaded;

        [DllImport("gdi32.dll")]
        static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, out uint pcFonts);

        /// <summary>โหลด Kanit จาก resource ให้ทั้ง GDI+ และ GDI ใช้ได้ / load embedded Kanit for GDI+ and GDI</summary>
        public static void LoadFonts()
        {
            if (loaded) return;
            loaded = true;
            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in asm.GetManifestResourceNames().Where(n => n.EndsWith(".ttf")))
            {
                try
                {
                    using (var s = asm.GetManifestResourceStream(name))
                    {
                        var data = new byte[s.Length];
                        s.Read(data, 0, data.Length);
                        var ptr = Marshal.AllocCoTaskMem(data.Length);
                        Marshal.Copy(data, 0, ptr, data.Length);
                        pfc.AddMemoryFont(ptr, data.Length);
                        gdiHandles.Add(AddFontMemResourceEx(ptr, (uint)data.Length, IntPtr.Zero, out _));
                        // ไม่คืนหน่วยความจำ — ฟอนต์ต้องอยู่จนปิดแอป / memory must live as long as the fonts are used
                    }
                }
                catch { }
            }
        }

        static FontFamily Family(string name)
        {
            LoadFonts();
            return pfc.Families.FirstOrDefault(f => f.Name == name)
                ?? pfc.Families.FirstOrDefault(f => f.Name == "Kanit")
                ?? new FontFamily("Segoe UI");
        }

        static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();
        static Font Get(string family, float pt, FontStyle style = FontStyle.Regular)
        {
            var key = family + pt + style;
            if (!cache.TryGetValue(key, out var f)) cache[key] = f = new Font(Family(family), pt, style, GraphicsUnit.Point);
            return f;
        }

        // ขนาดตาม Design System: body 17px, small/label 15px, h2 24px (ที่ 96 dpi: px × 0.75 = pt)
        public static Font Body => Get("Kanit", 11.25f);
        public static Font Small => Get("Kanit", 10f);
        public static Font Label => Get("Kanit Medium", 10f);
        public static Font Button => Get("Kanit Medium", 10.5f);
        public static Font H2 => Get("Kanit SemiBold", 15f);
        public static Font H3 => Get("Kanit SemiBold", 12.75f);
        public static readonly Font Mono = new Font("Consolas", 10.5f);

        // ---------- logo ----------
        public static Image Logo(bool dark)
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(dark ? "colorpack-logo-on-dark.png" : "colorpack-logo-on-light.png"));
            if (name == null) return null;
            using (var s = asm.GetManifestResourceStream(name)) return Image.FromStream(new MemoryStream(ReadAll(s)));
        }

        static byte[] ReadAll(Stream s)
        {
            using (var m = new MemoryStream()) { s.CopyTo(m); return m.ToArray(); }
        }

        public static void OpenUrl(string url)
        {
            // เปิดผ่าน explorer เพื่อไม่ให้เบราว์เซอร์รันด้วยสิทธิ์ admin
            try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + url + "\"") { UseShellExecute = true }); } catch { }
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void StyleGrid(DataGridView g)
        {
            g.BackgroundColor = Paper;
            g.BorderStyle = BorderStyle.None;
            g.GridColor = Line;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.ColumnHeadersDefaultCellStyle.BackColor = Paper;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Gray;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Paper;
            g.ColumnHeadersDefaultCellStyle.Font = Label;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 8, 4, 8);
            g.DefaultCellStyle.Font = Body;
            g.DefaultCellStyle.ForeColor = Ink;
            g.DefaultCellStyle.BackColor = Paper;
            g.DefaultCellStyle.SelectionBackColor = BlueTint;
            g.DefaultCellStyle.SelectionForeColor = Ink;
            g.DefaultCellStyle.Padding = new Padding(4, 6, 4, 6);
            g.RowTemplate.MinimumHeight = 40;
        }
    }

    /// <summary>ปุ่มมนตามธีม / rounded theme button</summary>
    public class RoundButton : Button
    {
        public enum Kinds { Primary, Secondary, Danger, Coffee, Pill, PillOn, PillDark, PillDarkOn }
        Kinds kind;
        bool hover, down;

        public Kinds Kind { get => kind; set { kind = value; Invalidate(); } }
        public bool Small { get; set; }

        public RoundButton(Kinds kind = Kinds.Secondary, bool small = false)
        {
            this.kind = kind;
            Small = small;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Font = small ? CP.Small : CP.Button;
            Margin = new Padding(4);
        }

        public override Size GetPreferredSize(Size proposed)
        {
            var t = TextRenderer.MeasureText(Text ?? "", Font);
            int padX = Small ? 14 : 20;
            int h = Small ? 32 : 40;
            return new Size(t.Width + padX * 2, Math.Max(h, t.Height + 10));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? CP.Paper);
            Color bg, fg, border;
            bool active = hover || down;
            switch (kind)
            {
                case Kinds.Primary: bg = active ? CP.BlueHover : CP.Blue; fg = Color.White; border = Color.Empty; break;
                case Kinds.Danger: bg = active ? CP.RedTint : CP.Paper; fg = CP.RedDeep; border = Color.FromArgb(150, CP.Red); break;
                case Kinds.Coffee: bg = active ? ControlPaint.Dark(CP.Orange, 0.05f) : CP.Orange; fg = CP.Ink; border = Color.Empty; break;
                case Kinds.Pill: bg = active ? CP.Ground : CP.Paper; fg = CP.Ink; border = Color.Empty; break;
                case Kinds.PillOn: bg = CP.Blue; fg = Color.White; border = Color.Empty; break;
                case Kinds.PillDark: bg = active ? Color.FromArgb(60, 60, 60) : Color.FromArgb(40, 40, 40); fg = Color.FromArgb(220, 220, 220); border = Color.Empty; break;
                case Kinds.PillDarkOn: bg = Color.White; fg = CP.Ink; border = Color.Empty; break;
                default: bg = active ? CP.Ground : CP.Paper; fg = CP.Ink; border = active ? CP.Ink : CP.Line; break;
            }
            if (!Enabled) { bg = Color.FromArgb(110, bg); fg = Color.FromArgb(130, fg); }
            bool pill = kind >= Kinds.Coffee;
            float radius = pill ? Height / 2f : (Small ? 9 : 12);
            var r = new RectangleF(1, 1, Width - 3, Height - 3);
            using (var path = CP.Round(r, radius))
            {
                using (var b = new SolidBrush(bg)) g.FillPath(b, path);
                if (border != Color.Empty) using (var p = new Pen(border, 1.5f)) g.DrawPath(p, path);
            }
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues)
                using (var p = new Pen(Color.FromArgb(90, CP.Blue), 2f)) using (var path = CP.Round(new RectangleF(2, 2, Width - 5, Height - 5), radius)) g.DrawPath(p, path);
        }
    }

    /// <summary>แผงพื้นขาวมุมมน / white rounded card</summary>
    public class Card : Panel
    {
        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = CP.Paper;
            Padding = new Padding(14);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? CP.Ground);
            using (var path = CP.Round(new RectangleF(0.5f, 0.5f, Width - 2, Height - 2), 14))
            {
                using (var b = new SolidBrush(BackColor)) g.FillPath(b, path);
                using (var p = new Pen(CP.Line)) g.DrawPath(p, path);
            }
        }
    }

    /// <summary>แถบหัวสีเข้ม: โลโก้ ชื่อแอป เวอร์ชัน ภาษา / dark header band</summary>
    public class HeaderBand : Panel
    {
        public readonly Label Title, Subtitle, VersionPill, LangLabel;
        public readonly RoundButton Th, En;

        public HeaderBand()
        {
            Dock = DockStyle.Top;
            Height = 76;
            BackColor = CP.Ink;
            Padding = new Padding(22, 12, 22, 12);

            var logo = new PictureBox
            {
                Image = CP.Logo(dark: true), SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(170, 44), Margin = new Padding(0, 4, 16, 0), BackColor = CP.Ink
            };
            var sep = new Panel { Size = new Size(1, 40), BackColor = Color.FromArgb(70, 70, 70), Margin = new Padding(0, 6, 16, 0) };
            Title = new Label { AutoSize = true, ForeColor = Color.White, Font = CP.H2, Margin = new Padding(0, 0, 0, 0), Text = "Hosts Manager" };
            Subtitle = new Label { AutoSize = true, ForeColor = Color.FromArgb(170, 170, 170), Font = CP.Small, Margin = new Padding(2, 0, 0, 0) };
            var titles = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, BackColor = CP.Ink, Margin = new Padding(0) };
            titles.Controls.Add(Title);
            titles.Controls.Add(Subtitle);
            var left = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, WrapContents = false, BackColor = CP.Ink };
            left.Controls.AddRange(new Control[] { logo, sep, titles });

            VersionPill = new Label
            {
                AutoSize = true, Text = "v" + CP.Version, ForeColor = Color.White, BackColor = Color.FromArgb(45, 45, 45),
                Font = CP.Label, Padding = new Padding(10, 4, 10, 4), Margin = new Padding(0, 12, 14, 0)
            };
            LangLabel = new Label { AutoSize = true, ForeColor = Color.FromArgb(170, 170, 170), Font = CP.Small, Margin = new Padding(0, 14, 6, 0) };
            Th = new RoundButton(RoundButton.Kinds.PillDark, small: true) { Text = "ไทย", Margin = new Padding(2, 6, 2, 0) };
            En = new RoundButton(RoundButton.Kinds.PillDark, small: true) { Text = "EN", Margin = new Padding(2, 6, 0, 0) };
            var right = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = CP.Ink };
            right.Controls.AddRange(new Control[] { VersionPill, LangLabel, Th, En });

            Controls.Add(right);
            Controls.Add(left);
        }

        public void SetLang(Lang lang)
        {
            Th.Kind = lang == Lang.Th ? RoundButton.Kinds.PillDarkOn : RoundButton.Kinds.PillDark;
            En.Kind = lang == Lang.En ? RoundButton.Kinds.PillDarkOn : RoundButton.Kinds.PillDark;
        }
    }

    /// <summary>เครดิตบริษัทท้ายหน้าต่าง / Colorpack credit footer</summary>
    public class CreditFooter : Panel
    {
        public CreditFooter()
        {
            Dock = DockStyle.Bottom;
            Height = 74;
            BackColor = CP.Paper;
            Padding = new Padding(22, 12, 22, 10);

            var logo = new PictureBox
            {
                Image = CP.Logo(dark: false), SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(150, 40), Margin = new Padding(0, 2, 14, 0)
            };
            var coTh = new Label { AutoSize = true, Text = "บริษัท คัลเลอร์แพค ครีเอชั่น จำกัด", Font = CP.H3, ForeColor = CP.Ink, Margin = new Padding(0) };
            var coEn = new Label { AutoSize = true, Text = "C O L O R P A C K   C R E A T I O N   C O . ,   L T D .", Font = new Font(CP.Small.FontFamily, 7.5f), ForeColor = CP.Gray, Margin = new Padding(2, 0, 0, 0) };
            var names = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 0) };
            names.Controls.Add(coTh);
            names.Controls.Add(coEn);
            var left = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, WrapContents = false };
            left.Controls.AddRange(new Control[] { logo, names });

            var copyright = new Label
            {
                AutoSize = true, Font = CP.Small, ForeColor = CP.Gray, Anchor = AnchorStyles.Right, Margin = new Padding(0),
                Text = $"© {DateTime.Now.Year} บริษัท คัลเลอร์แพค ครีเอชั่น จำกัด สงวนลิขสิทธิ์"
            };
            var links = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Right, Margin = new Padding(0) };
            links.Controls.Add(Link("colorpack.co.th", "https://colorpack.co.th/"));
            links.Controls.Add(new Label { AutoSize = true, Font = CP.Small, ForeColor = CP.Gray, Text = $"·  Hosts Manager v{CP.Version}  ·", Margin = new Padding(0) });
            links.Controls.Add(Link("GitHub", CP.RepoUrl));
            var lines = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Margin = new Padding(0, 6, 14, 0) };
            lines.Controls.Add(copyright, 0, 0);
            lines.Controls.Add(links, 0, 1);

            var coffee = new RoundButton(RoundButton.Kinds.Coffee) { Text = "☕  Buy me a coffee", Margin = new Padding(0, 10, 0, 0) };
            coffee.Click += (s, e) => CP.OpenUrl("https://buymeacoffee.com/cheuachatchai");
            var right = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false };
            right.Controls.Add(lines);
            right.Controls.Add(coffee);

            Controls.Add(right);
            Controls.Add(left);
        }

        static LinkLabel Link(string text, string url)
        {
            var l = new LinkLabel
            {
                AutoSize = true, Text = text, Font = CP.Small, LinkColor = CP.BlueDeep, ActiveLinkColor = CP.Blue,
                LinkBehavior = LinkBehavior.HoverUnderline, Margin = new Padding(0)
            };
            l.LinkClicked += (s, e) => CP.OpenUrl(url);
            return l;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(CP.Line)) e.Graphics.DrawLine(p, 0, 0, Width, 0);
        }
    }
}
