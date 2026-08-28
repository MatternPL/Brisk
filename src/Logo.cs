using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Brisk
{
    // Merket til Brisk. Tre feiestrøk i bevegelse pluss en glans —
    // leselig helt ned til 16 piksler, der en detaljert kost bare blir grøt.
    public static class Logo
    {
        public static readonly Color Deep = Color.FromArgb(0x16, 0x20, 0x38);
        public static readonly Color Deep2 = Color.FromArgb(0x0E, 0x11, 0x18);
        public static readonly Color Blue = Color.FromArgb(0x6F, 0xAB, 0xFF);
        public static readonly Color Blue2 = Color.FromArgb(0x4E, 0x86, 0xEE);

        // Tegner merket i en kvadratisk boks med venstre-topp i (x, y).
        public static void Paint(Graphics g, float x, float y, float s, bool backdrop)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (backdrop)
            {
                using (GraphicsPath bg = Rounded(new RectangleF(x, y, s, s), s * (s < 28f ? 0.17f : 0.22f)))
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new RectangleF(x - 1, y - 1, s + 2, s + 2), Deep, Deep2, 55f))
                    g.FillPath(b, bg);
            }

            // Tre strøk som feier nedover mot venstre, avtagende i lengde.
            // Under 24 px blir tre tynne strøk til grøt — da tegner vi to tykke i stedet.
            bool tiny = s < 28f;
            float[][] strokes = tiny
                ? new float[][]
                {
                    new float[] { 0.80f, 0.17f, 0.27f, 0.66f, 0.185f, 1.00f },
                    new float[] { 0.86f, 0.56f, 0.55f, 0.87f, 0.150f, 0.82f },
                }
                : new float[][]
                {
                    new float[] { 0.79f, 0.18f, 0.30f, 0.63f, 0.125f, 1.00f },
                    new float[] { 0.84f, 0.43f, 0.42f, 0.77f, 0.100f, 0.86f },
                    new float[] { 0.88f, 0.66f, 0.60f, 0.87f, 0.078f, 0.70f },
                };

            foreach (float[] k in strokes)
            {
                using (LinearGradientBrush lb = new LinearGradientBrush(
                    new PointF(x + s * k[0], y + s * k[1]),
                    new PointF(x + s * k[2], y + s * k[3]),
                    Mix(Blue, k[5]), Mix(Blue2, k[5])))
                using (Pen pen = new Pen(lb, s * k[4]))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, x + s * k[0], y + s * k[1], x + s * k[2], y + s * k[3]);
                }
            }

            // Glans øverst til venstre — gir merket retning.
            using (SolidBrush b = new SolidBrush(Color.FromArgb(245, 0xEC, 0xF3, 0xFF)))
                Spark(g, b, x + s * (tiny ? 0.235f : 0.26f), y + s * (tiny ? 0.235f : 0.25f),
                      s * (tiny ? 0.135f : 0.115f));
            if (!tiny)
                using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 0xEC, 0xF3, 0xFF)))
                    Spark(g, b, x + s * 0.45f, y + s * 0.15f, s * 0.055f);

            g.SmoothingMode = old;
        }

        static Color Mix(Color c, float alpha)
        {
            int a = (int)Math.Round(255 * Math.Max(0f, Math.Min(1f, alpha)));
            return Color.FromArgb(a, c.R, c.G, c.B);
        }

        static void Spark(Graphics g, Brush b, float cx, float cy, float r)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                float k = r * 0.30f;
                p.AddPolygon(new PointF[] {
                    new PointF(cx, cy - r),
                    new PointF(cx + k, cy - k),
                    new PointF(cx + r, cy),
                    new PointF(cx + k, cy + k),
                    new PointF(cx, cy + r),
                    new PointF(cx - k, cy + k),
                    new PointF(cx - r, cy),
                    new PointF(cx - k, cy - k)
                });
                g.FillPath(b, p);
            }
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
