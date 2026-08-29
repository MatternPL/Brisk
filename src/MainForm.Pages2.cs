using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Brisk
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
        //  DISKPLASS — hvor plassen har blitt av
        // ==============================================================
        ListView lvFolders, lvFiles, lvDup, lvOld, lvSys;
        Chooser cboRoot, cboMode;
        SplitContainer splitBig;
        Panel dupHost, oldHost, sysHost;
        Label lblDiskSum, lblSysInfo;
        FlatBtn btnFree;
        List<SpaceItem> sysItems = new List<SpaceItem>();

        Panel PageDisk()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            cboMode = new Chooser();
            cboMode.Width = 210;
            cboMode.Add(L.T("Største mapper og filer"));
            cboMode.Add(L.T("Duplikater"));
            cboMode.Add(L.T("Glemte filer"));
            cboMode.Add(L.T("Plass Windows holder på"));

            cboRoot = new Chooser();
            cboRoot.Width = 230;
            foreach (VolumeInfo v in MaintenanceTools.Volumes())
                cboRoot.Add(v.Letter);
            cboRoot.Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            cboRoot.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

            FlatBtn bScan = new FlatBtn(L.T("Analyser")); bScan.Primary(); bScan.Width = 130;
            FlatBtn bStop = new FlatBtn(L.T("Stopp")); bStop.Width = 90; bStop.Enabled = false;
            FlatBtn bOpen = new FlatBtn(L.T("Åpne i Utforsker")); bOpen.Width = 165;
            btnFree = new FlatBtn(L.T("Frigjør plass")); btnFree.Warn(); btnFree.Width = 165;
            btnFree.Visible = false;
            lblDiskSum = Theme.Lbl("", Theme.FBold, Theme.Muted);
            lblDiskSum.Width = 300;
            Panel bar = Toolbar(cboMode, cboRoot, bScan, bStop, bOpen, btnFree, lblDiskSum);
            Tip(bScan, "Leser gjennom hele treet. Sletter aldri noe.");
            Tip(bOpen, "Dobbeltklikk en rad gjør det samme.");
            Tip(cboMode, "Største viser hvor plassen ligger. Duplikater finner like filer. Glemte filer er store filer du ikke har rørt på et halvår.");

            // --- modus 1: største mapper og filer ---
            splitBig = new SplitContainer();
            splitBig.Dock = DockStyle.Fill;
            splitBig.Orientation = Orientation.Horizontal;
            splitBig.BackColor = Theme.Bg;
            splitBig.SplitterWidth = 14;
            splitBig.Panel1.BackColor = Theme.Bg;
            splitBig.Panel2.BackColor = Theme.Bg;
            splitBig.Panel1MinSize = 90;
            splitBig.Panel2MinSize = 90;
            lvFolders = ListIn(splitBig.Panel1, false,
                L.T("Mappe"), "620", L.T("Størrelse"), "130", L.T("Filer"), "100");
            splitBig.Panel1.Controls.Add(SectionLabel(L.T("Største mapper")));
            lvFiles = ListIn(splitBig.Panel2, false,
                L.T("Fil"), "460", L.T("Størrelse"), "130", L.T("Mappe"), "460");
            splitBig.Panel2.Controls.Add(SectionLabel(L.T("Største filer (over 100 MB)")));

            // --- modus 2: duplikater ---
            dupHost = new Panel();
            dupHost.Dock = DockStyle.Fill;
            dupHost.BackColor = Theme.Bg;
            dupHost.Visible = false;
            lvDup = ListIn(dupHost, false,
                L.T("Fil"), "330", L.T("Kopier"), "80", L.T("Størrelse"), "110",
                L.T("Kan spares"), "120", L.T("Hvor"), "560");
            dupHost.Controls.Add(SectionLabel(L.T("Like filer — behold én, slett resten selv")));

            // --- modus 3: glemte filer ---
            oldHost = new Panel();
            oldHost.Dock = DockStyle.Fill;
            oldHost.BackColor = Theme.Bg;
            oldHost.Visible = false;
            lvOld = ListIn(oldHost, false,
                L.T("Fil"), "380", L.T("Størrelse"), "120", L.T("Sist rørt"), "140", L.T("Mappe"), "500");
            oldHost.Controls.Add(SectionLabel(L.T("Store filer du ikke har rørt på lenge")));

            // --- modus 4: plass Windows selv holder på ---
            sysHost = new Panel();
            sysHost.Dock = DockStyle.Fill;
            sysHost.BackColor = Theme.Bg;
            sysHost.Visible = false;
            lvSys = ListIn(sysHost, false,
                L.T("Post"), "260", L.T("Størrelse"), "120", L.T("Hva det er"), "700");
            sysHost.Controls.Add(SectionLabel(L.T("Dette er ikke søppel — det er plass Windows har satt av")));
            lvSys.SelectedIndexChanged += delegate { SysSelected(); };

            Panel sysInfo = new Panel();
            sysInfo.Dock = DockStyle.Bottom;
            sysInfo.Height = 52;
            sysInfo.BackColor = Theme.Bg;
            lblSysInfo = Theme.Lbl("", Theme.FSmall, Theme.Muted);
            lblSysInfo.AutoSize = false;
            lblSysInfo.Dock = DockStyle.Fill;
            sysInfo.Controls.Add(lblSysInfo);

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Theme.Bg;
            body.Controls.Add(splitBig);
            body.Controls.Add(dupHost);
            body.Controls.Add(oldHost);
            body.Controls.Add(sysHost);

            p.Controls.Add(body);
            p.Controls.Add(sysInfo);
            p.Controls.Add(bar);
            SetSplit(splitBig, 300);

            cboMode.Changed += delegate
            {
                int m = ModeIndex();
                splitBig.Visible = m == 0;
                dupHost.Visible = m == 1;
                oldHost.Visible = m == 2;
                sysHost.Visible = m == 3;
                if (m == 1) dupHost.BringToFront();
                else if (m == 2) oldHost.BringToFront();
                else if (m == 3) sysHost.BringToFront();
                else splitBig.BringToFront();

                // I denne modusen er det ingenting aa skanne eller aapne;
                // knappene byttes ut med handlingen som hoerer til.
                cboRoot.Visible = m != 3;
                bStop.Visible = m != 3;
                bOpen.Visible = m != 3;
                bScan.Visible = m != 3;
                btnFree.Visible = m == 3;
                if (m == 3) btnFree.Location = bScan.Location;
                btnFree.Enabled = false;
                lblSysInfo.Text = "";
                lblDiskSum.Text = "";
                if (m == 3) LoadSystemSpace();
            };

            btnFree.Click += async delegate { await FreeSystemSpace(); };

            EventHandler openSel = delegate
            {
                ListView src = null;
                foreach (ListView lv in new ListView[] { lvFolders, lvFiles, lvDup, lvOld })
                    if (lv.SelectedItems.Count > 0) { src = lv; break; }
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
            lvDup.DoubleClick += openSel;
            lvOld.DoubleClick += openSel;

            bScan.Click += async delegate
            {
                string root = cboRoot.Value;
                if (string.IsNullOrEmpty(root)) return;
                cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;
                bStop.Enabled = true;
                int mode = ModeIndex();
                DateTime t0 = DateTime.Now;

                List<SizeEntry> fo = null, fi = null, old = null;
                List<DupGroup> dups = null;

                await Job(new Control[] { bScan, cboRoot, cboMode }, delegate
                {
                    if (mode == 0)
                        DiskTools.Scan(root, ct, delegate(string d) { Status(d); }, out fo, out fi);
                    else if (mode == 1)
                        dups = DupTools.Find(root, ct, delegate(string d) { Status(d); });
                    else
                        old = DupTools.Forgotten(root, 180, ct);
                });
                bStop.Enabled = false;

                int secs = (int)(DateTime.Now - t0).TotalSeconds;

                if (mode == 0)
                {
                    if (fo == null) { Status(L.T("Avbrutt.")); return; }
                    Fill(lvFolders, fo, false);
                    Fill(lvFiles, fi, true);
                    long biggest = fo.Count > 0 ? fo[0].Size : 0;
                    lblDiskSum.Text = L.F("{0} mapper, {1} store filer", fo.Count, fi.Count);
                    lblDiskSum.ForeColor = Theme.Good;
                    Status(L.F("Ferdig på {0} s. Største post: {1}.", secs, Util.Bytes(biggest)));
                }
                else if (mode == 1)
                {
                    if (dups == null) { Status(L.T("Avbrutt.")); return; }
                    long wasted = 0;
                    lvDup.BeginUpdate();
                    lvDup.Items.Clear();
                    foreach (DupGroup g in dups)
                    {
                        wasted += g.Wasted;
                        string first = g.Files[0];
                        List<string> dirs = new List<string>();
                        foreach (string f in g.Files)
                        {
                            try { dirs.Add(Path.GetDirectoryName(f)); }
                            catch { }
                        }
                        ListViewItem li = new ListViewItem(Path.GetFileName(first));
                        li.SubItems.Add(g.Files.Count.ToString());
                        li.SubItems.Add(Util.Bytes(g.Size));
                        li.SubItems.Add(Util.Bytes(g.Wasted));
                        li.SubItems.Add(string.Join("   ·   ", dirs.ToArray()));
                        li.Tag = first;
                        lvDup.Items.Add(li);
                    }
                    lvDup.EndUpdate();
                    lblDiskSum.Text = L.F("{0} kan spares", Util.Bytes(wasted));
                    lblDiskSum.ForeColor = wasted > 0 ? Theme.Warn : Theme.Good;
                    Status(dups.Count == 0
                        ? L.F("Ingen duplikater funnet. Brukte {0} s.", secs)
                        : L.F("{0} grupper med like filer. Brukte {1} s.", dups.Count, secs));
                }
                else
                {
                    if (old == null) { Status(L.T("Avbrutt.")); return; }
                    long sum = 0;
                    lvOld.BeginUpdate();
                    lvOld.Items.Clear();
                    foreach (SizeEntry e in old)
                    {
                        sum += e.Size;
                        ListViewItem li = new ListViewItem(e.Name);
                        li.SubItems.Add(Util.Bytes(e.Size));
                        li.SubItems.Add(L.F("{0} dager siden", e.Files));
                        string dir = "";
                        try { dir = Path.GetDirectoryName(e.Path); }
                        catch { }
                        li.SubItems.Add(dir);
                        li.Tag = e.Path;
                        li.ForeColor = e.Files > 365 ? Theme.Warn : Theme.Text;
                        lvOld.Items.Add(li);
                    }
                    lvOld.EndUpdate();
                    lblDiskSum.Text = Util.Bytes(sum);
                    lblDiskSum.ForeColor = Theme.Warn;
                    Status(L.F("{0} filer, til sammen {1}.", old.Count, Util.Bytes(sum)));
                }
            };

            bStop.Click += delegate
            {
                if (cts != null) { try { cts.Cancel(); } catch { } }
                Status(L.T("Avbryter …"));
            };

            return p;
        }

        int ModeIndex()
        {
            string v = cboMode.Value;
            if (v == L.T("Duplikater")) return 1;
            if (v == L.T("Glemte filer")) return 2;
            if (v == L.T("Plass Windows holder på")) return 3;
            return 0;
        }

        // ---- plass Windows selv holder på ----
        void LoadSystemSpace()
        {
            try
            {
                sysItems = SpaceTools.Scan();
                long sum = 0;
                lvSys.BeginUpdate();
                lvSys.Items.Clear();
                foreach (SpaceItem s in sysItems)
                {
                    sum += s.Size;
                    ListViewItem li = new ListViewItem(s.Name);
                    li.SubItems.Add(Util.Bytes(s.Size));
                    li.SubItems.Add(s.What);
                    li.Tag = s;
                    li.ForeColor = s.CanFree ? Theme.Text : Theme.Muted;
                    lvSys.Items.Add(li);
                }
                lvSys.EndUpdate();
                lblDiskSum.Text = Util.Bytes(sum);
                lblDiskSum.ForeColor = Theme.Warn;
                lblSysInfo.Text = L.T("Velg en rad for å se hva du kan frigjøre og hva det koster deg.");
            }
            catch (Exception ex) { Status(L.T("Kunne ikke lese: ") + ex.Message); }
        }

        void SysSelected()
        {
            btnFree.Enabled = false;
            if (lvSys.SelectedItems.Count == 0) { lblSysInfo.Text = ""; return; }
            SpaceItem s = lvSys.SelectedItems[0].Tag as SpaceItem;
            if (s == null) return;
            if (!s.CanFree)
            {
                lblSysInfo.ForeColor = Theme.Muted;
                lblSysInfo.Text = L.T("Denne bør stå i fred. Windows styrer den selv.");
                return;
            }
            btnFree.Enabled = true;
            btnFree.Text = s.Action;
            lblSysInfo.ForeColor = Theme.Warn;
            lblSysInfo.Text = s.Consequence;
        }

        async Task FreeSystemSpace()
        {
            if (lvSys.SelectedItems.Count == 0) return;
            SpaceItem s = lvSys.SelectedItems[0].Tag as SpaceItem;
            if (s == null || !s.CanFree) return;

            if (!Util.IsAdmin()) { Status(L.T("Krever administrator.")); return; }

            if (MessageBox.Show(this,
                    L.F("{0} — frigjør {1}.", s.Name, Util.Bytes(s.Size)) + "\n\n" +
                    s.Consequence + "\n\n" + L.T("Fortsette?"),
                    s.Action, MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes) return;

            bool ok = false;
            await Job(new Control[] { btnFree, cboMode }, delegate
            {
                ok = SpaceTools.Free(s, delegate(string l) { Status(l); });
            });
            LoadSystemSpace();
            RefreshOverview();
            Status(ok ? L.F("Frigjorde {0}.", Util.Bytes(s.Size)) : L.T("Ingenting ble frigjort."));
        }

        void Fill(ListView lv, List<SizeEntry> items, bool showFolder)
        {
            lv.BeginUpdate();
            lv.Items.Clear();
            if (items != null)
                foreach (SizeEntry e in items)
                {
                    ListViewItem li = new ListViewItem(e.Name);
                    li.SubItems.Add(Util.Bytes(e.Size));
                    if (showFolder)
                    {
                        string dir = "";
                        try { dir = Path.GetDirectoryName(e.Path); }
                        catch { }
                        li.SubItems.Add(dir);
                    }
                    else li.SubItems.Add(e.Files.ToString("N0"));
                    li.Tag = e.Path;
                    lv.Items.Add(li);
                }
            lv.EndUpdate();
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
