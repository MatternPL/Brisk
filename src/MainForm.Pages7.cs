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
        // saa fort den kan, og hvordan fargene ser ut.
        //
        // Alt staar per skjerm. Fargekortet laa foer nederst paa sida og
        // gjaldt alle, men en kurve som kler en 49-tommers OLED er ikke
        // noedvendigvis riktig for en liten LCD ved siden av - og metningen
        // ble uansett bare satt paa den forste skjermen.
        Label scVerdict, scSub;
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

            // --- fotnote ---
            Panel foot = new Panel();
            foot.Dock = DockStyle.Bottom;
            foot.Height = 44;
            foot.BackColor = Theme.Bg;
            Label fl = Theme.Lbl(
                L.T("Fargen settes per skjerm. Brisk leser kurven du har nå før den endrer noe, så «Tilbakestill» gir tilbake nøyaktig det den skjermen hadde."),
                Theme.FSmall, Theme.Muted);
            fl.AutoSize = false;
            fl.Dock = DockStyle.Fill;
            foot.Controls.Add(fl);

            // --- skjermene ---
            scList = new Panel();
            scList.Dock = DockStyle.Fill;
            scList.BackColor = Theme.Bg;
            scList.AutoScroll = true;

            Label teller;
            Panel headRow = Widgets.Head(L.T("Skjermer"), out teller);

            p.Controls.Add(scList);
            p.Controls.Add(headRow);
            p.Controls.Add(foot);
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
        }

        Panel ScreenCard(ScreenMode m)
        {
            // Kort uten VRR-linje er lavere. En tom linje der ser ut som noe
            // ikke ble lest ferdig.
            Panel host = new Panel();
            host.Dock = DockStyle.Top;
            host.Height = m.HasVrr ? 190 : 168;
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
            navn.AutoSize = false; navn.Height = 22; navn.AutoEllipsis = true;

            // Tommene er regnet fra fysisk storrelse i hele centimeter, saa
            // de er omtrentlige.
            string detalj = m.Width + " × " + m.Height;
            if (m.Inches >= 5) detalj += "   ·   " + m.Inches.ToString("0.0") + "″";
            if (m.Year > 1990) detalj += "   ·   " + m.Year;
            Label info = Theme.Lbl(detalj, Theme.FSmall, Theme.Muted);
            info.Location = new Point(20, 38);
            info.AutoSize = false; info.Height = 18; info.AutoEllipsis = true;

            Label hz = Theme.Lbl(
                m.AtMax ? L.F("{0} Hz — så fort panelet går", m.Hz)
                        : L.F("{0} Hz av {1} Hz", m.Hz, m.MaxHz),
                Theme.FSmall, m.AtMax ? Theme.Good : Theme.Warn);
            hz.Location = new Point(20, 60);
            hz.AutoSize = false; hz.Height = 18;

            FlatBtn setHz = new FlatBtn(L.F("Sett til {0} Hz", m.MaxHz));
            setHz.Primary();
            setHz.Width = 150; setHz.Height = 34;
            setHz.Visible = !m.AtMax && m.MaxHz > 0;

            // Skjermen sier selv fra om den takler variabel frekvens. Sier den
            // ingenting, staar det ingenting - vi vet ikke om den kan det.
            Label vrr = Theme.Lbl(
                m.HasVrr ? L.F("G-SYNC / FreeSync: {0}–{1} Hz", m.VrrMin, m.VrrMax) : "",
                Theme.FSmall, Theme.Muted);
            vrr.Location = new Point(20, 82);
            vrr.AutoSize = false; vrr.Height = 18; vrr.AutoEllipsis = true;
            vrr.Visible = m.HasVrr;

            // --- farge for nettopp denne skjermen ---
            Label farge = Theme.Lbl("", Theme.FSmall, Theme.Muted);
            farge.Location = new Point(20, m.HasVrr ? 104 : 82);
            farge.AutoSize = false; farge.Height = 18; farge.AutoEllipsis = true;

            FlatBtn bRec = new FlatBtn(L.T("Bruk anbefalt"));
            bRec.Width = 150; bRec.Height = 32;
            bRec.Font = Theme.FSmall;
            FlatBtn bReset = new FlatBtn(L.T("Tilbakestill"));
            bReset.Width = 120; bReset.Height = 32;
            bReset.Font = Theme.FSmall;

            ScreenMode meg = m;
            Action vis = delegate
            {
                bool endret = ScreenTools.ColourChanged(meg.Device);
                double g, k;
                string t = L.T("Farge") + ": ";
                if (!endret) t += L.T("står som Windows satte den");
                else
                {
                    t += L.T("endret av Brisk");
                    if (ScreenTools.AppliedCurve(meg.Device, out g, out k))
                        t += "   ·   " + L.F("gamma {0}", g.ToString("0.00")) +
                             "   ·   " + L.F("kontrast {0}", k.ToString("0.00"));
                    int niva = ScreenTools.Vibrance(meg.Device);
                    if (niva > 0) t += "   ·   " + L.F("metning {0}", niva);
                }
                farge.Text = t;
                farge.ForeColor = endret ? Theme.Accent : Theme.Muted;
                bRec.Enabled = !endret;
                bReset.Enabled = endret;
            };
            vis();

            setHz.Click += delegate
            {
                string feil = ScreenTools.SetHz(meg, meg.MaxHz);
                Status(feil ?? L.F("Satt til {0} Hz.", meg.MaxHz));
                LoadScreen();
            };
            bRec.Click += delegate
            {
                string feil = ScreenTools.ApplyColour(meg.Device, AnbefaltGamma, AnbefaltKontrast,
                    ScreenTools.HasVibrance ? AnbefaltMetning : -1);
                Status(feil ?? L.T("Fargeprofilen er satt."));
                vis();
            };
            bReset.Click += delegate
            {
                string feil = ScreenTools.ResetColour(meg.Device);
                Status(feil ?? L.T("Fargen er satt tilbake."));
                vis();
            };

            Theme.Arrange(card, delegate
            {
                setHz.Location = new Point(card.Width - setHz.Width - 20, 26);
                int plass = Math.Max(160, (setHz.Visible ? setHz.Left : card.Width) - 40);
                navn.Width = plass;
                info.Width = plass;
                hz.Width = plass;
                vrr.Width = plass;
                farge.Width = Math.Max(160, card.Width - 40);
                bRec.Location = new Point(20, card.Height - bRec.Height - 16);
                bReset.Location = new Point(20 + bRec.Width + 10, card.Height - bReset.Height - 16);
            });

            card.Controls.Add(navn);
            card.Controls.Add(info);
            card.Controls.Add(hz);
            card.Controls.Add(setHz);
            card.Controls.Add(vrr);
            card.Controls.Add(farge);
            card.Controls.Add(bRec);
            card.Controls.Add(bReset);
            host.Controls.Add(card);
            return host;
        }
    }
}
