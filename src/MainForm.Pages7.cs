using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Brisk
{
    public partial class MainForm
    {
        // ==============================================================
        //  SKJERM
        // ==============================================================
        // To ting som begge merkes med en gang: at skjermen faktisk kjorer
        // saa fort den kan, og hvordan fargene ser ut. Alt som endres her
        // kan settes tilbake, og det som sto der for lagres foerst.
        Label scVerdict, scSub, scColourState;
        Panel scList;
        List<ScreenMode> scModes = new List<ScreenMode>();

        // Maalt paa et RTX 5090: metningen gaar 0-63, og 0 er standard.
        // 20 er tydelig dypere uten at hudtoner blir oransje.
        const int AnbefaltMetning = 20;
        const double AnbefaltGamma = 0.94;
        const double AnbefaltKontrast = 1.06;

        Panel PageScreen()
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
                Icons.Draw(e.Graphics, "skjerm", new RectangleF(24, 30, 28, 28), Theme.Accent);
            };

            scVerdict = Theme.Lbl("", new Font("Segoe UI Light", 20f), Theme.Text);
            scVerdict.Location = new Point(68, 22);
            scSub = Theme.Lbl("", Theme.FSmall, Theme.Muted);
            scSub.Location = new Point(70, 56);
            scSub.AutoSize = false;
            scSub.Height = 22;

            FlatBtn refresh = new FlatBtn(L.T("Les på nytt")).AsIcon("oppfrisk");
            Tip(refresh, "Les på nytt");
            refresh.Click += delegate { LoadScreen(); };
            Theme.Arrange(card, delegate
            {
                refresh.Location = new Point(card.Width - refresh.Width - 20, 22);
                scSub.Width = Math.Max(200, card.Width - 220);
            });

            card.Controls.Add(scVerdict);
            card.Controls.Add(scSub);
            card.Controls.Add(refresh);
            head.Controls.Add(card);

            // --- farge ---
            Panel colourHost = new Panel();
            colourHost.Dock = DockStyle.Bottom;
            colourHost.Height = 168;
            colourHost.BackColor = Theme.Bg;
            colourHost.Padding = new Padding(0, 16, 0, 0);

            Panel cc = Theme.MakeCard();
            cc.Dock = DockStyle.Fill;

            Label cTitle = Theme.Lbl(L.T("Farge"), Theme.FCard, Theme.Text);
            cTitle.Location = new Point(20, 14);

            scColourState = Theme.Lbl("", Theme.FSmall, Theme.Muted);
            scColourState.Location = new Point(20, 38);
            scColourState.AutoSize = false;
            scColourState.Height = 18;

            // Sto «i retning av det en OLED gir». Det er en paastand om et
            // resultat vi ikke kan love, og den slags inviterer til kritikk
            // fra folk med kalibrerte skjermer. Si hva den gjor, og be folk
            // prove selv.
            Label cWhat = Theme.Lbl(
                L.T("Dypere svart og litt mer metning. Om det ser bedre ut er en smakssak, så prøv det — Brisk leser kurven du har nå før den endrer noe, så «Tilbakestill» setter tilbake nøyaktig det du hadde."),
                Theme.FSmall, Theme.Muted);
            cWhat.AutoSize = false;
            cWhat.Location = new Point(20, 62);
            cWhat.Height = 40;

            FlatBtn bRec = new FlatBtn(L.T("Bruk anbefalt"));
            bRec.Primary();
            bRec.Width = 170; bRec.Height = 36;
            FlatBtn bReset = new FlatBtn(L.T("Tilbakestill"));
            bReset.Width = 150; bReset.Height = 36;

            Theme.Arrange(cc, delegate
            {
                int bredde = Math.Max(200, cc.Width - 40);
                cTitle.Width = bredde;
                scColourState.Width = bredde;
                cWhat.Width = bredde;
                bRec.Location = new Point(20, cc.Height - bRec.Height - 16);
                bReset.Location = new Point(20 + bRec.Width + 10, cc.Height - bReset.Height - 16);
            });

            cc.Controls.Add(cTitle);
            cc.Controls.Add(scColourState);
            cc.Controls.Add(cWhat);
            cc.Controls.Add(bRec);
            cc.Controls.Add(bReset);
            colourHost.Controls.Add(cc);

            bRec.Click += delegate
            {
                string feil = ScreenTools.ApplyColour(AnbefaltGamma, AnbefaltKontrast,
                    ScreenTools.HasVibrance ? AnbefaltMetning : -1);
                Status(feil ?? L.T("Fargeprofilen er satt."));
                LoadScreen();
            };
            bReset.Click += delegate
            {
                string feil = ScreenTools.ResetColour();
                Status(feil ?? L.T("Fargen er satt tilbake."));
                LoadScreen();
            };

            // --- skjermene ---
            scList = new Panel();
            scList.Dock = DockStyle.Fill;
            scList.BackColor = Theme.Bg;
            scList.AutoScroll = true;

            Label foot;
            Panel headRow = Widgets.Head(L.T("Skjermer"), out foot);

            p.Controls.Add(scList);
            p.Controls.Add(headRow);
            p.Controls.Add(colourHost);
            p.Controls.Add(head);

            Defer(delegate { LoadScreen(); });
            return p;
        }

        void LoadScreen()
        {
            scModes = ScreenTools.Displays();
            scList.Controls.Clear();

            int bak = 0;
            foreach (ScreenMode m in scModes) if (!m.AtMax) bak++;

            if (scModes.Count == 0)
            {
                scVerdict.Text = L.T("Fant ingen skjermer");
                scVerdict.ForeColor = Theme.Muted;
                scSub.Text = "";
            }
            else if (bak == 0)
            {
                scVerdict.Text = L.T("Skjermene kjører så fort de kan");
                scVerdict.ForeColor = Theme.Good;
                scSub.Text = L.F("{0} skjermer kontrollert.", scModes.Count);
            }
            else
            {
                scVerdict.Text = bak == 1
                    ? L.T("Én skjerm kjører saktere enn den kan")
                    : L.F("{0} skjermer kjører saktere enn de kan", bak);
                scVerdict.ForeColor = Theme.Warn;
                scSub.Text = L.T("Du betalte for bildene. Sett dem opp til det panelet klarer.");
            }

            // Nederst foerst: Dock.Top stabler ovenfra, saa lista bygges baklengs.
            for (int i = scModes.Count - 1; i >= 0; i--)
                scList.Controls.Add(ScreenCard(scModes[i]));

            // Alt som er endret skal staa, ikke bare metningen. Foer sto det
            // «metning 20» alene, som fikk det til aa se ut som om gammakurven
            // var urort - og den er den storste delen av forskjellen.
            int niva = ScreenTools.Vibrance();
            double gam, kon;
            string endret = L.T("Endret av Brisk.");
            if (ScreenTools.AppliedCurve(out gam, out kon))
                endret += "   ·   " + L.F("gamma {0}", gam.ToString("0.00")) +
                          "   ·   " + L.F("kontrast {0}", kon.ToString("0.00"));
            if (niva > 0) endret += "   ·   " + L.F("metning {0}", niva);

            scColourState.Text = !ScreenTools.ColourChanged
                ? L.T("Står som Windows satte den.")
                : endret;
            scColourState.ForeColor = ScreenTools.ColourChanged ? Theme.Accent : Theme.Muted;
        }

        Panel ScreenCard(ScreenMode m)
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Top;
            host.Height = 112;
            host.BackColor = Theme.Bg;
            host.Padding = new Padding(0, 0, 0, 12);

            Panel card = Theme.MakeCard();
            card.Dock = DockStyle.Fill;
            Color stripe = m.AtMax ? Theme.Good : Theme.Warn;
            card.Paint += delegate(object s, PaintEventArgs e)
            {
                using (SolidBrush b = new SolidBrush(stripe))
                    e.Graphics.FillRectangle(b, 0, 0, 3, card.Height);
            };

            // Skjermens eget navn foerst naar vi har det. «DISPLAY2» sier
            // ingenting; «Odyssey G93SC» sier hvilken skjerm det er snakk om.
            string tittel = m.Model.Length > 0 ? m.Model
                          : m.Primary ? L.T("Hovedskjerm")
                          : m.Device.Replace(@"\\.\", "");
            if (m.Primary && m.Model.Length > 0) tittel += "   ·   " + L.T("hovedskjerm");
            Label navn = Theme.Lbl(tittel, Theme.FCard, Theme.Text);
            navn.Location = new Point(20, 14);
            navn.AutoSize = false;
            navn.Height = 22;
            navn.AutoEllipsis = true;

            // Andre linje: det tekniske. Tommene er regnet fra fysisk
            // storrelse i hele centimeter, saa de er omtrentlige.
            string detalj = m.Width + " × " + m.Height;
            if (m.Inches >= 5) detalj += "   ·   " + m.Inches.ToString("0.0") + "″";
            if (m.Year > 1990) detalj += "   ·   " + m.Year;
            Label info = Theme.Lbl(detalj, Theme.FSmall, Theme.Muted);
            info.Location = new Point(20, 38);
            info.AutoSize = false;
            info.Height = 18;
            info.AutoEllipsis = true;

            Label hz = Theme.Lbl(
                m.AtMax ? L.F("{0} Hz — så fort panelet går", m.Hz)
                        : L.F("{0} Hz av {1} Hz", m.Hz, m.MaxHz),
                Theme.FSmall, m.AtMax ? Theme.Good : Theme.Warn);
            hz.Location = new Point(20, 60);
            hz.AutoSize = false;
            hz.Height = 18;

            FlatBtn act = new FlatBtn(L.F("Sett til {0} Hz", m.MaxHz));
            act.Primary();
            act.Width = 170; act.Height = 36;
            act.Visible = !m.AtMax && m.MaxHz > 0;

            ScreenMode meg = m;
            act.Click += delegate
            {
                string feil = ScreenTools.SetHz(meg, meg.MaxHz);
                Status(feil ?? L.F("Satt til {0} Hz.", meg.MaxHz));
                LoadScreen();
            };

            Theme.Arrange(card, delegate
            {
                act.Location = new Point(card.Width - act.Width - 20, 26);
                int plass = Math.Max(160, act.Left - 40);
                navn.Width = plass;
                info.Width = plass;
                hz.Width = plass;
            });

            card.Controls.Add(navn);
            card.Controls.Add(info);
            card.Controls.Add(hz);
            card.Controls.Add(act);
            host.Controls.Add(card);
            return host;
        }
    }
}
