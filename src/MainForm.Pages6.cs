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
            ((TableLayoutPanel)gmList).ColumnCount = 3;
            ((TableLayoutPanel)gmList).RowCount = 2;
            for (int i = 0; i < 3; i++)
                ((TableLayoutPanel)gmList).ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));
            for (int i = 0; i < 2; i++)
                ((TableLayoutPanel)gmList).RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

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
                gmList.Controls.Add(GameCard(g), n % 3, n / 3);
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

            foreach (GameSetting g2 in alle)
                if (g2.PendingReboot) gmNeedsReboot = true;
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
            name.Location = new Point(20, 12);
            name.AutoSize = false;
            name.Height = 22;
            name.AutoEllipsis = true;

            string merke = !g.Available ? L.T("Ikke tilgjengelig")
                         : g.PendingReboot ? L.T(g.State)
                         : g.Optimal ? L.T("Alt i orden") + "  ·  " + L.T(g.State)
                         : L.T(g.State);
            Label state = Theme.Lbl(merke, Theme.FSmall,
                !g.Available ? Theme.Muted
                : g.PendingReboot ? Theme.Accent
                : g.Optimal ? Theme.Good : Theme.Warn);
            state.Location = new Point(20, 36);
            state.AutoSize = false;
            state.Height = 18;
            state.AutoEllipsis = true;

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
            else if (g.PendingReboot) { act.Text = L.T("Sett tilbake"); }
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

            // Alt stables loddrett, i stedet for at knapp og tekst kjemper om
            // den samme plassen nederst. Ingenting overlapper da, uansett hvor
            // hoyt kortet blir.
            Theme.Arrange(card, delegate
            {
                int bredde = Math.Max(140, card.Width - 40);

                name.Width = bredde;
                state.Width = bredde;

                gain.Location = new Point(20, 58);
                gain.Width = bredde;
                gain.TextAlign = ContentAlignment.MiddleLeft;

                gainSub.Location = new Point(20, 88);
                gainSub.Width = bredde;
                gainSub.TextAlign = ContentAlignment.MiddleLeft;

                what.Location = new Point(20, 112);
                what.Width = bredde;
                what.Height = 46;

                cost.Location = new Point(20, 160);
                cost.Width = bredde;
                cost.Height = 44;

                act.Location = new Point(20, card.Height - act.Height - 14);
                act.Width = bredde;
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
