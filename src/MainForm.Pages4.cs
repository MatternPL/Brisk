using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Brisk
{
    public partial class MainForm
    {
        // ==============================================================
        //  HELSE
        // ==============================================================
        ListView lvDrives, lvCrash, lvAppCrash;
        Label lblBattery, lblCrashCount;
        SegmentBar segHealth;
        Panel crashHost, appCrashHost;
        ActionTile tileOpenCrash;
        List<DumpAnalysis> crashDumps = new List<DumpAnalysis>();
        List<AppCrash> appCrashes = new List<AppCrash>();

        Panel PageHealth()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            ActionTile tRef = new ActionTile(L.T("Oppdater"),
                L.T("Leser disker, kræsjlogger og batteri på nytt.")).AsPrimary();
            tileOpenCrash = new ActionTile(L.T("Åpne analysen"),
                L.T("Leser dumpfila og viser hvilken driver som feilet."));
            ActionTile tRep = new ActionTile(L.T("Lag rapport"),
                L.T("Lagrer en tekstfil på skrivebordet du kan sende til den som hjelper deg."));

            Panel actions = Widgets.Row(98, tRef, tileOpenCrash, tRep);

            Panel driveHost = new Panel();
            driveHost.Dock = DockStyle.Top;
            driveHost.Height = 190;
            driveHost.BackColor = Theme.Bg;
            driveHost.Padding = new Padding(0, 0, 0, 12);
            lvDrives = ListIn(driveHost, false,
                L.T("Disk"), "300", L.T("Type"), "90", L.T("Tilstand"), "120",
                L.T("Slitasje"), "100", L.T("Temperatur"), "110", L.T("Plass"), "280");
            driveHost.Controls.Add(Widgets.Head(L.T("Disker"), out lblBattery));

            segHealth = new SegmentBar();
            segHealth.Add(L.T("Blåskjermer"));
            segHealth.Add(L.T("Programkræsj"));

            crashHost = new Panel();
            crashHost.Dock = DockStyle.Fill;
            crashHost.BackColor = Theme.Bg;
            lvCrash = ListIn(crashHost, false,
                L.T("Når"), "150", L.T("Stoppkode"), "210", L.T("Sannsynlig årsak"), "230",
                L.T("Hva som skjedde"), "440");
            lvCrash.DoubleClick += delegate { OpenCrash(); };
            crashHost.Controls.Add(Widgets.Head(
                L.T("Dobbeltklikk en rad for full analyse"), out lblCrashCount));

            appCrashHost = new Panel();
            appCrashHost.Dock = DockStyle.Fill;
            appCrashHost.BackColor = Theme.Bg;
            appCrashHost.Visible = false;
            lvAppCrash = ListIn(appCrashHost, false,
                L.T("Program"), "230", L.T("Antall"), "80", L.T("Sist"), "140",
                L.T("Feilmodul"), "210", L.T("Hva som skjedde"), "380");
            lvAppCrash.SelectedIndexChanged += delegate
            {
                if (lvAppCrash.SelectedItems.Count == 0) return;
                AppCrash c = lvAppCrash.SelectedItems[0].Tag as AppCrash;
                if (c != null) Status(AppCrashTools.Advice(c));
            };
            lvAppCrash.DoubleClick += delegate { OpenAppCrash(); };
            Label c2;
            appCrashHost.Controls.Add(Widgets.Head(
                L.T("Siste 30 dager — dobbeltklikk for detaljer"), out c2));

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Theme.Bg;
            body.Controls.Add(crashHost);
            body.Controls.Add(appCrashHost);

            p.Controls.Add(body);
            p.Controls.Add(segHealth);
            p.Controls.Add(driveHost);
            p.Controls.Add(actions);

            segHealth.Changed += delegate
            {
                bool bs = segHealth.Index == 0;
                crashHost.Visible = bs;
                appCrashHost.Visible = !bs;
                if (bs) crashHost.BringToFront(); else appCrashHost.BringToFront();
                tileOpenCrash.Enabled = bs;
            };

            tRef.Click += async delegate { await LoadHealth(new Control[] { tRef, tRep }); };
            tileOpenCrash.Click += delegate { OpenCrash(); };
            tRep.Click += async delegate
            {
                string text = null;
                await Job(new Control[] { tRef, tRep }, delegate { text = Report.Build(); });
                if (text == null) return;
                SaveReport(text);
            };

            Defer(delegate { Task ignored = LoadHealth(new Control[] { tRef, tRep }); });
            return p;
        }

        async Task LoadHealth(Control[] btns)
        {
            List<DriveWear> wear = null;
            List<CrashEvent> crashes = null;
            BatteryHealth bat = null;
            List<DiskInfo> disks = null;
            List<VolumeInfo> vols = null;

            await Job(btns, delegate
            {
                Status(L.T("Leser disker …"));
                disks = MaintenanceTools.PhysicalDisks();
                wear = HealthTools.Drives();
                vols = MaintenanceTools.Volumes();
                Status(L.T("Leser hendelseslogg …"));
                crashes = HealthTools.Crashes(30);
                bat = HealthTools.Battery();

                Status(L.T("Leser programkræsj …"));
                appCrashes = AppCrashTools.Recent(30, 900);

                Status(L.T("Analyserer dumpfiler …"));
                crashDumps.Clear();
                foreach (string f in DumpTools.Find())
                {
                    crashDumps.Add(DumpTools.Analyse(f));
                    if (crashDumps.Count >= 20) break;
                }
            });

            // --- disker ---
            lvDrives.BeginUpdate();
            lvDrives.Items.Clear();
            if (disks != null)
                foreach (DiskInfo d in disks)
                {
                    DriveWear w = null;
                    if (wear != null)
                        foreach (DriveWear x in wear)
                            if (x.Name == d.Name) { w = x; break; }

                    ListViewItem li = new ListViewItem(d.Name);
                    li.SubItems.Add(d.Media);
                    li.SubItems.Add(L.T(d.Health));
                    li.SubItems.Add(w != null && w.Wear >= 0 ? L.F("{0} % brukt", w.Wear) : "—");
                    li.SubItems.Add(w != null && w.Temperature > 0 ? w.Temperature + " °C" : "—");
                    li.SubItems.Add(Util.Bytes(d.Size));

                    bool bad = d.Health != "Frisk" && d.Health != "Ukjent";
                    bool worn = w != null && w.Wear >= 80;
                    bool hot = w != null && w.Temperature >= 70;
                    li.ForeColor = bad || worn ? Theme.Bad : hot ? Theme.Warn : Theme.Text;
                    lvDrives.Items.Add(li);
                }

            if (vols != null)
                foreach (VolumeInfo v in vols)
                {
                    double freePct = v.Total > 0 ? (double)v.Free / v.Total : 0;
                    ListViewItem li = new ListViewItem("    " + v.Letter +
                        (string.IsNullOrEmpty(v.Label) ? "" : " (" + v.Label + ")"));
                    li.SubItems.Add(L.T("Volum"));
                    li.SubItems.Add(freePct < 0.1 ? L.T("Lite plass") : "OK");
                    li.SubItems.Add("—");
                    li.SubItems.Add("—");
                    li.SubItems.Add(L.F("{0} ledig av {1}", Util.Bytes(v.Free), Util.Bytes(v.Total)));
                    li.ForeColor = freePct < 0.1 ? Theme.Warn : Theme.Muted;
                    lvDrives.Items.Add(li);
                }
            lvDrives.EndUpdate();

            // --- blåskjermer ---
            lvCrash.BeginUpdate();
            lvCrash.Items.Clear();
            foreach (DumpAnalysis d in crashDumps)
            {
                ListViewItem li = new ListViewItem(d.Time.ToString("yyyy-MM-dd HH:mm"));
                li.SubItems.Add(d.CodeText);
                li.SubItems.Add(d.LikelyCause != null ? d.LikelyCause.Name
                              : d.Culprit != null ? d.Culprit.Name : "—");
                li.SubItems.Add(d.Error.Length > 0 ? d.Error : d.Meaning);
                li.Tag = d;
                bool fersk = (DateTime.Now - d.Time).TotalDays < 30;
                li.ForeColor = d.LikelyCause != null && !d.LikelyCause.IsMicrosoft ? Theme.Warn
                             : fersk ? Theme.Text : Theme.Muted;
                lvCrash.Items.Add(li);
            }
            if (crashes != null)
                foreach (CrashEvent c in crashes)
                {
                    bool har = false;
                    foreach (DumpAnalysis d in crashDumps)
                        if (Math.Abs((d.Time - c.Time).TotalMinutes) < 10) { har = true; break; }
                    if (har) continue;
                    ListViewItem li = new ListViewItem(c.Time.ToString("yyyy-MM-dd HH:mm"));
                    li.SubItems.Add(c.Code);
                    li.SubItems.Add("—");
                    li.SubItems.Add(c.Meaning + "  (" + L.T("ingen dumpfil") + ")");
                    li.ForeColor = Theme.Muted;
                    lvCrash.Items.Add(li);
                }
            if (lvCrash.Items.Count == 0)
            {
                ListViewItem li = new ListViewItem(L.T("Ingen blåskjermer i loggen."));
                li.ForeColor = Theme.Good;
                lvCrash.Items.Add(li);
            }
            lvCrash.EndUpdate();
            lblCrashCount.Text = crashDumps.Count > 0
                ? L.F("{0} dumpfiler analysert", crashDumps.Count) : "";

            // --- programkræsj ---
            lvAppCrash.BeginUpdate();
            lvAppCrash.Items.Clear();
            foreach (AppCrash c in appCrashes)
            {
                ListViewItem li = new ListViewItem(c.App);
                li.SubItems.Add(c.Count.ToString());
                li.SubItems.Add(c.Last.ToString("yyyy-MM-dd HH:mm"));
                li.SubItems.Add(c.Hang ? L.T("sluttet å svare") : c.Module);
                li.SubItems.Add(c.Meaning);
                li.Tag = c;
                li.ForeColor = c.Count >= 10 ? Theme.Bad : c.Count >= 3 ? Theme.Warn : Theme.Muted;
                lvAppCrash.Items.Add(li);
            }
            if (lvAppCrash.Items.Count == 0)
            {
                ListViewItem li = new ListViewItem(L.T("Ingen programkræsj siste 30 dager."));
                li.ForeColor = Theme.Good;
                lvAppCrash.Items.Add(li);
            }
            lvAppCrash.EndUpdate();

            // --- batteri ---
            if (bat == null) lblBattery.Text = "";
            else
            {
                lblBattery.Text = L.F("Batteri: {0} % av opprinnelig kapasitet", bat.HealthPercent);
                lblBattery.ForeColor = bat.HealthPercent < 60 ? Theme.Bad
                                     : bat.HealthPercent < 80 ? Theme.Warn : Theme.Good;
            }

            Status("");
        }

        void OpenAppCrash()
        {
            if (lvAppCrash.SelectedItems.Count == 0) return;
            AppCrash c = lvAppCrash.SelectedItems[0].Tag as AppCrash;
            if (c == null) return;

            string txt = c.App + (c.Version.Length > 0 ? "  " + c.Version : "") + "\n\n" +
                L.F("{0} ganger siste 30 dager, sist {1}.", c.Count, c.Last.ToString("yyyy-MM-dd HH:mm")) + "\n";
            if (!c.Hang)
            {
                txt += "\n" + L.T("Feilmodul") + ": " + c.Module +
                       (c.ModuleVersion.Length > 0 ? "  " + c.ModuleVersion : "");
                txt += "\n" + L.T("Kode") + ": " + c.Code +
                       (c.Meaning.Length > 0 ? "  —  " + c.Meaning : "");
            }
            txt += "\n\n" + AppCrashTools.Advice(c);

            MessageBox.Show(this, txt, L.T("Programkræsj"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void OpenCrash()
        {
            DumpAnalysis d = null;
            if (lvCrash.SelectedItems.Count > 0)
                d = lvCrash.SelectedItems[0].Tag as DumpAnalysis;
            if (d == null && crashDumps.Count > 0) d = crashDumps[0];
            if (d == null) { Status(L.T("Ingen dumpfil å analysere.")); return; }
            try
            {
                using (CrashDialog dlg = new CrashDialog(d)) dlg.ShowDialog(this);
            }
            catch (Exception ex) { Status(L.T("Kunne ikke vise analysen: ") + ex.Message); }
        }

        void SaveReport(string text)
        {
            string path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Brisk-rapport.txt");
            try
            {
                System.IO.File.WriteAllText(path, text, System.Text.Encoding.UTF8);
                Status(L.T("Rapport lagret på skrivebordet."));
                Util.OpenPath(path);
            }
            catch (Exception ex) { Status(L.T("Kunne ikke lagre rapporten: ") + ex.Message); }
        }

        // ==============================================================
        //  NETTVERK
        // ==============================================================
        ListView lvNet;

        Panel PageNetwork()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            ActionTile tRun = new ActionTile(L.T("Test tilkoblingen"),
                L.T("Sjekker nettverkskort, gateway, internett, DNS, Wi-Fi, hosts-fil og proxy. Endrer ingenting.")).AsPrimary();
            ActionTile tSet = new ActionTile(L.T("Nettverksinnstillinger"),
                L.T("Åpner Windows sine egne innstillinger."));
            ActionTile tReset = new ActionTile(L.T("Nullstill nettverket"),
                L.T("Siste utvei når ingenting virker. Nullstiller Winsock og TCP/IP, og krever omstart.")).AsDanger();

            Panel actions = Widgets.Row(98, tRun, tSet, tReset);

            Panel outHost = new Panel();
            outHost.Dock = DockStyle.Bottom;
            outHost.Height = 120;
            outHost.BackColor = Theme.Bg;
            TextBox netOut = Console(outHost, 0);

            Panel listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.BackColor = Theme.Bg;
            lvNet = ListIn(listHost, false, L.T("Test"), "220", L.T("Resultat"), "760");
            Label netCount;
            listHost.Controls.Add(Widgets.Head(L.T("Tilkobling"), out netCount));

            p.Controls.Add(listHost);
            p.Controls.Add(outHost);
            p.Controls.Add(actions);

            tSet.Click += delegate { Util.OpenPath("ms-settings:network-status"); };

            tRun.Click += async delegate
            {
                List<NetCheck> res = null;
                await Job(new Control[] { tRun, tReset, tSet }, delegate
                {
                    res = NetTools.RunAll(delegate(string s) { Status(s); });
                });
                lvNet.BeginUpdate();
                lvNet.Items.Clear();
                int bad = 0;
                if (res != null)
                    foreach (NetCheck c in res)
                    {
                        ListViewItem li = new ListViewItem("●  " + c.What);
                        li.SubItems.Add(c.Result);
                        li.ForeColor = c.Level >= 2 ? Theme.Bad : c.Level == 1 ? Theme.Warn : Theme.Good;
                        lvNet.Items.Add(li);
                        if (c.Level >= 2) bad++;
                    }
                lvNet.EndUpdate();
                netCount.Text = bad == 0 ? L.T("Alt ser normalt ut.") : L.F("{0} problemer funnet.", bad);
                netCount.ForeColor = bad == 0 ? Theme.Good : Theme.Bad;
                Status("");
            };

            tReset.Click += async delegate
            {
                if (!Util.IsAdmin()) { Status(L.T("Krever administrator.")); return; }
                if (MessageBox.Show(this,
                        L.T("Dette nullstiller nettverksoppsettet og krever omstart. Lagrede Wi-Fi-passord beholdes.") +
                        "\n\n" + L.T("Fortsette?"),
                        L.T("Nullstill nettverket"), MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning) != DialogResult.Yes) return;

                await Job(new Control[] { tRun, tReset, tSet }, delegate
                {
                    NetTools.Reset(delegate(string l) { Append(netOut, l); });
                });
            };

            Defer(delegate { Append(netOut, L.T("Trykk «Test tilkoblingen» for å komme i gang.")); });
            return p;
        }
    }
}
