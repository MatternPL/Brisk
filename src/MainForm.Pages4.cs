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
        //  HELSE — disker, kræsj og batteri
        // ==============================================================
        ListView lvDrives, lvCrash;
        Label lblBattery;
        List<DumpAnalysis> crashDumps = new List<DumpAnalysis>();

        Panel PageHealth()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            FlatBtn bRef = new FlatBtn(L.T("Oppdater")); bRef.Primary(); bRef.Width = 130;
            FlatBtn bRep = new FlatBtn(L.T("Lag rapport")); bRep.Width = 150;
            lblBattery = Theme.Lbl("", Theme.FBold, Theme.Muted);
            lblBattery.Width = 420;
            Panel bar = Toolbar(bRef, bRep, lblBattery);
            Tip(bRep, "Lagrer en tekstfil på skrivebordet du kan sende til den som hjelper deg.");

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.BackColor = Theme.Bg;
            split.SplitterWidth = 14;
            split.Panel1.BackColor = Theme.Bg;
            split.Panel2.BackColor = Theme.Bg;
            split.Panel1MinSize = 90;
            split.Panel2MinSize = 90;

            lvDrives = ListIn(split.Panel1, false,
                L.T("Disk"), "300", L.T("Type"), "90", L.T("Tilstand"), "120",
                L.T("Slitasje"), "100", L.T("Temperatur"), "110", L.T("Plass"), "280");
            split.Panel1.Controls.Add(SectionLabel(L.T("Disker")));

            lvCrash = ListIn(split.Panel2, false,
                L.T("Når"), "150", L.T("Stoppkode"), "210", L.T("Sannsynlig årsak"), "230",
                L.T("Hva som skjedde"), "440");
            lvCrash.DoubleClick += delegate { OpenCrash(); };
            split.Panel2.Controls.Add(SectionLabel(
                L.T("Blåskjermer — dobbeltklikk for full analyse")));

            p.Controls.Add(split);
            p.Controls.Add(bar);
            SetSplit(split, 260);

            FlatBtn bOpen = new FlatBtn(L.T("Åpne analysen")); bOpen.Width = 150;
            bOpen.Click += delegate { OpenCrash(); };
            bar.Controls.Add(bOpen);
            bOpen.Location = new Point(bRep.Left + bRep.Width + 10, bRep.Top);
            Tip(bOpen, "Leser dumpfila fra kræsjet og viser hvilken driver som feilet.");

            bRef.Click += async delegate { await LoadHealth(new Control[] { bRef, bRep }); };
            bRep.Click += async delegate
            {
                string text = null;
                await Job(new Control[] { bRef, bRep }, delegate { text = Report.Build(); });
                if (text == null) return;
                SaveReport(text);
            };

            Defer(delegate { Task ignored = LoadHealth(new Control[] { bRef, bRep }); });
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

            // --- kræsj ---
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

            // Hendelser uten dumpfil — dumpen kan være slettet eller avslått.
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

            FlatBtn bRun = new FlatBtn(L.T("Test tilkoblingen")); bRun.Primary().Big(); bRun.Width = 190;
            FlatBtn bSettings = new FlatBtn(L.T("Nettverksinnstillinger")); bSettings.Width = 195; bSettings.Height = 44;
            FlatBtn bReset = new FlatBtn(L.T("Nullstill nettverket")); bReset.Danger(); bReset.Width = 190; bReset.Height = 44;
            Panel bar = Toolbar(bRun, bSettings, bReset);
            bar.Height = 62;
            Tip(bRun, "Sjekker nettverkskort, gateway, internett, DNS, Wi-Fi, hosts-fil og proxy. Endrer ingenting.");
            Tip(bReset, "Siste utvei når ingenting virker. Nullstiller Winsock og TCP/IP, og krever omstart.");

            Panel outHost = new Panel();
            outHost.Dock = DockStyle.Bottom;
            outHost.Height = 130;
            outHost.BackColor = Theme.Bg;
            TextBox netOut = Console(outHost, 0);

            Panel listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.BackColor = Theme.Bg;
            lvNet = ListIn(listHost, false, L.T("Test"), "220", L.T("Resultat"), "760");

            p.Controls.Add(listHost);
            p.Controls.Add(outHost);
            p.Controls.Add(bar);

            bSettings.Click += delegate { Util.OpenPath("ms-settings:network-status"); };

            bRun.Click += async delegate
            {
                List<NetCheck> res = null;
                await Job(new Control[] { bRun, bReset, bSettings }, delegate
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
                Status(bad == 0 ? L.T("Alt ser normalt ut.") : L.F("{0} problemer funnet.", bad));
            };

            bReset.Click += async delegate
            {
                if (!Util.IsAdmin()) { Status(L.T("Krever administrator.")); return; }
                if (MessageBox.Show(this,
                        L.T("Dette nullstiller nettverksoppsettet og krever omstart. Lagrede Wi-Fi-passord beholdes.") +
                        "\n\n" + L.T("Fortsette?"),
                        L.T("Nullstill nettverket"), MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning) != DialogResult.Yes) return;

                await Job(new Control[] { bRun, bReset, bSettings }, delegate
                {
                    NetTools.Reset(delegate(string l) { Append(netOut, l); });
                });
            };

            Defer(delegate { Append(netOut, L.T("Trykk «Test tilkoblingen» for å komme i gang.")); });
            return p;
        }
    }
}
