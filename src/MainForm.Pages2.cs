using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vaktmester
{
    public partial class MainForm
    {
        // SplitterDistance kan bare settes nar kontrollen har fatt storrelse.
        void SetSplit(SplitContainer sc, int distance)
        {
            Defer(delegate
            {
                try
                {
                    int max = sc.Height - sc.Panel2MinSize - sc.SplitterWidth;
                    if (max > sc.Panel1MinSize)
                        sc.SplitterDistance = Math.Max(sc.Panel1MinSize, Math.Min(distance, max));
                }
                catch { }
            });
        }

        // ==============================================================
        //  DISKPLASS — hvor plassen faktisk har blitt av
        // ==============================================================
        ListView lvFolders, lvFiles;
        ComboBox cboRoot;
        Label lblDiskSum;

        Panel PageDisk()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            cboRoot = new ComboBox();
            cboRoot.DropDownStyle = ComboBoxStyle.DropDownList;
            cboRoot.Width = 210;
            cboRoot.Height = 26;
            cboRoot.FlatStyle = FlatStyle.Flat;
            cboRoot.BackColor = Theme.CardHi;
            cboRoot.ForeColor = Theme.Text;
            foreach (VolumeInfo v in MaintenanceTools.Volumes())
                cboRoot.Items.Add(v.Letter);
            cboRoot.Items.Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            if (cboRoot.Items.Count > 0) cboRoot.SelectedIndex = 0;

            FlatBtn bScan = new FlatBtn("Analyser plass"); bScan.Primary(); bScan.Width = 150;
            FlatBtn bStop = new FlatBtn("Stopp"); bStop.Width = 90; bStop.Enabled = false;
            FlatBtn bOpen = new FlatBtn("Åpne i Utforsker"); bOpen.Width = 165;
            lblDiskSum = Theme.Lbl("", Theme.FBold, Theme.Muted);
            lblDiskSum.Width = 260;
            Panel bar = Toolbar(cboRoot, bScan, bStop, bOpen, lblDiskSum);

            Panel note = new Panel();
            note.Dock = DockStyle.Bottom;
            note.Height = 34;
            note.BackColor = Theme.Bg;
            Label nl = Theme.Lbl(
                "Ingenting slettes her — dette er bare en oversikt. Dobbeltklikk en rad for å åpne stedet i Utforsker.",
                Theme.FSmall, Theme.Muted);
            nl.AutoSize = false; nl.Dock = DockStyle.Fill;
            note.Controls.Add(nl);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.BackColor = Theme.Bg;
            split.SplitterWidth = 12;
            split.Panel1.BackColor = Theme.Bg;
            split.Panel2.BackColor = Theme.Bg;
            split.Panel1MinSize = 90;
            split.Panel2MinSize = 90;

            Label h1 = Theme.Lbl("Største mapper", Theme.FBold, Theme.Text);
            h1.Dock = DockStyle.Top; h1.Height = 24;
            lvFolders = ListIn(split.Panel1, false, "Mappe", "600", "Størrelse", "120", "Filer", "90");
            split.Panel1.Controls.Add(h1);

            Label h2 = Theme.Lbl("Største enkeltfiler (over 100 MB)", Theme.FBold, Theme.Text);
            h2.Dock = DockStyle.Top; h2.Height = 24;
            lvFiles = ListIn(split.Panel2, false, "Fil", "600", "Størrelse", "120", "Mappe", "400");
            split.Panel2.Controls.Add(h2);

            p.Controls.Add(split);
            p.Controls.Add(note);
            p.Controls.Add(bar);
            SetSplit(split, 300);

            EventHandler openSel = delegate
            {
                ListView src = lvFolders.SelectedItems.Count > 0 ? lvFolders
                             : lvFiles.SelectedItems.Count > 0 ? lvFiles : null;
                if (src == null) { Status("Velg en rad først."); return; }
                string path = Convert.ToString(src.SelectedItems[0].Tag);
                if (string.IsNullOrEmpty(path)) return;
                try
                {
                    if (Directory.Exists(path)) Util.OpenPath(path);
                    else System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
                }
                catch (Exception ex) { Status("Kunne ikke åpne: " + ex.Message); }
            };
            bOpen.Click += openSel;
            lvFolders.DoubleClick += openSel;
            lvFiles.DoubleClick += openSel;

            bScan.Click += async delegate
            {
                string root = Convert.ToString(cboRoot.SelectedItem);
                if (string.IsNullOrEmpty(root)) return;
                cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;
                bStop.Enabled = true;
                List<SizeEntry> fo = null, fi = null;
                DateTime t0 = DateTime.Now;
                await Job(new Control[] { bScan, cboRoot }, delegate
                {
                    Status("Går gjennom " + root + " … dette kan ta et par minutter.");
                    DiskTools.Scan(root, ct, delegate(string d) { Status("Leser: " + d); }, out fo, out fi);
                });
                bStop.Enabled = false;
                if (fo == null) { Status("Analysen ble avbrutt."); return; }

                lvFolders.BeginUpdate();
                lvFolders.Items.Clear();
                foreach (SizeEntry e in fo)
                {
                    ListViewItem li = new ListViewItem(e.Name);
                    li.SubItems.Add(Util.Bytes(e.Size));
                    li.SubItems.Add(e.Files.ToString("N0"));
                    li.Tag = e.Path;
                    lvFolders.Items.Add(li);
                }
                lvFolders.EndUpdate();

                lvFiles.BeginUpdate();
                lvFiles.Items.Clear();
                foreach (SizeEntry e in fi)
                {
                    ListViewItem li = new ListViewItem(e.Name);
                    li.SubItems.Add(Util.Bytes(e.Size));
                    string dir = "";
                    try { dir = Path.GetDirectoryName(e.Path); }
                    catch { }
                    li.SubItems.Add(dir);
                    li.Tag = e.Path;
                    lvFiles.Items.Add(li);
                }
                lvFiles.EndUpdate();

                long biggest = fo.Count > 0 ? fo[0].Size : 0;
                lblDiskSum.Text = fo.Count + " mapper, " + fi.Count + " store filer";
                lblDiskSum.ForeColor = Theme.Good;
                Status("Ferdig på " + (int)(DateTime.Now - t0).TotalSeconds + " s. Største post: " +
                       Util.Bytes(biggest) + ".");
            };

            bStop.Click += delegate
            {
                if (cts != null) { try { cts.Cancel(); } catch { } }
                Status("Avbryter …");
            };

            return p;
        }

        // ==============================================================
        //  PROGRAMVARE — oppdatering (winget) + avinstallering
        // ==============================================================
        ListView lvApps, lvInstalled;
        TextBox appOut;
        Label lblInstalledSum;

        Panel PageApps()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            FlatBtn bChk = new FlatBtn("Se etter oppdateringer"); bChk.Primary(); bChk.Width = 200;
            FlatBtn bUp = new FlatBtn("Oppdater merkede"); bUp.Width = 165; bUp.Enabled = false;
            FlatBtn bAll = new FlatBtn("Merk alle"); bAll.Width = 110;
            Panel bar = Toolbar(bChk, bUp, bAll);

            Panel outHost = new Panel();
            outHost.Dock = DockStyle.Bottom;
            outHost.Height = 110;
            outHost.BackColor = Theme.Bg;
            appOut = Console(outHost, 0);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.BackColor = Theme.Bg;
            split.SplitterWidth = 12;
            split.Panel1.BackColor = Theme.Bg;
            split.Panel2.BackColor = Theme.Bg;
            split.Panel1MinSize = 90;
            split.Panel2MinSize = 90;

            Label h1 = Theme.Lbl("Tilgjengelige programoppdateringer (winget)", Theme.FBold, Theme.Text);
            h1.Dock = DockStyle.Top; h1.Height = 24;
            lvApps = ListIn(split.Panel1, true,
                "Program", "290", "Installert", "130", "Ny versjon", "130", "Pakke-ID", "320");
            split.Panel1.Controls.Add(h1);

            Panel instBar = new Panel();
            instBar.Dock = DockStyle.Top;
            instBar.Height = 30;
            instBar.BackColor = Theme.Bg;
            Label h2 = Theme.Lbl("Installerte programmer — sortert etter størrelse", Theme.FBold, Theme.Text);
            h2.Location = new Point(0, 4);
            FlatBtn bUn = new FlatBtn("Avinstaller valgt"); bUn.Danger();
            bUn.Width = 155; bUn.Height = 26; bUn.Location = new Point(360, 1); bUn.Font = Theme.FSmall;
            FlatBtn bRefI = new FlatBtn("Oppdater liste");
            bRefI.Width = 130; bRefI.Height = 26; bRefI.Location = new Point(525, 1); bRefI.Font = Theme.FSmall;
            lblInstalledSum = Theme.Lbl("", Theme.FSmall, Theme.Muted);
            lblInstalledSum.Location = new Point(670, 6);
            instBar.Controls.Add(h2); instBar.Controls.Add(bUn);
            instBar.Controls.Add(bRefI); instBar.Controls.Add(lblInstalledSum);

            lvInstalled = ListIn(split.Panel2, false,
                "Program", "330", "Størrelse", "110", "Versjon", "140", "Utgiver", "220", "Installert", "110");
            split.Panel2.Controls.Add(instBar);

            p.Controls.Add(split);
            p.Controls.Add(outHost);
            p.Controls.Add(bar);
            SetSplit(split, 250);

            bAll.Click += delegate
            {
                bool any = false;
                foreach (ListViewItem li in lvApps.Items) if (!li.Checked) any = true;
                foreach (ListViewItem li in lvApps.Items) li.Checked = any;
            };

            bChk.Click += async delegate
            {
                if (!WingetTools.IsAvailable())
                {
                    Status("winget mangler. Installer «App Installer» fra Microsoft Store, så virker denne.");
                    Append(appOut, "winget ble ikke funnet på maskinen.");
                    return;
                }
                List<AppUpgrade> ups = null; string note = "";
                await Job(new Control[] { bChk, bUp, bAll }, delegate
                {
                    Status("Spør winget om oppdateringer …");
                    ups = WingetTools.ListUpgrades(out note);
                });
                lvApps.Items.Clear();
                if (ups != null)
                    foreach (AppUpgrade a in ups)
                    {
                        ListViewItem li = new ListViewItem(a.Name);
                        li.SubItems.Add(a.Current);
                        li.SubItems.Add(a.Available);
                        li.SubItems.Add(a.Id);
                        li.Checked = true;
                        li.Tag = a;
                        lvApps.Items.Add(li);
                    }
                bUp.Enabled = lvApps.Items.Count > 0;
                Status(lvApps.Items.Count > 0
                    ? lvApps.Items.Count + " program(mer) kan oppdateres."
                    : (note.Length > 0 ? note : "Alt er oppdatert."));
            };

            bUp.Click += async delegate
            {
                List<AppUpgrade> chosen = new List<AppUpgrade>();
                foreach (ListViewItem li in lvApps.Items)
                    if (li.Checked && li.Tag != null) chosen.Add((AppUpgrade)li.Tag);
                if (chosen.Count == 0) { Status("Ingen programmer er merket."); return; }

                int ok = 0;
                await Job(new Control[] { bChk, bUp, bAll }, delegate
                {
                    foreach (AppUpgrade a in chosen)
                    {
                        Status("Oppdaterer " + a.Name + " …");
                        Append(appOut, "── " + a.Name + " (" + a.Id + ")");
                        if (WingetTools.Upgrade(a, delegate(string l) { Append(appOut, "   " + l); })) ok++;
                    }
                });
                Status("Oppdaterte " + ok + " av " + chosen.Count + " program(mer).");
            };

            bRefI.Click += delegate { LoadInstalled(); };
            bUn.Click += delegate
            {
                if (lvInstalled.SelectedItems.Count == 0) { Status("Velg et program i den nedre lista."); return; }
                InstalledApp a = lvInstalled.SelectedItems[0].Tag as InstalledApp;
                if (a == null) return;
                if (MessageBox.Show(this,
                        "Avinstaller «" + a.Name + "»?\n\n" +
                        "Programmets egen avinstallering starter. Følg eventuelle spørsmål der.",
                        "Bekreft avinstallering", MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning) != DialogResult.Yes) return;
                if (AppInventory.StartUninstall(a))
                    Status("Avinstallering startet for " + a.Name + ". Oppdater lista når den er ferdig.");
                else Status("Fant ingen avinstalleringskommando for " + a.Name + ".");
            };

            Defer(delegate { LoadInstalled(); });
            return p;
        }

        void LoadInstalled()
        {
            try
            {
                List<InstalledApp> apps = AppInventory.List();
                lvInstalled.BeginUpdate();
                lvInstalled.Items.Clear();
                long sum = 0;
                foreach (InstalledApp a in apps)
                {
                    ListViewItem li = new ListViewItem(a.Name);
                    li.SubItems.Add(a.EstimatedSize > 0 ? Util.Bytes(a.EstimatedSize) : "—");
                    li.SubItems.Add(a.Version);
                    li.SubItems.Add(a.Publisher);
                    li.SubItems.Add(a.Installed == DateTime.MinValue ? "" : a.Installed.ToString("yyyy-MM-dd"));
                    li.Tag = a;
                    lvInstalled.Items.Add(li);
                    sum += a.EstimatedSize;
                }
                lvInstalled.EndUpdate();
                lblInstalledSum.Text = apps.Count + " programmer · ca. " + Util.Bytes(sum);
            }
            catch (Exception ex) { Status("Kunne ikke lese programlista: " + ex.Message); }
        }
    }
}
