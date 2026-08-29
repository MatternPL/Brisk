using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Brisk
{
    // Merket til Brisk: en måler slått ut til full. Tegnet som vektor slik at
    // det blir like skarpt på 16 piksler i tittellinja som på 256 i Utforsker.
    public static class Logo
    {
        public static readonly Color Deep = Color.FromArgb(0x1B, 0x24, 0x42);
        public static readonly Color Deep2 = Color.FromArgb(0x0B, 0x0E, 0x15);
        public static readonly Color Blue = Color.FromArgb(0x6F, 0xBA, 0xFF);
        public static readonly Color Blue2 = Color.FromArgb(0x1E, 0x33, 0xA6);
        public static readonly Color Pivot = Color.FromArgb(0xF4, 0xF8, 0xFF);

        public static void Paint(Graphics g, float x, float y, float s, bool backdrop)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Under 24 px blir tynne detaljer til grøt. Da tegnes en tykkere,
            // enklere utgave av samme motiv.
            bool tiny = s < 24f;

            if (backdrop)
            {
                using (GraphicsPath bg = Rounded(new RectangleF(x, y, s, s), s * (tiny ? 0.20f : 0.25f)))
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new RectangleF(x - 1, y - 1, s + 2, s + 2), Deep, Deep2, 65f))
                    g.FillPath(b, bg);
            }

            // Måleren: en bue som er åpen nedover, med lys som vokser mot høyre.
            float inset = s * (tiny ? 0.30f : 0.285f);
            RectangleF arc = new RectangleF(x + inset, y + inset, s - inset * 2, s - inset * 2);
            float thick = s * (tiny ? 0.150f : 0.078f);

            using (LinearGradientBrush lb = new LinearGradientBrush(
                new PointF(x, y + s * 0.78f), new PointF(x + s, y + s * 0.16f), Blue2, Blue))
            using (Pen p = new Pen(lb, thick))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawArc(p, arc, 148f, 244f);
            }

            // Viseren peker på enden av buen — full utslag.
            float cx = x + s / 2f, cy = y + s / 2f;
            float reach = (s / 2f) - inset - thick * 0.20f;
            using (Pen p = new Pen(Pivot, s * (tiny ? 0.105f : 0.055f)))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLine(p, cx, cy, cx + reach, cy + s * 0.030f);
            }

            // Navet som viseren dreier om.
            float pr = s * (tiny ? 0.090f : 0.078f);
            using (SolidBrush b = new SolidBrush(Pivot))
                g.FillEllipse(b, cx - pr, cy - pr, pr * 2, pr * 2);

            g.SmoothingMode = old;
        }

        public static GraphicsPath Rounded(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Bitmap Bitmap(int size, bool backdrop)
        {
            Bitmap b = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.Transparent);
                Paint(g, 0, 0, size, backdrop);
            }
            return b;
        }
    }
}
