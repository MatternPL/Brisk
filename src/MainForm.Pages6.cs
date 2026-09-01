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

            FlatBtn refresh = new FlatBtn(L.T("Les på nytt")).AsIcon("oppfrisk");
            Tip(refresh, "Les på nytt");
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
            // Radene settes i LoadGame, siden antallet kort avhenger av om
            // maskinen har et NVIDIA-kort eller ikke.
            gmList = new TableLayoutPanel();
            gmList.Dock = DockStyle.Fill;
            gmList.BackColor = Theme.Bg;
            gmList.ColumnCount = 3;
            for (int i = 0; i < 3; i++)
                gmList.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));

            // --- fotnote ---
            Panel foot = new Panel();
            foot.Dock = DockStyle.Bottom;
            foot.Height = 46;
            foot.BackColor = Theme.Bg;
            Label fl = Theme.Lbl(
                L.T("Bare ting som lar seg måle i bilder per sekund. Triks som «unparking» av kjerner og SystemResponsiveness gjør ingenting, og står derfor ikke her."),
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

            int rader = (alle.Count + 2) / 3;
            gmList.RowStyles.Clear();
            gmList.RowCount = Math.Max(1, rader);
            for (int i = 0; i < gmList.RowCount; i++)
                gmList.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / gmList.RowCount));

            int n = 0;
            foreach (GameSetting g in alle)
            {
                gmList.Controls.Add(GameCard(g, n % 3 == 2), n % 3, n / 3);
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
                gmSub.Text = L.T("Se hva hver enkelt koster før du endrer den. To av dem senker sikkerheten.");
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

        Panel GameCard(GameSetting g, bool sisteSpalte)
        {
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

            // Én ting skal svare paa «har jeg gjort dette eller ikke». Foer sto
            // det en tilstandstekst her - «Fine as it is · Off» - som ikke
            // svarte paa spoersmaalet i det hele tatt.
            string merke = !g.Available ? L.T("Ikke tilgjengelig")
                         : g.StuckOn ? L.T("Windows starter den likevel")
                         : g.PendingReboot ? L.T("Optimalisert — krever omstart")
                         : g.Optimal ? L.T("Optimalisert")
                         : L.T("Ikke optimalisert");
            Label state = Theme.Lbl(merke, Theme.FSmall,
                !g.Available ? Theme.Muted
                : g.StuckOn ? Theme.Bad
                : g.PendingReboot ? Theme.Accent
                : g.Optimal ? Theme.Good : Theme.Warn);
            state.Location = new Point(20, 36);
            state.AutoSize = false;
            state.Height = 18;
            state.AutoEllipsis = true;

            // Sitter den fast paa, er det viktigere aa forklare hvorfor enn aa
            // gjenta hva innstillingen er.
            Label what = Theme.Lbl(
                !g.Available ? L.T(g.Unavailable)
                : g.StuckOn ? L.T("Registeret er satt og maskinen er startet på nytt, men hypervisoren starter likevel.")
                : L.T(g.What), Theme.FSmall, Theme.Muted);
            what.AutoSize = false;
            what.Location = new Point(20, 62);
            what.Height = 34;

            Label cost = Theme.Lbl(
                g.Available && g.Cost.Length > 0 ? L.T("Koster:") + " " + L.T(g.Cost) : "",
                Theme.FSmall, Color.FromArgb(0x9A, 0x84, 0x5A));
            cost.AutoSize = false;
            cost.Location = new Point(20, 98);
            cost.Height = 34;

            // To knappetekster for hele sida: den ene gjor det, den andre
            // angrer. «Slå av for spill» og «Sett tilbake» sa ingenting om
            // hvilken vei man var paa vei.
            FlatBtn act = new FlatBtn("");
            act.Width = 128; act.Height = 32;
            act.Font = Theme.FSmall;
            if (!g.Available) { act.Enabled = false; act.Visible = false; }
            else if (g.Optimal || g.PendingReboot)
            {
                // Er appen fjernet, er «Angre» aa hente den igjen. Knappen skal
                // si hvor den sender deg, ikke bare «Angre».
                act.Text = g.Destructive ? L.T("Hent tilbake") : L.T("Angre");
            }
            else { act.Text = L.T("Optimaliser"); act.Primary(); }
            Tip(act, g.Optimal || g.PendingReboot
                ? (g.Destructive ? "Åpner Microsoft Store, der du kan installere den igjen."
                                 : "Setter innstillingen tilbake slik Windows hadde den.")
                : "Endrer innstillingen slik spill liker den.");

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
                if (forGaming && (g.Gain == Gain.Stor || g.Destructive) && g.Cost.Length > 0)
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
            // gainSub sto her: «Noticeable effect», «Small, but measurable».
            // Prosenttallet rett over sier det samme, og de 28 pikslene
            // trengs til teksten som faktisk forklarer noe.

            // Alt stables loddrett, i stedet for at knapp og tekst kjemper om
            // den samme plassen nederst. Ingenting overlapper da, uansett hvor
            // hoyt kortet blir.
            Theme.Arrange(card, delegate
            {
                int bredde = Math.Max(140, card.Width - 40);

                name.Width = bredde;
                state.Width = bredde;

                // Knappen staar paa samme linje som prosenttallet. Stablet
                // under teksten trengte kortet 250 piksler, og med ni kort i
                // tre rader er det bare 180 aa gaa paa - da havnet knappen
                // under kanten.
                act.Width = 128;
                act.Height = 32;
                act.Location = new Point(Math.Max(150, card.Width - act.Width - 20), 56);

                gain.Location = new Point(20, 54);
                gain.Width = Math.Max(80, act.Left - 30);
                gain.TextAlign = ContentAlignment.MiddleLeft;

                int topp = 92;
                int ledig = Math.Max(44, card.Height - topp - 14);
                int hWhat = (int)(ledig * 0.52);

                what.Location = new Point(20, topp);
                what.Width = bredde;
                what.Height = hWhat;

                cost.Location = new Point(20, topp + hWhat + 4);
                cost.Width = bredde;
                cost.Height = Math.Max(16, ledig - hWhat - 4);
            });

            return Widgets.Cell(card, sisteSpalte);
        }

        static string GainText(Gain g)
        {
            if (g == Gain.Stor) return L.T("Merkbar effekt");
            if (g == Gain.Liten) return L.T("Liten, men målbar");
            return L.T("Varierer mellom spill");
        }
    }
}
