using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Brisk
{
    // ------------------------------------------------------------------
    //  Egen tittellinje
    //
    //  Windows sin egen linje er borte (FormBorderStyle.None), og denne
    //  tegnes i stedet. Grunnen er ikke pynt alene: den systemtegnede linja
    //  folger ikke temaet vaart, den er graa i lys modus og har en annen
    //  hoyde enn resten av rammeverket - saa vinduet saa ut som to program
    //  limt sammen.
    //
    //  Alt Windows gjorde gratis maa gjores her: dra for aa flytte, snapping
    //  mot kantene, dobbeltklikk for aa maksimere, og aa dra i kantene for
    //  aa endre storrelse (se WndProc lenger nede).
    // ------------------------------------------------------------------
    public class TitleBar : Panel
    {
        public const int H = 38;

        readonly MainForm form;
        readonly ChromeBtn bMin, bMax, bClose;

        public TitleBar(MainForm f)
        {
            form = f;
            Dock = DockStyle.Top;
            Height = H;
            BackColor = Theme.Side;
            DoubleBuffered = true;

            bClose = new ChromeBtn(ChromeBtn.Kind.Close);
            bMax = new ChromeBtn(ChromeBtn.Kind.Max);
            bMin = new ChromeBtn(ChromeBtn.Kind.Min);
            bClose.Dock = DockStyle.Right;
            bMax.Dock = DockStyle.Right;
            bMin.Dock = DockStyle.Right;

            bMin.Click += delegate { form.WindowState = FormWindowState.Minimized; };
            bMax.Click += delegate { ToggleMax(); };
            bClose.Click += delegate { form.Close(); };

            Controls.Add(bMin);
            Controls.Add(bMax);
            Controls.Add(bClose);

            // Ingen logo eller navn her: sidemenyen har begge deler rett under,
            // og to like merker oppa hverandre ser ut som en glipp. Linja er
            // bare noe aa dra i og et sted knappene kan bo.
            MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                if (e.Clicks == 2) { ToggleMax(); return; }
                Dra();
            };
        }

        void ToggleMax()
        {
            if (form.WindowState == FormWindowState.Maximized)
                form.WindowState = FormWindowState.Normal;
            else
            {
                // Uten dette dekker et rammelost vindu ogsaa oppgavelinja.
                form.SetMaximizedArea();
                form.WindowState = FormWindowState.Maximized;
            }
            bMax.Restore = form.WindowState == FormWindowState.Maximized;
            bMax.Invalidate();
        }

        // Lar Windows overta dragingen. Da faar vi snapping mot skjermkantene
        // og alt annet brukeren forventer, gratis.
        void Dra()
        {
            try
            {
                ReleaseCapture();
                SendMessage(form.Handle, 0xA1 /* WM_NCLBUTTONDOWN */,
                    (IntPtr)2 /* HTCAPTION */, IntPtr.Zero);
            }
            catch (Exception) { }
        }

        public void SyncMax()
        {
            bMax.Restore = form.WindowState == FormWindowState.Maximized;
            bMax.Invalidate();
        }

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
    }

    // Minimer / maksimer / lukk. Tegnes for hand slik at de folger temaet og
    // har samme hoyde som linja de sitter i.
    public class ChromeBtn : Control
    {
        public enum Kind { Min, Max, Close }

        readonly Kind kind;
        bool hover;

        public bool Restore;   // maksknappen bytter figur naar vinduet er maksimert

        public ChromeBtn(Kind k)
        {
            kind = k;
            Width = 46;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            MouseEnter += delegate { hover = true; Invalidate(); };
            MouseLeave += delegate { hover = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color bg = !hover ? Theme.Side
                     : kind == Kind.Close ? Color.FromArgb(0xC4, 0x2B, 0x1C)
                     : Theme.CardHi;
            using (SolidBrush b = new SolidBrush(bg))
                g.FillRectangle(b, ClientRectangle);

            Color fg = hover && kind == Kind.Close ? Color.White : Theme.Text;
            int cx = Width / 2, cy = Height / 2;

            using (Pen p = new Pen(fg, 1f))
            {
                g.SmoothingMode = SmoothingMode.None;
                if (kind == Kind.Min)
                    g.DrawLine(p, cx - 5, cy, cx + 5, cy);
                else if (kind == Kind.Close)
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawLine(p, cx - 5, cy - 5, cx + 5, cy + 5);
                    g.DrawLine(p, cx - 5, cy + 5, cx + 5, cy - 5);
                }
                else if (Restore)
                {
                    // To ark oppa hverandre - samme figur Windows selv bruker.
                    g.DrawRectangle(p, cx - 5, cy - 2, 7, 7);
                    g.DrawLine(p, cx - 2, cy - 5, cx + 5, cy - 5);
                    g.DrawLine(p, cx + 5, cy - 5, cx + 5, cy + 2);
                }
                else
                    g.DrawRectangle(p, cx - 5, cy - 5, 10, 10);
            }
        }
    }

    public partial class MainForm
    {
        TitleBar chrome;

        // Windows tegner ingen ramme lenger, saa kantene maa meldes inn selv.
        // Uten dette gaar det ikke an aa dra i hjornene for aa endre storrelse.
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTCLIENT = 1;
            const int kant = 6;

            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                if (m.Result.ToInt32() == HTCLIENT)
                {
                    long lp = m.LParam.ToInt64();
                    Point p = PointToClient(new Point((short)(lp & 0xFFFF),
                                                      (short)((lp >> 16) & 0xFFFF)));
                    bool v = p.X <= kant, h = p.X >= ClientSize.Width - kant;
                    bool o = p.Y <= kant, n = p.Y >= ClientSize.Height - kant;

                    int treff = o && v ? 13 : o && h ? 14 : n && v ? 16 : n && h ? 17
                              : v ? 10 : h ? 11 : o ? 12 : n ? 15 : 0;
                    if (treff != 0) m.Result = (IntPtr)treff;
                }
                return;
            }

            base.WndProc(ref m);
        }

        // Maksimeres vinduet med Win+Pil eller ved aa snappe det mot toppen,
        // gaar det utenom knappen vaar - da maa figuren rettes opp her.
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (chrome == null) return;
            if (WindowState == FormWindowState.Maximized) SetMaximizedArea();
            chrome.SyncMax();
        }

        // Uten Windows-ramma faar vinduet heller ingen skygge, og da flyter
        // det ut i bakgrunnen paa et morkt skrivebord. CS_DROPSHADOW gir den
        // tilbake uten aa tegne noen ramme selv.
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000;   // CS_DROPSHADOW
                return cp;
            }
        }

        // MaximizedBounds er beskyttet, saa tittellinja kan ikke sette den
        // selv. Sammenligningen sparer en runde med unodig ny utlegging.
        public void SetMaximizedArea()
        {
            Rectangle r = Screen.FromControl(this).WorkingArea;
            if (MaximizedBounds != r) MaximizedBounds = r;
        }
    }
}
