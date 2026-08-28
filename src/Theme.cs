using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Vaktmester
{
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(0x14, 0x16, 0x1A);
        public static readonly Color Side = Color.FromArgb(0x10, 0x12, 0x16);
        public static readonly Color Card = Color.FromArgb(0x1C, 0x1F, 0x26);
        public static readonly Color CardHi = Color.FromArgb(0x23, 0x27, 0x30);
        public static readonly Color Line = Color.FromArgb(0x2A, 0x2F, 0x3A);
        public static readonly Color Text = Color.FromArgb(0xE6, 0xE9, 0xEF);
        public static readonly Color Muted = Color.FromArgb(0x8B, 0x93, 0xA3);
        public static readonly Color Accent = Color.FromArgb(0x4C, 0x8D, 0xFF);
        public static readonly Color Good = Color.FromArgb(0x3F, 0xBF, 0x7F);
        public static readonly Color Warn = Color.FromArgb(0xE0, 0xA3, 0x3E);
        public static readonly Color Bad = Color.FromArgb(0xE5, 0x54, 0x4B);

        public static readonly Font F = new Font("Segoe UI", 9.75f);
        public static readonly Font FBold = new Font("Segoe UI Semibold", 9.75f);
        public static readonly Font FSmall = new Font("Segoe UI", 8.5f);
        public static readonly Font FTitle = new Font("Segoe UI Light", 20f);
        public static readonly Font FCard = new Font("Segoe UI Semibold", 11f);
        public static readonly Font FBig = new Font("Segoe UI Light", 26f);
        public static readonly Font FMono = new Font("Consolas", 9f);

        // ---- Mørk modus for innebygde kontroller (Win10 1809+) ----
        [DllImport("uxtheme.dll", EntryPoint = "#135", CharSet = CharSet.Unicode)]
        static extern int SetPreferredAppMode(int mode);
        [DllImport("uxtheme.dll", EntryPoint = "#136")]
        static extern void FlushMenuThemes();
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hWnd, string sub, string id);
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        public static void EnableDarkMode()
        {
            try { SetPreferredAppMode(2); FlushMenuThemes(); } catch { }
        }

        public static void DarkTitleBar(Form f)
        {
            try
            {
                int on = 1;
                DwmSetWindowAttribute(f.Handle, 20, ref on, 4);   // DWMWA_USE_IMMERSIVE_DARK_MODE
            }
            catch { }
        }

        public static void DarkControl(Control c, string theme)
        {
            try { SetWindowTheme(c.Handle, theme, null); } catch { }
        }

        // Programikonet. Hentes fra den innebygde .ico-fila som inneholder
        // 16/24/32/48/64/128/256 - da velger Windows riktig storrelse selv,
        // i stedet for a skalere ned en stor bitmap til en grot i tittellinja.
        static Icon appIcon;
        static bool appIconTried;

        public static Icon AppIcon()
        {
            if (appIconTried) return appIcon;
            appIconTried = true;
            try
            {
                using (System.IO.Stream st = System.Reflection.Assembly
                           .GetExecutingAssembly().GetManifestResourceStream("vaktmester.icon"))
                    if (st != null) appIcon = new Icon(st);
            }
            catch { }
            if (appIcon == null)
            {
                // Nodlosning: tegn merket. Darligere kanter, men bedre enn standardikonet.
                try { appIcon = Icon.FromHandle(Logo.Bitmap(32, true).GetHicon()); }
                catch { }
            }
            return appIcon;
        }

        public static void ApplyIcon(Form f)
        {
            try
            {
                Icon i = AppIcon();
                if (i != null) f.Icon = i;
            }
            catch { }
        }

        // ---- Byggeklosser ----
        public static ListView MakeList()
        {
            ListView lv = new ListView();
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.GridLines = false;
            lv.BorderStyle = BorderStyle.None;
            lv.BackColor = Card;
            lv.ForeColor = Text;
            lv.Font = F;
            lv.HideSelection = false;
            lv.HandleCreated += delegate { DarkControl(lv, "DarkMode_Explorer"); };
            return lv;
        }

        public static Label Lbl(string text, Font font, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = font;
            l.ForeColor = color;
            l.BackColor = Color.Transparent;
            l.AutoSize = true;
            return l;
        }

        public static Panel MakeCard()
        {
            Panel p = new Panel();
            p.BackColor = Card;
            p.Padding = new Padding(16);
            p.Paint += delegate(object s, PaintEventArgs e)
            {
                Panel me = (Panel)s;
                using (Pen pen = new Pen(Line))
                    e.Graphics.DrawRectangle(pen, 0, 0, me.Width - 1, me.Height - 1);
            };
            return p;
        }
    }

    // Flat knapp med tydelig hover/press og valgfri aksentfarge.
    class FlatBtn : Button
    {
        public Color Base = Theme.CardHi;
        public Color Hover = Color.FromArgb(0x2E, 0x33, 0x3E);
        bool over, down;

        public FlatBtn(string text)
        {
            Text = text;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            ForeColor = Theme.Text;
            Font = Theme.FBold;
            Height = 34;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            MouseEnter += delegate { over = true; Invalidate(); };
            MouseLeave += delegate { over = false; down = false; Invalidate(); };
            MouseDown += delegate { down = true; Invalidate(); };
            MouseUp += delegate { down = false; Invalidate(); };
        }

        public FlatBtn Primary()
        {
            Base = Theme.Accent;
            Hover = Color.FromArgb(0x62, 0x9C, 0xFF);
            ForeColor = Color.White;
            return this;
        }

        public FlatBtn Danger()
        {
            Base = Color.FromArgb(0x4A, 0x24, 0x24);
            Hover = Color.FromArgb(0x62, 0x2E, 0x2E);
            ForeColor = Color.FromArgb(0xFF, 0xB8, 0xB3);
            return this;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color c = !Enabled ? Color.FromArgb(0x22, 0x25, 0x2C)
                    : down ? ControlPaint.Dark(Hover, 0.08f)
                    : over ? Hover : Base;
            e.Graphics.Clear(c);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
                Enabled ? ForeColor : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }
    }

    // Navigasjonsknapp i sidemenyen.
    class NavBtn : Control
    {
        public bool Active;
        bool over;
        public string Sub = "";

        public NavBtn(string text)
        {
            Text = text;
            Height = 44;
            Dock = DockStyle.Top;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            MouseEnter += delegate { over = true; Invalidate(); };
            MouseLeave += delegate { over = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Active ? Color.FromArgb(0x1B, 0x1F, 0x28)
                  : over ? Color.FromArgb(0x16, 0x19, 0x1F) : Theme.Side);
            if (Active)
                using (SolidBrush b = new SolidBrush(Theme.Accent))
                    g.FillRectangle(b, 0, 8, 3, Height - 16);

            TextRenderer.DrawText(g, Text, Active ? Theme.FBold : Theme.F,
                new Rectangle(18, 0, Width - 24, Height),
                Active ? Theme.Text : Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    // Enkel horisontal måler.
    class Bar : Control
    {
        public double Value;                     // 0..1
        public Color Fill = Theme.Accent;

        public Bar()
        {
            Height = 8;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Card);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(0x2A, 0x2F, 0x3A)))
                g.FillRectangle(b, 0, 0, Width, Height);
            int w = (int)Math.Round(Math.Max(0, Math.Min(1, Value)) * Width);
            if (w > 0)
                using (SolidBrush b = new SolidBrush(Fill))
                    g.FillRectangle(b, 0, 0, w, Height);
        }
    }
}
