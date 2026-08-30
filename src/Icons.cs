using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Brisk
{
    // Enkle strektegninger til sidemenyen. Holdt bevisst grove — de skal leses
    // på 18 piksler, ikke beundres.
    public static class Icons
    {
        public static void Draw(Graphics g, string key, RectangleF r, Color c)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float w = Math.Max(1.6f, r.Width / 9f);

            using (Pen p = new Pen(c, w))
            using (SolidBrush b = new SolidBrush(c))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;

                float x = r.X, y = r.Y, s = r.Width;

                switch (key)
                {
                    case "oversikt":            // fire ruter
                        {
                            float d = s * 0.40f, gap = s * 0.18f;
                            g.DrawRectangle(p, x, y, d, d);
                            g.DrawRectangle(p, x + d + gap, y, d, d);
                            g.DrawRectangle(p, x, y + d + gap, d, d);
                            g.DrawRectangle(p, x + d + gap, y + d + gap, d, d);
                            break;
                        }

                    case "rydding":             // feiestrøk
                        g.DrawLine(p, x + s * 0.82f, y + s * 0.12f, x + s * 0.34f, y + s * 0.62f);
                        g.DrawLine(p, x + s * 0.58f, y + s * 0.52f, x + s * 0.14f, y + s * 0.92f);
                        g.DrawLine(p, x + s * 0.86f, y + s * 0.55f, x + s * 0.60f, y + s * 0.86f);
                        break;

                    case "diskplass":           // sektor i en sirkel
                        g.DrawEllipse(p, x + w / 2, y + w / 2, s - w, s - w);
                        g.DrawLine(p, x + s / 2, y + s / 2, x + s / 2, y + w / 2);
                        g.DrawLine(p, x + s / 2, y + s / 2, x + s - w / 2, y + s * 0.66f);
                        break;

                    case "oppstart":            // av/på-symbol
                        g.DrawArc(p, x + w / 2, y + s * 0.16f, s - w, s - w * 1.2f, -60, 300);
                        g.DrawLine(p, x + s / 2, y, x + s / 2, y + s * 0.42f);
                        break;

                    case "minne":               // brikke med bein
                        {
                            float m = s * 0.22f;
                            g.DrawRectangle(p, x + m, y + m, s - m * 2, s - m * 2);
                            for (int i = 0; i < 3; i++)
                            {
                                float t = y + m + (s - m * 2) * (0.22f + i * 0.28f);
                                g.DrawLine(p, x, t, x + m, t);
                                g.DrawLine(p, x + s - m, t, x + s, t);
                            }
                            break;
                        }

                    case "helse":               // pulslinje
                        {
                            PointF[] pts = {
                                new PointF(x, y + s * 0.55f),
                                new PointF(x + s * 0.24f, y + s * 0.55f),
                                new PointF(x + s * 0.36f, y + s * 0.22f),
                                new PointF(x + s * 0.52f, y + s * 0.82f),
                                new PointF(x + s * 0.66f, y + s * 0.55f),
                                new PointF(x + s, y + s * 0.55f)
                            };
                            g.DrawLines(p, pts);
                            break;
                        }

                    case "nettverk":            // wifi-buer
                        g.DrawArc(p, x, y + s * 0.10f, s, s, 200, 140);
                        g.DrawArc(p, x + s * 0.20f, y + s * 0.30f, s * 0.60f, s * 0.60f, 200, 140);
                        g.FillEllipse(b, x + s * 0.42f, y + s * 0.72f, s * 0.16f, s * 0.16f);
                        break;

                    case "drivere":
                    case "oppdateringer":       // pil ned i en boks
                        g.DrawLine(p, x + s / 2, y + s * 0.06f, x + s / 2, y + s * 0.60f);
                        g.DrawLine(p, x + s * 0.28f, y + s * 0.38f, x + s / 2, y + s * 0.62f);
                        g.DrawLine(p, x + s * 0.72f, y + s * 0.38f, x + s / 2, y + s * 0.62f);
                        g.DrawLine(p, x, y + s * 0.88f, x + s, y + s * 0.88f);
                        break;

                    case "programmer":
                    case "programvare":         // eske
                        g.DrawRectangle(p, x, y + s * 0.24f, s, s * 0.66f);
                        g.DrawLine(p, x, y + s * 0.46f, x + s, y + s * 0.46f);
                        g.DrawLine(p, x + s / 2, y + s * 0.24f, x + s / 2, y + s * 0.46f);
                        break;

                    case "vedlikehold":         // skiftenøkkel
                        g.DrawLine(p, x + s * 0.20f, y + s * 0.80f, x + s * 0.62f, y + s * 0.38f);
                        g.DrawArc(p, x + s * 0.48f, y + s * 0.02f, s * 0.50f, s * 0.50f, 120, 260);
                        break;

                    case "verktoy":             // skrunokkel og skrutrekker i kryss
                        g.DrawLine(p, x + s * 0.16f, y + s * 0.84f, x + s * 0.60f, y + s * 0.40f);
                        g.DrawArc(p, x + s * 0.46f, y + s * 0.04f, s * 0.46f, s * 0.46f, 120, 260);
                        g.DrawLine(p, x + s * 0.84f, y + s * 0.84f, x + s * 0.44f, y + s * 0.44f);
                        g.DrawLine(p, x + s * 0.10f, y + s * 0.16f, x + s * 0.30f, y + s * 0.36f);
                        break;

                    case "tilpass":             // skyveknapper
                        for (int i = 0; i < 3; i++)
                        {
                            float t = y + s * (0.18f + i * 0.32f);
                            g.DrawLine(p, x, t, x + s, t);
                            float k = x + s * (i == 1 ? 0.68f : 0.32f);
                            g.FillEllipse(b, k - w, t - w, w * 2, w * 2);
                        }
                        break;

                    case "sok":                 // forstorrelsesglass
                        g.DrawEllipse(p, x + s * 0.08f, y + s * 0.08f, s * 0.56f, s * 0.56f);
                        g.DrawLine(p, x + s * 0.62f, y + s * 0.62f, x + s * 0.94f, y + s * 0.94f);
                        break;

                    case "disk":                // plate med nav
                        g.DrawEllipse(p, x + s * 0.05f, y + s * 0.05f, s * 0.90f, s * 0.90f);
                        g.FillEllipse(b, x + s * 0.40f, y + s * 0.40f, s * 0.20f, s * 0.20f);
                        break;

                    case "temperatur":          // termometer
                        g.DrawLine(p, x + s * 0.50f, y + s * 0.10f, x + s * 0.50f, y + s * 0.62f);
                        g.DrawEllipse(p, x + s * 0.34f, y + s * 0.60f, s * 0.32f, s * 0.32f);
                        g.DrawLine(p, x + s * 0.62f, y + s * 0.24f, x + s * 0.82f, y + s * 0.24f);
                        g.DrawLine(p, x + s * 0.62f, y + s * 0.42f, x + s * 0.82f, y + s * 0.42f);
                        break;

                    case "skjold":              // skjold
                        g.DrawLine(p, x + s * 0.12f, y + s * 0.16f, x + s * 0.50f, y + s * 0.04f);
                        g.DrawLine(p, x + s * 0.50f, y + s * 0.04f, x + s * 0.88f, y + s * 0.16f);
                        g.DrawLine(p, x + s * 0.12f, y + s * 0.16f, x + s * 0.12f, y + s * 0.52f);
                        g.DrawLine(p, x + s * 0.88f, y + s * 0.16f, x + s * 0.88f, y + s * 0.52f);
                        g.DrawArc(p, x + s * 0.12f, y + s * 0.20f, s * 0.76f, s * 0.76f, 20, 140);
                        break;

                    case "usb":                 // usb-plugg
                        g.DrawLine(p, x + s * 0.50f, y + s * 0.96f, x + s * 0.50f, y + s * 0.28f);
                        g.DrawRectangle(p, x + s * 0.32f, y + s * 0.06f, s * 0.36f, s * 0.24f);
                        g.DrawLine(p, x + s * 0.50f, y + s * 0.62f, x + s * 0.22f, y + s * 0.44f);
                        g.FillEllipse(b, x + s * 0.14f, y + s * 0.36f, s * 0.16f, s * 0.16f);
                        g.DrawLine(p, x + s * 0.50f, y + s * 0.50f, x + s * 0.78f, y + s * 0.34f);
                        g.DrawRectangle(p, x + s * 0.70f, y + s * 0.22f, s * 0.16f, s * 0.16f);
                        break;

                    case "nedlasting":          // pil ned mot en strek
                        g.DrawLine(p, x + s * 0.50f, y + s * 0.06f, x + s * 0.50f, y + s * 0.62f);
                        g.DrawLine(p, x + s * 0.26f, y + s * 0.40f, x + s * 0.50f, y + s * 0.64f);
                        g.DrawLine(p, x + s * 0.74f, y + s * 0.40f, x + s * 0.50f, y + s * 0.64f);
                        g.DrawLine(p, x + s * 0.12f, y + s * 0.92f, x + s * 0.88f, y + s * 0.92f);
                        break;

                    case "klokke":              // urskive
                        g.DrawEllipse(p, x + s * 0.05f, y + s * 0.05f, s * 0.90f, s * 0.90f);
                        g.DrawLine(p, x + s * 0.50f, y + s * 0.50f, x + s * 0.50f, y + s * 0.24f);
                        g.DrawLine(p, x + s * 0.50f, y + s * 0.50f, x + s * 0.72f, y + s * 0.62f);
                        break;

                    case "dokument":           // ark med brett
                        g.DrawLine(p, x + s * 0.16f, y + s * 0.04f, x + s * 0.62f, y + s * 0.04f);
                        g.DrawLine(p, x + s * 0.16f, y + s * 0.04f, x + s * 0.16f, y + s * 0.96f);
                        g.DrawLine(p, x + s * 0.16f, y + s * 0.96f, x + s * 0.84f, y + s * 0.96f);
                        g.DrawLine(p, x + s * 0.84f, y + s * 0.96f, x + s * 0.84f, y + s * 0.28f);
                        g.DrawLine(p, x + s * 0.62f, y + s * 0.04f, x + s * 0.84f, y + s * 0.28f);
                        g.DrawLine(p, x + s * 0.34f, y + s * 0.56f, x + s * 0.68f, y + s * 0.56f);
                        g.DrawLine(p, x + s * 0.34f, y + s * 0.74f, x + s * 0.68f, y + s * 0.74f);
                        break;

                    case "spill":               // spillkontroll
                        g.DrawArc(p, x, y + s * 0.24f, s * 0.52f, s * 0.52f, 90, 180);
                        g.DrawArc(p, x + s * 0.48f, y + s * 0.24f, s * 0.52f, s * 0.52f, 270, 180);
                        g.DrawLine(p, x + s * 0.26f, y + s * 0.24f, x + s * 0.74f, y + s * 0.24f);
                        g.DrawLine(p, x + s * 0.26f, y + s * 0.76f, x + s * 0.74f, y + s * 0.76f);
                        g.DrawLine(p, x + s * 0.20f, y + s * 0.50f, x + s * 0.36f, y + s * 0.50f);
                        g.DrawLine(p, x + s * 0.28f, y + s * 0.42f, x + s * 0.28f, y + s * 0.58f);
                        g.FillEllipse(b, x + s * 0.64f, y + s * 0.44f, s * 0.12f, s * 0.12f);
                        break;

                    case "fart":               // fartsstreker
                        g.DrawLine(p, x + s * 0.06f, y + s * 0.26f, x + s * 0.74f, y + s * 0.26f);
                        g.DrawLine(p, x + s * 0.22f, y + s * 0.50f, x + s * 0.94f, y + s * 0.50f);
                        g.DrawLine(p, x + s * 0.06f, y + s * 0.74f, x + s * 0.62f, y + s * 0.74f);
                        break;

                    case "logg":                // linjer
                        for (int i = 0; i < 3; i++)
                        {
                            float t = y + s * (0.20f + i * 0.28f);
                            g.DrawLine(p, x, t, x + s * (i == 1 ? 0.70f : 1f), t);
                        }
                        break;
                }
            }
            g.SmoothingMode = old;
        }
    }
}
