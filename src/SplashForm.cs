using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

namespace Brisk
{
    // Det som er maalt ved oppstart, saa forsida har tall med en gang i stedet
    // for aa staa med streker til brukeren trykker Sjekk PC-en.
    public class StartupScan
    {
        public long Junk = -1;        // alt som finnes, inkludert det som er av
        public long JunkDefault = -1; // bare det som er huket av som standard
        public int StartupActive = -1;
        public int StartupTotal = -1;
        public int Wear = -1;
        public string WearDrive = "";
        public int Temp = -1;
        public string TempDrive = "";
        public GpuInfo Gpu;
        public int BlueScreens = -1;     // alle siste 30 dager
        public int BlueScreensNew = -1;  // de brukeren ikke har kvittert ut
        public bool Done;
    }

    // Vises mens maalingen gaar. Den gjor den ekte jobben - dette er ikke en
    // stripe som later som mens programmet egentlig er ferdig.
    public class SplashForm : Form
    {
        readonly Bar bar;
        readonly Label step;
        readonly StartupScan result = new StartupScan();
        Thread worker;

        public StartupScan Result { get { return result; } }

        public SplashForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(520, 260);
            BackColor = Color.FromArgb(0x0D, 0x10, 0x16);
            ShowInTaskbar = true;
            Text = "Brisk";
            DoubleBuffered = true;
            Theme.ApplyIcon(this);

            Paint += delegate(object s, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Logo.Paint(g, 44, 48, 84, true);

                using (Font f = new Font("Segoe UI Light", 40f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (SolidBrush b = new SolidBrush(Theme.Text))
                    g.DrawString("Brisk", f, b, 152, 52);

                using (SolidBrush b = new SolidBrush(Theme.Muted))
                    g.DrawString("v" + Updater.CurrentVersion, Theme.FSmall, b, 156, 106);

                using (Pen p = new Pen(Color.FromArgb(0x22, 0x27, 0x31)))
                    g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);

                using (LinearGradientBrush lb = new LinearGradientBrush(
                    new Rectangle(0, Height - 4, Width, 4),
                    Color.FromArgb(0x1E, 0x33, 0xA6), Color.FromArgb(0x6F, 0xBA, 0xFF), 0f))
                    g.FillRectangle(lb, 0, Height - 4, Width, 4);
            };

            bar = new Bar();
            bar.Location = new Point(44, 176);
            bar.Width = ClientSize.Width - 88;
            bar.Height = 6;
            bar.Fill = Theme.Accent;
            Controls.Add(bar);

            step = Theme.Lbl(L.T("Starter …"), Theme.FSmall, Theme.Muted);
            step.AutoSize = false;
            step.Location = new Point(44, 194);
            step.Width = ClientSize.Width - 88;
            step.Height = 20;
            step.AutoEllipsis = true;
            Controls.Add(step);

            Shown += delegate { Start(); };
        }

        void Vis(string tekst, double andel)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke((Action)delegate
            {
                if (IsDisposed) return;
                step.Text = tekst;
                bar.Value = andel;
                bar.Invalidate();
            });
        }

        // Maalingen kjorer paa en egen traad. Alt er pakket inn hver for seg -
        // feiler ett steg, skal de andre likevel bli maalt.
        void Start()
        {
            worker = new Thread(delegate ()
            {
                // Maalingen tar under et sekund paa en rask maskin. Uten en
                // minstetid ville vinduet blinket forbi og sett ut som en feil.
                // Paa tregere maskiner er den for lengst passert.
                DateTime start = DateTime.Now;
                try
                {
                    Vis(L.T("Ser etter søppelfiler …"), 0.05);
                    long sum = 0;
                    try
                    {
                        long standard = 0;
                        List<CleanTarget> mål = Cleaner.BuildTargets();
                        int i = 0;
                        foreach (CleanTarget t in mål)
                        {
                            i++;
                            Vis(L.T(t.Name), 0.05 + 0.55 * (i / (double)Math.Max(1, mål.Count)));
                            Cleaner.Scan(t, CancellationToken.None, null);
                            sum += t.FoundBytes;
                            // Nettleser-cache, krasjdumper og Windows.old er av
                            // som standard. De skal ikke telles med i tallet som
                            // avgjor om noe er verdt aa se paa - brukeren har
                            // allerede bestemt at de skal bli staaende.
                            if (t.DefaultChecked) standard += t.FoundBytes;
                        }
                        result.Junk = sum;
                        result.JunkDefault = standard;
                    }
                    catch (Exception ex) { Util.Log("Oppstartsmåling, søppel: " + ex.Message); }

                    Vis(L.T("Leser oppstartsprogrammer …"), 0.68);
                    try
                    {
                        int paa = 0, alle = 0;
                        foreach (StartupItem it in StartupTools.Enumerate(false))
                        {
                            alle++;
                            if (it.Enabled) paa++;
                        }
                        result.StartupActive = paa;
                        result.StartupTotal = alle;
                    }
                    catch (Exception ex) { Util.Log("Oppstartsmåling, oppstart: " + ex.Message); }

                    Vis(L.T("Leser diskhelse …"), 0.78);
                    try
                    {
                        foreach (DriveWear d in HealthTools.Drives())
                        {
                            if (d.Wear > result.Wear) { result.Wear = d.Wear; result.WearDrive = d.Name; }
                            if (d.Temperature > result.Temp)
                            { result.Temp = d.Temperature; result.TempDrive = d.Name; }
                        }
                    }
                    catch (Exception ex) { Util.Log("Oppstartsmåling, disk: " + ex.Message); }

                    // Bare det som staar paa maskina. Oppslaget mot NVIDIA og
                    // AMD gaar over nett og hoerer ikke hjemme i oppstarten.
                    Vis(L.T("Leser skjermkort …"), 0.88);
                    try { result.Gpu = GpuTools.Read(); }
                    catch (Exception ex) { Util.Log("Oppstartsmåling, skjermkort: " + ex.Message); }

                    Vis(L.T("Analyserer dumpfiler …"), 0.93);
                    try
                    {
                        // Teller dumpfiler, ikke loggforte hendelser: er dumpen
                        // borte har Helse ingenting aa vise, og da skal forsida
                        // ikke paastaa at det finnes noe aa se paa.
                        int n = DumpTools.RecentCount(30);
                        result.BlueScreens = n;
                        result.BlueScreensNew =
                            DumpTools.Newest() > HealthTools.CrashesSeenUntil ? n : 0;
                    }
                    catch (Exception ex) { Util.Log("Oppstartsmåling, blåskjermer: " + ex.Message); }

                    Vis(L.T("Klar."), 1.0);
                    result.Done = true;
                    Util.Log("Oppstartsmåling ferdig: " + Util.Bytes(result.Junk) + " søppel.");
                }
                catch (Exception ex) { Util.Log("Oppstartsmåling feilet: " + ex.Message); }

                int gaatt = (int)(DateTime.Now - start).TotalMilliseconds;
                if (gaatt < 1100) Thread.Sleep(1100 - gaatt);

                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke((Action)delegate
                {
                    if (!IsDisposed) { DialogResult = DialogResult.OK; Close(); }
                });
            });
            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.MTA);
            worker.Start();
        }
    }
}
