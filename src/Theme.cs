using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Brisk
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
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);

        const int LVM_GETHEADER = 0x1000 + 31;

        // ListView-overskriften er en egen kontroll. DarkMode_Explorer pa selve
        // lista treffer den ikke - den ma ha DarkMode_ItemsView selv, ellers blir
        // overskriftsraden hvit midt i et morkt vindu.
        public static void DarkListHeader(ListView lv)
        {
            try
            {
                if (!lv.IsHandleCreated) return;
                IntPtr h = SendMessage(lv.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
                if (h != IntPtr.Zero) SetWindowTheme(h, "DarkMode_ItemsView", null);
            }
            catch { }
        }

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

        // Runde hjorner som resten av Windows 11. Et rammelost vindu faar dem
        // ikke av seg selv - Windows runder bare vinduer den tegner ramma paa.
        // Attributtet finnes fra build 22000; paa eldre Windows gjor kallet
        // ingenting, og da staar hjornene skarpe slik de alltid har gjort.
        public static void RoundCorners(Form f)
        {
            try
            {
                int rundt = 2;   // DWMWCP_ROUND
                DwmSetWindowAttribute(f.Handle, 33, ref rundt, 4);
            }
            catch (Exception) { }
        }

        // Kombobokser blir hvite uansett BackColor. DarkMode_CFD er temaet
        // Windows selv bruker pa mork combobox fra 1903 og utover.
        public static void DarkCombo(ComboBox c)
        {
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = CardHi;
            c.ForeColor = Text;
            c.DrawMode = DrawMode.OwnerDrawFixed;
            c.ItemHeight = 20;
            c.DrawItem += delegate(object s, DrawItemEventArgs e)
            {
                ComboBox me = (ComboBox)s;
                bool sel = (e.State & DrawItemState.Selected) != 0;
                using (SolidBrush b = new SolidBrush(sel ? Accent : CardHi))
                    e.Graphics.FillRectangle(b, e.Bounds);
                if (e.Index >= 0 && e.Index < me.Items.Count)
                    TextRenderer.DrawText(e.Graphics, Convert.ToString(me.Items[e.Index]), F,
                        new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height),
                        sel ? Color.White : Text,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };
            c.HandleCreated += delegate { DarkControl(c, "DarkMode_CFD"); };
            if (c.IsHandleCreated) DarkControl(c, "DarkMode_CFD");
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
                           .GetExecutingAssembly().GetManifestResourceStream("brisk.icon"))
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
            // Ma settes bade na og ved HandleCreated: rekkefolgen varierer med
            // hvordan kontrollen blir foreldret, og bommer man forsvinner det
            // morke temaet pa kolonneoverskriftene.
            EnableSorting(lv);
            FitColumns(lv);
            lv.HandleCreated += delegate
            {
                DarkControl(lv, "DarkMode_Explorer");
                DarkListHeader(lv);
            };
            if (lv.IsHandleCreated)
            {
                DarkControl(lv, "DarkMode_Explorer");
                DarkListHeader(lv);
            }
            return lv;
        }

        // Klikk paa en kolonneoverskrift sorterer. Andre klikk snur.
        // Verdiene i listene er ofte tall med enhet - "1,9 TB", "37 °C",
        // "5 % brukt", "4,9 s" - saa en ren tekstsortering ville satt 10 foer 9.
        // Comparer prover derfor tall og dato foerst.
        public static void EnableSorting(ListView lv)
        {
            ListSorter sorter = new ListSorter();
            lv.ListViewItemSorter = null;
            lv.ColumnClick += delegate(object s, ColumnClickEventArgs e)
            {
                if (e.Column == sorter.Column) sorter.Descending = !sorter.Descending;
                else { sorter.Column = e.Column; sorter.Descending = false; }
                lv.ListViewItemSorter = sorter;
                lv.Sort();
            };
        }

        // Plasserer barna i en kontroll, og gjor det paa nytt hver gang den
        // endrer storrelse.
        //
        // Grunnen til at dette finnes: Resize alene fyrer ikke paalitelig for
        // forste tegning. Legger man en kontroll i en celle i en
        // TableLayoutPanel, faar den sin virkelige storrelse etter at handleren
        // er koblet paa, og da blir knapper og tekst staaende plassert etter
        // standardstorrelsen - typisk 200x100. Resultatet er knapper midt oppi
        // teksten, og bokser som ikke staar i flukt med hverandre.
        //
        // Layout fyrer ogsaa naar foreldrekontrollen fordeler plassen, og
        // kallet til slutt sorger for at oppsettet er riktig med en gang.
        public static void Arrange(Control c, Action layout)
        {
            if (c == null || layout == null) return;
            bool inne = false;
            EventHandler kjor = delegate
            {
                if (inne) return;          // sett barnas storrelse kan utlose Layout paa nytt
                inne = true;
                try { layout(); }
                catch (Exception) { }
                inne = false;
            };
            c.SizeChanged += kjor;
            c.Layout += delegate { kjor(c, EventArgs.Empty); };
            kjor(c, EventArgs.Empty);
        }

        // Fordeler kolonnebreddene over hele lista, med samme innbyrdes forhold
        // som de ble satt opp med. Uten dette blir det staaende en dod stripe
        // til hoyre naar vinduet er bredere enn summen av kolonnene, og
        // kolonnene renner utenfor naar det er smalere.
        public static void FitColumns(ListView lv)
        {
            if (lv == null) return;
            bool inne = false;
            EventHandler fordel = delegate
            {
                if (inne || lv.Columns.Count == 0) return;
                inne = true;
                try
                {
                    // Litt margin, ellers gir avrunding et vannrett rullefelt.
                    // Fire piksler var ikke nok naar lista er smal - da ble det
                    // et rullefelt under en liste som hadde god plass.
                    int plass = lv.ClientSize.Width - 8;
                    if (plass < 60) { inne = false; return; }

                    int sum = 0;
                    int[] onsket = new int[lv.Columns.Count];
                    for (int i = 0; i < lv.Columns.Count; i++)
                    {
                        onsket[i] = lv.Columns[i].Tag == null
                            ? lv.Columns[i].Width
                            : Convert.ToInt32(lv.Columns[i].Tag);
                        lv.Columns[i].Tag = onsket[i];   // husk oppsettet
                        sum += onsket[i];
                    }
                    if (sum <= 0) { inne = false; return; }

                    int brukt = 0;
                    for (int i = 0; i < lv.Columns.Count; i++)
                    {
                        int w = i == lv.Columns.Count - 1
                            ? plass - brukt
                            : (int)Math.Round(plass * (onsket[i] / (double)sum));
                        if (w < 40) w = 40;
                        lv.Columns[i].Width = w;
                        brukt += w;
                    }
                }
                catch (Exception) { }
                inne = false;
            };
            lv.SizeChanged += fordel;
            lv.HandleCreated += fordel;
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

    // Morkt utseende pa hurtigmenyer.
    class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColors()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Selected ? Color.White : Theme.Text;
            base.OnRenderItemText(e);
        }

        class DarkColors : ProfessionalColorTable
        {
            public override Color MenuItemSelected { get { return Theme.Accent; } }
            public override Color MenuItemSelectedGradientBegin { get { return Theme.Accent; } }
            public override Color MenuItemSelectedGradientEnd { get { return Theme.Accent; } }
            public override Color MenuItemBorder { get { return Theme.Accent; } }
            public override Color ToolStripDropDownBackground { get { return Theme.CardHi; } }
            public override Color ImageMarginGradientBegin { get { return Theme.CardHi; } }
            public override Color ImageMarginGradientMiddle { get { return Theme.CardHi; } }
            public override Color ImageMarginGradientEnd { get { return Theme.CardHi; } }
            public override Color MenuBorder { get { return Theme.Line; } }
        }
    }

    // Knapp som apner en liste med valg. Erstatter ComboBox, som ikke lar seg
    // gjore mork pa en palitelig mate.
    class Chooser : FlatBtn
    {
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        public event EventHandler Changed;
        public string Value = "";

        public Chooser() : base("")
        {
            Font = Theme.F;
            menu.Renderer = new DarkMenuRenderer();
            menu.BackColor = Theme.CardHi;
            menu.ForeColor = Theme.Text;
            menu.ShowImageMargin = false;
            Click += delegate { menu.Show(this, new Point(0, Height)); };
        }

        public void Add(string item)
        {
            ToolStripMenuItem mi = new ToolStripMenuItem(item);
            mi.BackColor = Theme.CardHi;
            mi.ForeColor = Theme.Text;
            mi.Click += delegate
            {
                Value = item;
                Text = item;
                EventHandler h = Changed;
                if (h != null) h(this, EventArgs.Empty);
            };
            menu.Items.Add(mi);
            if (menu.Items.Count == 1) { Value = item; Text = item; }
        }

        public int Count { get { return menu.Items.Count; } }
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

        public FlatBtn Good()
        {
            Base = Color.FromArgb(0x1E, 0x4A, 0x35);
            Hover = Color.FromArgb(0x27, 0x60, 0x44);
            ForeColor = Color.FromArgb(0xB6, 0xF0, 0xD1);
            return this;
        }

        public FlatBtn Warn()
        {
            Base = Color.FromArgb(0x4A, 0x3C, 0x1C);
            Hover = Color.FromArgb(0x60, 0x4D, 0x24);
            ForeColor = Color.FromArgb(0xFF, 0xDF, 0xA8);
            return this;
        }

        // Stor variant til hovedhandlingen paa en side.
        public FlatBtn Big()
        {
            Height = 44;
            Font = new Font("Segoe UI Semibold", 11f);
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
        public string Key = "";

        public NavBtn(string text)
        {
            Text = text;
            // 44 og ikke 46: med fire gruppeoverskrifter i tillegg maa tolv
            // punkter fortsatt faa plass naar vinduet staar paa minstehoyden.
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

            Color fg = Active ? Theme.Text : Theme.Muted;
            Icons.Draw(g, Key, new RectangleF(20, (Height - 18) / 2f, 18, 18),
                Active ? Theme.Accent : fg);

            TextRenderer.DrawText(g, Text, Active ? Theme.FBold : Theme.F,
                new Rectangle(52, 0, Width - 58, Height), fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    // Enkel horisontal måler. Indeterminate gir en vandrende stripe naar
    // vi ikke vet hvor langt vi har kommet.
    class Bar : Control
    {
        public double Value;                     // 0..1
        public Color Fill = Theme.Accent;
        public bool Indeterminate;
        int phase;
        System.Windows.Forms.Timer anim;

        public void Pulse(bool on)
        {
            Indeterminate = on;
            if (anim == null)
            {
                anim = new System.Windows.Forms.Timer();
                anim.Interval = 40;
                anim.Tick += delegate { phase = (phase + 6) % 260; Invalidate(); };
            }
            if (on) anim.Start(); else anim.Stop();
            Visible = on || Value > 0;
            Invalidate();
        }

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
            if (Indeterminate)
            {
                int bw = Math.Max(60, Width / 5);
                int x = (int)((phase / 260.0) * (Width + bw)) - bw;
                using (SolidBrush b = new SolidBrush(Fill))
                    g.FillRectangle(b, x, 0, bw, Height);
                return;
            }
            int w = (int)Math.Round(Math.Max(0, Math.Min(1, Value)) * Width);
            if (w > 0)
                using (SolidBrush b = new SolidBrush(Fill))
                    g.FillRectangle(b, 0, 0, w, Height);
        }
    }


    // Sammenligner to rader i én kolonne. Prover dato, saa tall med enhet,
    // og faller tilbake paa tekst.
    class ListSorter : System.Collections.IComparer
    {
        public int Column;
        public bool Descending;

        public int Compare(object a, object b)
        {
            string x = Cell((ListViewItem)a, Column);
            string y = Cell((ListViewItem)b, Column);
            int r;

            DateTime da, db;
            if (DateTime.TryParse(x, out da) && DateTime.TryParse(y, out db))
                r = da.CompareTo(db);
            else
            {
                double na, nb;
                bool ga = Number(x, out na), gb = Number(y, out nb);
                if (ga && gb) r = na.CompareTo(nb);
                else if (ga) r = -1;          // tall foer tekst
                else if (gb) r = 1;
                else r = string.Compare(x, y, StringComparison.CurrentCultureIgnoreCase);
            }
            return Descending ? -r : r;
        }

        static string Cell(ListViewItem it, int col)
        {
            if (it == null) return "";
            if (col < 0 || col >= it.SubItems.Count) return "";
            return it.SubItems[col].Text ?? "";
        }

        // Leser et tall fra starten av teksten og ganger opp med enheten, saa
        // 1,9 TB sorteres over 900 GB.
        static bool Number(string s, out double v)
        {
            v = 0;
            if (string.IsNullOrEmpty(s)) return false;
            string t = s.Trim().TrimStart('\u25cf', ' ');

            int i = 0;
            bool sett = false;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (i < t.Length && (t[i] == '-' || t[i] == '+')) { sb.Append(t[i]); i++; }
            while (i < t.Length && (char.IsDigit(t[i]) || t[i] == ',' || t[i] == '.'))
            {
                if (char.IsDigit(t[i])) sett = true;
                sb.Append(t[i] == '.' ? ',' : t[i]);
                i++;
            }
            if (!sett) return false;

            double tall;
            if (!double.TryParse(sb.ToString().Replace(",", System.Globalization.CultureInfo.CurrentCulture
                    .NumberFormat.NumberDecimalSeparator), out tall))
                return false;

            string rest = t.Substring(i).TrimStart().ToUpperInvariant();
            double faktor = 1;
            if (rest.StartsWith("KB")) faktor = 1024;
            else if (rest.StartsWith("MB")) faktor = 1024 * 1024;
            else if (rest.StartsWith("GB")) faktor = 1024d * 1024 * 1024;
            else if (rest.StartsWith("TB")) faktor = 1024d * 1024 * 1024 * 1024;
            else if (rest.StartsWith("MS")) faktor = 0.001;
            else if (rest.StartsWith("S")) faktor = 1;

            v = tall * faktor;
            return true;
        }
    }
}
