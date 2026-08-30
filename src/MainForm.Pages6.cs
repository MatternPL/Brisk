using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Brisk
{
    public partial class MainForm
    {
        // ==============================================================
        //  SPILLMODUS
        // ==============================================================
        // Viser hva som staar i veien for bilder per sekund, hva hver ting
        // gir, og hva den koster. Ingen samleknapp som gjor alt paa en gang -
        // to av postene senker sikkerheten reelt, og de skal velges enkeltvis.
        Label gmVerdict, gmSub, gmReboot;
        TableLayoutPanel gmList;
        bool gmNeedsReboot;

        Panel PageGame()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            // --- dommen oeverst ---
            Panel head = new Panel();
            head.Dock = DockStyle.Top;
            head.Height = 116;
            head.BackColor = Theme.Bg;
            head.Padding = new Padding(0, 0, 0, 16);

            Panel card = Theme.MakeCard();
            card.Dock = DockStyle.Fill;
            card.Paint += delegate(object s, PaintEventArgs e)
            {
                Icons.Draw(e.Graphics, "fart", new RectangleF(24, 30, 28, 28), Theme.Accent);
            };

            gmVerdict = Theme.Lbl("", new Font("Segoe UI Light", 20f), Theme.Text);
            gmVerdict.Location = new Point(68, 22);
            gmSub = Theme.Lbl("", Theme.FSmall, Theme.Muted);
            gmSub.Location = new Point(70, 56);

            FlatBtn refresh = new FlatBtn(L.T("Les på nytt"));
            refresh.Width = 130; refresh.Height = 38;
            refresh.Click += delegate { LoadGame(); };
            Theme.Arrange(card, delegate
            {
                refresh.Location = new Point(card.Width - refresh.Width - 20, 22);
                gmSub.Width = Math.Max(200, card.Width - 220);
            });
            gmSub.AutoSize = false;
            gmSub.Height = 22;

            card.Controls.Add(gmVerdict);
            card.Controls.Add(gmSub);
            card.Controls.Add(refresh);
            head.Controls.Add(card);

            // --- omstartsvarsel ---
            gmReboot = Theme.Lbl("", Theme.F, Theme.Warn);
            gmReboot.AutoSize = false;
            gmReboot.Dock = DockStyle.Top;
            gmReboot.Height = 0;

            // --- listen ---
            // To spalter, tre rader. Seks kort under hverandre krevde rulling,
            // og en side som viser hva som staar i veien for ytelse boer vises
            // i sin helhet uten at man maa lete.
            gmList = new TableLayoutPanel();
            gmList.Dock = DockStyle.Fill;
            gmList.BackColor = Theme.Bg;
            ((TableLayoutPanel)gmList).ColumnCount = 2;
            ((TableLayoutPanel)gmList).RowCount = 3;
            for (int i = 0; i < 2; i++)
                ((TableLayoutPanel)gmList).ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            for (int i = 0; i < 3; i++)
                ((TableLayoutPanel)gmList).RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3f));

            // --- fotnote ---
            Panel foot = new Panel();
            foot.Dock = DockStyle.Bottom;
            foot.Height = 46;
            foot.BackColor = Theme.Bg;
            Label fl = Theme.Lbl(
                L.T("Her står bare ting som lar seg måle i bilder per sekund. Brisk rører ikke tidsoppløsning, «unparking» av kjerner eller SystemResponsiveness — det er triks uten effekt, og de hører ikke hjemme her."),
                Theme.FSmall, Theme.Muted);
            fl.AutoSize = false;
            fl.Dock = DockStyle.Fill;
            foot.Controls.Add(fl);

            p.Controls.Add(gmList);
            p.Controls.Add(foot);
            p.Controls.Add(gmReboot);
            p.Controls.Add(head);

            Defer(delegate { LoadGame(); });
            return p;
        }

        void LoadGame()
        {
            List<GameSetting> alle = GameTools.Read();

            gmList.Controls.Clear();
            int koster = 0, tilgjengelig = 0;

            int n = 0;
            foreach (GameSetting g in alle)
            {
                gmList.Controls.Add(GameCard(g), n % 2, n / 2);
                n++;
            }

            foreach (GameSetting g in alle)
            {
                if (!g.Available) continue;
                tilgjengelig++;
                if (!g.Optimal) koster++;
            }

            if (koster == 0)
            {
                gmVerdict.Text = L.T("Alt står allerede slik spill liker det");
                gmVerdict.ForeColor = Theme.Good;
                gmSub.Text = L.F("{0} innstillinger kontrollert.", tilgjengelig);
            }
            else
            {
                gmVerdict.Text = koster == 1
                    ? L.T("Én innstilling koster deg bilder")
                    : L.F("{0} innstillinger koster deg bilder", koster);
                gmVerdict.ForeColor = Theme.Warn;
                gmSub.Text = L.T("Les hva hver enkelt koster deg før du slår den av. To av dem senker sikkerheten.");
            }

            ShowReboot();

        }

        void ShowReboot()
        {
            if (!gmNeedsReboot) { gmReboot.Height = 0; gmReboot.Text = ""; return; }
            gmReboot.Text = "   " + L.T("Endringen gjelder først etter omstart.");
            gmReboot.Height = 30;
        }

        Panel GameCard(GameSetting g)
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Theme.Bg;
            host.Padding = new Padding(0, 0, 12, 12);

            Panel card = Theme.MakeCard();
            card.Dock = DockStyle.Fill;

            Color stripe = !g.Available ? Theme.Line
                         : g.Optimal ? Theme.Good
                         : g.Gain == Gain.Stor ? Theme.Warn : Theme.Accent;
            card.Paint += delegate(object s, PaintEventArgs e)
            {
                using (SolidBrush b = new SolidBrush(stripe))
                    e.Graphics.FillRectangle(b, 0, 0, 3, card.Height);
            };

            Label name = Theme.Lbl(L.T(g.Name), Theme.FCard, Theme.Text);
            name.Location = new Point(20, 14);

            string merke = !g.Available ? L.T("Ikke tilgjengelig")
                         : g.Optimal ? L.T("Alt i orden") + "  ·  " + L.T(g.State)
                         : L.T(g.State);
            Label state = Theme.Lbl(merke, Theme.FSmall,
                !g.Available ? Theme.Muted : g.Optimal ? Theme.Good : Theme.Warn);
            state.Location = new Point(22, 40);

            Label what = Theme.Lbl(!g.Available ? L.T(g.Unavailable) : L.T(g.What), Theme.FSmall, Theme.Muted);
            what.AutoSize = false;
            what.Location = new Point(20, 62);
            what.Height = 34;

            Label cost = Theme.Lbl(
                g.Available && g.Cost.Length > 0 ? L.T("Koster deg:") + " " + L.T(g.Cost) : "",
                Theme.FSmall, Color.FromArgb(0x9A, 0x84, 0x5A));
            cost.AutoSize = false;
            cost.Location = new Point(20, 98);
            cost.Height = 34;

            FlatBtn act = new FlatBtn("");
            act.Width = 170; act.Height = 34;
            if (!g.Available) { act.Enabled = false; act.Visible = false; }
            else if (g.Optimal) { act.Text = L.T("Sett tilbake"); }
            else { act.Text = L.T("Slå av for spill"); act.Primary(); }

            Label gain = Theme.Lbl(
                g.Available && g.Estimate.Length > 0 ? g.Estimate : "",
                new Font("Segoe UI Light", 20f),
                g.Gain == Gain.Stor ? Theme.Accent : Theme.Muted);
            gain.AutoSize = false;
            gain.TextAlign = ContentAlignment.MiddleRight;
            gain.Width = 190; gain.Height = 30;

            Label gainSub = Theme.Lbl(g.Available ? GainText(g.Gain) : "", Theme.FSmall, Theme.Muted);
            gainSub.AutoSize = false;
            gainSub.TextAlign = ContentAlignment.MiddleRight;
            gainSub.Width = 190; gainSub.Height = 16;

            act.Click += async delegate
            {
                bool forGaming = !g.Optimal;
                if (forGaming && g.Gain == Gain.Stor && g.Cost.Length > 0)
                {
                    if (MessageBox.Show(this,
                            L.T(g.Name) + "\r\n\r\n" + L.T(g.Cost) + "\r\n\r\n" + L.T("Fortsette?"),
                            L.T("Spillmodus"), MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning) != DialogResult.Yes) return;
                }
                if (g.NeedsAdmin && !Util.IsAdmin())
                {
                    Status(L.T("Denne krever administrator. Start Brisk som administrator."));
                    return;
                }

                act.Enabled = false;
                string feil = null;
                await System.Threading.Tasks.Task.Run(delegate
                {
                    feil = GameTools.Apply(g.Key, forGaming);
                });
                act.Enabled = true;

                if (feil != null) { Status(L.T("Klarte ikke endre: ") + feil); return; }
                if (g.NeedsReboot) gmNeedsReboot = true;
                Util.Log("Spillmodus: " + g.Key + " -> " + (forGaming ? "spill" : "standard"));
                Status("");
                LoadGame();
            };

            card.Controls.Add(name);
            card.Controls.Add(state);
            card.Controls.Add(what);
            card.Controls.Add(cost);
            card.Controls.Add(act);
            card.Controls.Add(gain);
            card.Controls.Add(gainSub);

            Theme.Arrange(card, delegate
            {
                int right = card.Width - 20;

                gain.Location = new Point(right - gain.Width, 12);
                gainSub.Location = new Point(right - gainSub.Width, 44);

                // Knappen nederst til hoyre. Kostnadsteksten stopper for den,
                // ellers legger de seg oppaa hverandre.
                act.Location = new Point(right - act.Width, card.Height - act.Height - 14);

                what.Location = new Point(20, 66);
                what.Width = Math.Max(160, card.Width - 40);
                what.Height = 36;

                cost.Location = new Point(20, card.Height - 44);
                cost.Width = Math.Max(120, act.Left - 32);
                cost.Height = 34;
            });

            host.Controls.Add(card);
            return host;
        }

        static string GainText(Gain g)
        {
            if (g == Gain.Stor) return L.T("Merkbar effekt");
            if (g == Gain.Liten) return L.T("Liten, men målbar");
            return L.T("Varierer mellom spill");
        }
    }
}
