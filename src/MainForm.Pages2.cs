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
        Chooser cboRoot;
        Label lblDiskSum;

        Panel PageDisk()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            cboRoot = new Chooser();
            cboRoot.Width = 230;
            foreach (VolumeInfo v in MaintenanceTools.Volumes())
                cboRoot.Add(v.Letter);
            cboRoot.Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

            FlatBtn bScan = new FlatBtn(L.T("Analyser plass")); bScan.Primary(); bScan.Width = 150;
            FlatBtn bStop = new FlatBtn(L.T("Stopp")); bStop.Width = 90; bStop.Enabled = false;
            FlatBtn bOpen = new FlatBtn(L.T("Åpne i Utforsker")); bOpen.Width = 165;
            lblDiskSum = Theme.Lbl("", Theme.FBold, Theme.Muted);
            lblDiskSum.Width = 260;
            Panel bar = Toolbar(cboRoot, bScan, bStop, bOpen, lblDiskSum);

            Tip(bScan, "Leser gjennom hele treet. Sletter ingenting.");
            Tip(bOpen, "Dobbeltklikk en rad gjør det samme.");

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.BackColor = Theme.Bg;
            split.SplitterWidth = 12;
            split.Panel1.BackColor = Theme.Bg;
            split.Panel2.BackColor = Theme.Bg;
            split.Panel1MinSize = 90;
            split.Panel2MinSize = 90;

            Label h1 = Theme.Lbl(L.T("Største mapper"), Theme.FBold, Theme.Text);
            h1.Dock = DockStyle.Top; h1.Height = 24;
            lvFolders = ListIn(split.Panel1, false, L.T("Mappe"), "600", L.T("Størrelse"), "120", L.T("Filer"), "90");
            split.Panel1.Controls.Add(h1);

            Label h2 = Theme.Lbl(L.T("Største filer (over 100 MB)"), Theme.FBold, Theme.Text);
            h2.Dock = DockStyle.Top; h2.Height = 24;
            lvFiles = ListIn(split.Panel2, false, L.T("Fil"), "600", L.T("Størrelse"), "120", L.T("Mappe"), "400");
            split.Panel2.Controls.Add(h2);

            p.Controls.Add(split);
            p.Controls.Add(bar);
            SetSplit(split, 300);

            EventHandler openSel = delegate
            {
                ListView src = lvFolders.SelectedItems.Count > 0 ? lvFolders
                             : lvFiles.SelectedItems.Count > 0 ? lvFiles : null;
                if (src == null) { Status(L.T("Velg en rad først.")); return; }
                string path = Convert.ToString(src.SelectedItems[0].Tag);
                if (string.IsNullOrEmpty(path)) return;
                try
                {
                    if (Directory.Exists(path)) Util.OpenPath(path);
                    else System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
                }
                catch (Exception ex) { Status(L.T("Kunne ikke åpne: ") + ex.Message); }
            };
            bOpen.Click += openSel;
            lvFolders.DoubleClick += openSel;
            lvFiles.DoubleClick += openSel;

            bScan.Click += async delegate
            {
                string root = cboRoot.Value;
                if (string.IsNullOrEmpty(root)) return;
                cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;
                bStop.Enabled = true;
                List<SizeEntry> fo = null, fi = null;
                DateTime t0 = DateTime.Now;
                await Job(new Control[] { bScan, cboRoot }, delegate
                {
                    Status(L.F("Går gjennom {0} …", root));
                    DiskTools.Scan(root, ct, delegate(string d) { Status(d); }, out fo, out fi);
                });
                bStop.Enabled = false;
                if (fo == null) { Status(L.T("Avbrutt.")); return; }

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
                lblDiskSum.Text = L.F("{0} mapper, {1} store filer", fo.Count, fi.Count);
                lblDiskSum.ForeColor = Theme.Good;
                Status(L.F("Ferdig på {0} s. Største post: {1}.",
                       (int)(DateTime.Now - t0).TotalSeconds, Util.Bytes(biggest)));
            };

            bStop.Click += delegate
            {
                if (cts != null) { try { cts.Cancel(); } catch { } }
                Status(L.T("Avbryter …"));
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

            FlatBtn bChk = new FlatBtn(L.T("Se etter oppdateringer")); bChk.Primary(); bChk.Width = 200;
            FlatBtn bUp = new FlatBtn(L.T("Oppdater merkede")); bUp.Width = 165; bUp.Enabled = false;
            FlatBtn bAll = new FlatBtn(L.T("Merk alle")); bAll.Width = 110;
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

            Label h1 = Theme.Lbl(L.T("Programoppdateringer (winget)"), Theme.FBold, Theme.Text);
            h1.Dock = DockStyle.Top; h1.Height = 24;
            lvApps = ListIn(split.Panel1, true,
                L.T("Program"), "290", L.T("Installert"), "130", L.T("Ny versjon"), "130", L.T("Pakke-ID"), "320");
            split.Panel1.Controls.Add(h1);

            Panel instBar = new Panel();
            instBar.Dock = DockStyle.Top;
            instBar.Height = 30;
            instBar.BackColor = Theme.Bg;
            Label h2 = Theme.Lbl(L.T("Installerte programmer"), Theme.FBold, Theme.Text);
            h2.Location = new Point(0, 4);
            FlatBtn bUn = new FlatBtn(L.T("Avinstaller")); bUn.Danger();
            bUn.Width = 155; bUn.Height = 26; bUn.Location = new Point(360, 1); bUn.Font = Theme.FSmall;
            FlatBtn bRefI = new FlatBtn(L.T("Oppdater"));
            bRefI.Width = 130; bRefI.Height = 26; bRefI.Location = new Point(525, 1); bRefI.Font = Theme.FSmall;
            lblInstalledSum = Theme.Lbl("", Theme.FSmall, Theme.Muted);
            lblInstalledSum.Location = new Point(670, 6);
            instBar.Controls.Add(h2); instBar.Controls.Add(bUn);
            instBar.Controls.Add(bRefI); instBar.Controls.Add(lblInstalledSum);

            lvInstalled = ListIn(split.Panel2, false,
                L.T("Program"), "330", L.T("Størrelse"), "110", L.T("Versjon"), "140",
                L.T("Utgiver"), "220", L.T("Installert"), "110");
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
                    Status(L.T("winget mangler. Installer «App Installer» fra Microsoft Store."));
                    return;
                }
                List<AppUpgrade> ups = null; string note = "";
                await Job(new Control[] { bChk, bUp, bAll }, delegate
                {
                    Status(L.T("Spør winget …"));
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
                    ? L.F("{0} kan oppdateres.", lvApps.Items.Count)
                    : (note.Length > 0 ? note : L.T("Alt er oppdatert.")));
            };

            bUp.Click += async delegate
            {
                List<AppUpgrade> chosen = new List<AppUpgrade>();
                foreach (ListViewItem li in lvApps.Items)
                    if (li.Checked && li.Tag != null) chosen.Add((AppUpgrade)li.Tag);
                if (chosen.Count == 0) { Status(L.T("Ingenting er merket.")); return; }

                int ok = 0;
                await Job(new Control[] { bChk, bUp, bAll }, delegate
                {
                    foreach (AppUpgrade a in chosen)
                    {
                        Status(a.Name);
                        Append(appOut, "── " + a.Name + " (" + a.Id + ")");
                        if (WingetTools.Upgrade(a, delegate(string l) { Append(appOut, "   " + l); })) ok++;
                    }
                });
                Status(L.F("Oppdaterte {0} av {1}.", ok, chosen.Count));
            };

            bRefI.Click += delegate { LoadInstalled(); };
            bUn.Click += delegate
            {
                if (lvInstalled.SelectedItems.Count == 0) { Status(L.T("Velg et program i den nedre lista.")); return; }
                InstalledApp a = lvInstalled.SelectedItems[0].Tag as InstalledApp;
                if (a == null) return;
                if (MessageBox.Show(this,
                        L.F("Avinstaller «{0}»? Programmets egen avinstallering starter.", a.Name),
                        L.T("Avinstaller"), MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning) != DialogResult.Yes) return;
                if (AppInventory.StartUninstall(a)) Status(L.F("Startet avinstallering av {0}.", a.Name));
                else Status(L.F("Fant ingen avinstalleringskommando for {0}.", a.Name));
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
                lblInstalledSum.Text = L.F("{0} programmer · {1}", apps.Count, Util.Bytes(sum));
            }
            catch (Exception ex) { Status(L.T("Kunne ikke lese programlista: ") + ex.Message); }
        }
    }
}
