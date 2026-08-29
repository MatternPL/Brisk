using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Brisk
{
    // Synlig fanerad. Erstatter nedtrekkslistene, som skjulte at valgene fantes.
    class SegmentBar : Control
    {
        readonly List<string> items = new List<string>();
        int active;
        int hot = -1;
        public event EventHandler Changed;

        public SegmentBar()
        {
            Height = 38;
            Dock = DockStyle.Top;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            MouseLeave += delegate { hot = -1; Invalidate(); };
            MouseMove += delegate(object s, MouseEventArgs e)
            {
                int i = HitTest(e.X);
                if (i != hot) { hot = i; Invalidate(); }
            };
            MouseDown += delegate(object s, MouseEventArgs e)
            {
                int i = HitTest(e.X);
                if (i < 0 || i == active) return;
                active = i;
                Invalidate();
                EventHandler h = Changed;
                if (h != null) h(this, EventArgs.Empty);
            };
        }

        public void Add(string text) { items.Add(text); Invalidate(); }
        public int Index { get { return active; } }
        public string Value { get { return active < items.Count ? items[active] : ""; } }

        public void Select(int i)
        {
            if (i < 0 || i >= items.Count || i == active) return;
            active = i;
            Invalidate();
            EventHandler h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }

        int SegWidth
        {
            get { return items.Count == 0 ? 0 : Math.Min(220, Math.Max(120, Width / items.Count)); }
        }

        int HitTest(int x)
        {
            int w = SegWidth;
            if (w == 0) return -1;
            int i = x / w;
            return i >= 0 && i < items.Count ? i : -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg);
            int w = SegWidth;

            using (Pen line = new Pen(Theme.Line))
                g.DrawLine(line, 0, Height - 1, Width, Height - 1);

            for (int i = 0; i < items.Count; i++)
            {
                Rectangle r = new Rectangle(i * w, 0, w, Height - 1);
                bool on = i == active;

                if (on)
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(0x1C, 0x20, 0x2A)))
                        g.FillRectangle(b, r);
                else if (i == hot)
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(0x18, 0x1B, 0x22)))
                        g.FillRectangle(b, r);

                TextRenderer.DrawText(g, items[i], on ? Theme.FBold : Theme.F, r,
                    on ? Theme.Text : Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis);

                if (on)
                    using (SolidBrush b = new SolidBrush(Theme.Accent))
                        g.FillRectangle(b, r.X, Height - 3, r.Width, 3);
            }
        }
    }

    // Handlingsflis: tittel og én linje som forklarer hva som skjer.
    // Erstatter knapper med skjult hjelpetekst.
    class ActionTile : Control
    {
        public string Title = "";
        public string Info = "";
        public Color Accent = Theme.Accent;
        public bool Primary;
        bool over, down;

        public ActionTile(string title, string info)
        {
            Title = title;
            Info = info;
            Height = 86;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            MouseEnter += delegate { over = true; Invalidate(); };
            MouseLeave += delegate { over = false; down = false; Invalidate(); };
            MouseDown += delegate { down = true; Invalidate(); };
            MouseUp += delegate { down = false; Invalidate(); };
            EnabledChanged += delegate { Invalidate(); };
        }

        public ActionTile AsPrimary() { Primary = true; return this; }
        public ActionTile AsDanger() { Accent = Theme.Bad; return this; }
        public ActionTile AsWarn() { Accent = Theme.Warn; return this; }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);

            Color face = !Enabled ? Color.FromArgb(0x18, 0x1A, 0x20)
                       : down ? Color.FromArgb(0x26, 0x2C, 0x38)
                       : over ? Color.FromArgb(0x22, 0x27, 0x31)
                       : Theme.Card;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (SolidBrush b = new SolidBrush(face)) g.FillRectangle(b, r);
            using (Pen p = new Pen(over && Enabled ? Accent : Theme.Line)) g.DrawRectangle(p, r);

            // Fargestripe til venstre viser hva slags handling dette er.
            using (SolidBrush b = new SolidBrush(Enabled ? Accent : Theme.Line))
                g.FillRectangle(b, 0, 0, Primary ? 4 : 3, Height - 1);

            Color title = !Enabled ? Theme.Muted : Primary ? Accent : Theme.Text;
            TextRenderer.DrawText(g, Title, Theme.FBold,
                new Rectangle(16, 10, Width - 26, 20), title,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(g, Info, Theme.FSmall,
                new Rectangle(16, 32, Width - 28, Height - 40), Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
        }
    }

    // Melding midt i en tom liste, med hva du gjør videre.
    class EmptyState : Label
    {
        public EmptyState(string text)
        {
            Text = text;
            Dock = DockStyle.Fill;
            TextAlign = ContentAlignment.MiddleCenter;
            ForeColor = Theme.Muted;
            Font = Theme.F;
            BackColor = Theme.Card;
        }
    }

    // Firkantflis for Verktoy-sida. Navn, en kort linje om hva det gjor, og
    // hvem som har laget det. Klikk velger; Kjor-knappen ligger under lista.
    class ToolTile : Control
    {
        public readonly ExternalTool Tool;
        bool over, picked;

        public ToolTile(ExternalTool t)
        {
            Tool = t;
            Size = new Size(196, 150);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            MouseEnter += delegate { over = true; Invalidate(); };
            MouseLeave += delegate { over = false; Invalidate(); };
        }

        public bool Picked
        {
            get { return picked; }
            set { if (picked != value) { picked = value; Invalidate(); } }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);

            Color face = picked ? Color.FromArgb(0x22, 0x2A, 0x3A)
                       : over ? Color.FromArgb(0x1E, 0x22, 0x2B)
                       : Theme.Card;
            Color edge = picked ? Theme.Accent : Theme.Line;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (SolidBrush b = new SolidBrush(face)) g.FillRectangle(b, r);
            using (Pen p = new Pen(edge, picked ? 2f : 1f))
                g.DrawRectangle(p, picked ? 1 : 0, picked ? 1 : 0,
                                Width - (picked ? 3 : 1), Height - (picked ? 3 : 1));

            // Stripe oppe viser om kommandoen henter kode fra nettet.
            using (SolidBrush b = new SolidBrush(Tool.Remote ? Theme.Warn : Theme.Accent))
                g.FillRectangle(b, 0, 0, Width - 1, 3);

            TextRenderer.DrawText(g, Tool.Name, Theme.FCard,
                new Rectangle(14, 18, Width - 24, 24),
                picked ? Theme.Text : Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(g, L.T(Tool.What), Theme.FSmall,
                new Rectangle(14, 48, Width - 26, 58), Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(g, Tool.By, Theme.FSmall,
                new Rectangle(14, Height - 30, Width - 26, 18),
                Color.FromArgb(0x6A, 0x72, 0x80),
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    public static class Widgets
    {
        // Legger handlingsfliser i en rad som deler bredden likt.
        public static Panel Row(int height, params Control[] tiles)
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Top;
            p.Height = height;
            p.BackColor = Theme.Bg;
            p.Padding = new Padding(0, 0, 0, 12);

            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.BackColor = Theme.Bg;
            t.ColumnCount = tiles.Length;
            t.RowCount = 1;
            for (int i = 0; i < tiles.Length; i++)
                t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / tiles.Length));

            for (int i = 0; i < tiles.Length; i++)
            {
                Panel wrap = new Panel();
                wrap.Dock = DockStyle.Fill;
                wrap.BackColor = Theme.Bg;
                wrap.Padding = new Padding(0, 0, i == tiles.Length - 1 ? 0 : 12, 0);
                tiles[i].Dock = DockStyle.Fill;
                wrap.Controls.Add(tiles[i]);
                t.Controls.Add(wrap, i, 0);
            }
            p.Controls.Add(t);
            return p;
        }

        // Overskrift over en liste, med valgfri teller til høyre.
        public static Panel Head(string text, out Label counter)
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Top;
            p.Height = 28;
            p.BackColor = Theme.Bg;

            Label l = Theme.Lbl(text, Theme.FBold, Theme.Text);
            l.Location = new Point(0, 4);

            counter = Theme.Lbl("", Theme.FSmall, Theme.Muted);
            counter.AutoSize = false;
            counter.TextAlign = ContentAlignment.MiddleRight;
            counter.Dock = DockStyle.Right;
            counter.Width = 340;

            p.Controls.Add(counter);
            p.Controls.Add(l);
            return p;
        }
    }
}
