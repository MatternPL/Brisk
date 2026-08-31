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
        // SplitterDistance kan bare settes når kontrollen har fått størrelse.
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

        // Meny på en handlingsflis, til valg som ikke passer som faner.
        static ContextMenuStrip TileMenu(ActionTile tile, string[] items, Action<string> chosen)
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Renderer = new DarkMenuRenderer();
            m.BackColor = Theme.CardHi;
            m.ForeColor = Theme.Text;
            m.ShowImageMargin = false;
            foreach (string s in items)
            {
                string val = s;
                ToolStripMenuItem mi = new ToolStripMenuItem(val);
                mi.BackColor = Theme.CardHi;
                mi.ForeColor = Theme.Text;
                mi.Click += delegate { chosen(val); };
                m.Items.Add(mi);
            }
            tile.Click += delegate { m.Show(tile, new Point(0, tile.Height)); };
            return m;
        }

        // ==============================================================
        //  DISKPLASS
        // ==============================================================
        ListView lvFolders, lvFiles, lvDup, lvOld, lvSys;
        SegmentBar segDisk;
        SplitContainer splitBig;
        Panel dupHost, oldHost, sysHost, rowNormal, rowSys;
        Label lblDiskSum, lblSysInfo;
        ActionTile tileWhere, tileFree, tileStop, tileOpen;
        string diskRoot = "";
        List<SpaceItem> sysItems = new List<SpaceItem>();

        Panel PageDisk()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            // --- faner ---
            segDisk = new SegmentBar();
            segDisk.Add(L.T("Største"));
            segDisk.Add(L.T("Duplikater"));
            segDisk.Add(L.T("Glemte filer"));
            segDisk.Add(L.T("Plass Windows holder på"));

            // --- handlinger ---
            List<string> roots = new List<string>();
            foreach (VolumeInfo v in MaintenanceTools.Volumes()) roots.Add(v.Letter);
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            diskRoot = roots.Count > 0 ? roots[0] : "C:\\";

            ActionTile tScan = new ActionTile(L.T("Analyser"),
                L.T("Leser gjennom hele treet. Sletter aldri noe.")).AsPrimary();
            tileWhere = new ActionTile(diskRoot, L.T("Klikk for å velge hvor det skal søkes."));
            tileStop = new ActionTile(L.T("Stopp"), L.T("Avbryter søket."));
            tileOpen = new ActionTile(L.T("Åpne i Utforsker"),
                L.T("Åpner stedet for den valgte raden. Dobbeltklikk gjør det samme."));
            tileStop.Enabled = false;

            TileMenu(tileWhere, roots.ToArray(), delegate(string v)
            {
                diskRoot = v;
                tileWhere.Title = v;
                tileWhere.Invalidate();
            });

            rowNormal = Widgets.Row(110, tScan, tileWhere, tileStop, tileOpen);

            tileFree = new ActionTile(L.T("Frigjør plass"),
                L.T("Velg en rad først. Du får se hva det koster deg før noe skjer.")).AsWarn();
            tileFree.Enabled = false;
            rowSys = Widgets.Row(110, tileFree);
            rowSys.Visible = false;

            // --- modus 1: største ---
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
            Label c1;
            splitBig.Panel1.Controls.Add(Widgets.Head(L.T("Største mapper"), out c1));
            lvFiles = ListIn(splitBig.Panel2, false,
                L.T("Fil"), "460", L.T("Størrelse"), "130", L.T("Mappe"), "460");
            Label c2;
            splitBig.Panel2.Controls.Add(Widgets.Head(L.T("Største filer (over 100 MB)"), out c2));

            // --- modus 2: duplikater ---
            dupHost = new Panel();
            dupHost.Dock = DockStyle.Fill;
            dupHost.BackColor = Theme.Bg;
            dupHost.Visible = false;
            lvDup = ListIn(dupHost, false,
                L.T("Fil"), "330", L.T("Kopier"), "80", L.T("Størrelse"), "110",
                L.T("Kan spares"), "120", L.T("Hvor"), "560");
            Label c3;
            dupHost.Controls.Add(Widgets.Head(L.T("Like filer — behold én, slett resten selv"), out c3));

            // --- modus 3: glemte filer ---
            oldHost = new Panel();
            oldHost.Dock = DockStyle.Fill;
            oldHost.BackColor = Theme.Bg;
            oldHost.Visible = false;
            lvOld = ListIn(oldHost, false,
                L.T("Fil"), "380", L.T("Størrelse"), "120", L.T("Sist rørt"), "140", L.T("Mappe"), "500");
            Label c4;
            oldHost.Controls.Add(Widgets.Head(L.T("Store filer du ikke har rørt på lenge"), out c4));

            // --- modus 4: plass Windows holder på ---
            sysHost = new Panel();
            sysHost.Dock = DockStyle.Fill;
            sysHost.BackColor = Theme.Bg;
            sysHost.Visible = false;
            lvSys = ListIn(sysHost, false,
                L.T("Post"), "260", L.T("Størrelse"), "120", L.T("Hva det er"), "700");
            sysHost.Controls.Add(Widgets.Head(
                L.T("Dette er ikke søppel — det er plass Windows har satt av"), out lblDiskSum));
            lvSys.SelectedIndexChanged += delegate { SysSelected(); };

            Panel sysInfo = new Panel();
            sysInfo.Dock = DockStyle.Bottom;
            sysInfo.Height = 44;
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
            p.Controls.Add(rowSys);
            p.Controls.Add(rowNormal);
            p.Controls.Add(segDisk);
            SetSplit(splitBig, 300);

            segDisk.Changed += delegate
            {
                int m = segDisk.Index;
                splitBig.Visible = m == 0;
                dupHost.Visible = m == 1;
                oldHost.Visible = m == 2;
                sysHost.Visible = m == 3;
                if (m == 1) dupHost.BringToFront();
                else if (m == 2) oldHost.BringToFront();
                else if (m == 3) sysHost.BringToFront();
                else splitBig.BringToFront();

                rowNormal.Visible = m != 3;
                rowSys.Visible = m == 3;
                tileFree.Enabled = false;
                lblSysInfo.Text = "";
                if (m == 3) LoadSystemSpace();
            };

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
            tileOpen.Click += openSel;
            lvFolders.DoubleClick += openSel;
            lvFiles.DoubleClick += openSel;
            lvDup.DoubleClick += openSel;
            lvOld.DoubleClick += openSel;
            tileFree.Click += async delegate { await FreeSystemSpace(); };

            tScan.Click += async delegate
            {
                if (string.IsNullOrEmpty(diskRoot)) return;
                string root = diskRoot;
                cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;
                tileStop.Enabled = true;
                int mode = segDisk.Index;
                DateTime t0 = DateTime.Now;

                List<SizeEntry> fo = null, fi = null, old = null;
                List<DupGroup> dups = null;

                await Job(new Control[] { tScan, tileWhere }, delegate
                {
                    if (mode == 0)
                        DiskTools.Scan(root, ct, delegate(string d) { Status(d); }, out fo, out fi);
                    else if (mode == 1)
                        dups = DupTools.Find(root, ct, delegate(string d) { Status(d); });
                    else
                        old = DupTools.Forgotten(root, 180, ct);
                });
                tileStop.Enabled = false;

                int secs = (int)(DateTime.Now - t0).TotalSeconds;

                if (mode == 0)
                {
                    if (fo == null) { Status(L.T("Avbrutt.")); return; }
                    Fill(lvFolders, fo, false);
                    Fill(lvFiles, fi, true);
                    long biggest = fo.Count > 0 ? fo[0].Size : 0;
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
                    Status(dups.Count == 0
                        ? L.F("Ingen duplikater funnet. Brukte {0} s.", secs)
                        : L.F("{0} grupper med like filer. {1} kan spares.", dups.Count, Util.Bytes(wasted)));
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
                    Status(L.F("{0} filer, til sammen {1}.", old.Count, Util.Bytes(sum)));
                }
            };

            tileStop.Click += delegate
            {
                if (cts != null) { try { cts.Cancel(); } catch { } }
                Status(L.T("Avbryter …"));
            };

            return p;
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

        void LoadSystemSpace()
        {
            try
            {
                sysItems = SpaceTools.Scan();
                long sum = 0, free = 0;
                lvSys.BeginUpdate();
                lvSys.Items.Clear();
                foreach (SpaceItem s in sysItems)
                {
                    sum += s.Size;
                    if (s.CanFree) free += s.Size;
                    ListViewItem li = new ListViewItem(s.Name);
                    li.SubItems.Add(Util.Bytes(s.Size));
                    li.SubItems.Add(s.What);
                    li.Tag = s;
                    li.ForeColor = s.CanFree ? Theme.Text : Theme.Muted;
                    lvSys.Items.Add(li);
                }
                lvSys.EndUpdate();
                lblDiskSum.Text = L.F("{0} totalt, {1} kan frigjøres", Util.Bytes(sum), Util.Bytes(free));
                lblDiskSum.ForeColor = Theme.Warn;
                lblSysInfo.Text = L.T("Velg en rad for å se hva du kan frigjøre og hva det koster deg.");
            }
            catch (Exception ex) { Status(L.T("Kunne ikke lese: ") + ex.Message); }
        }

        void SysSelected()
        {
            tileFree.Enabled = false;
            if (lvSys.SelectedItems.Count == 0) { lblSysInfo.Text = ""; return; }
            SpaceItem s = lvSys.SelectedItems[0].Tag as SpaceItem;
            if (s == null) return;
            if (!s.CanFree)
            {
                lblSysInfo.ForeColor = Theme.Muted;
                lblSysInfo.Text = L.T("Denne bør stå i fred. Windows styrer den selv.");
                tileFree.Title = L.T("Frigjør plass");
                tileFree.Info = L.T("Denne raden kan ikke frigjøres.");
                tileFree.Invalidate();
                return;
            }
            tileFree.Enabled = true;
            tileFree.Title = s.Action;
            tileFree.Info = L.F("Frigjør {0}.", Util.Bytes(s.Size));
            tileFree.Invalidate();
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
            await Job(new Control[] { tileFree }, delegate
            {
                ok = SpaceTools.Free(s, delegate(string l) { Status(l); });
            });
            LoadSystemSpace();
            RefreshOverview();
            Status(ok ? L.F("Frigjorde {0}.", Util.Bytes(s.Size)) : L.T("Ingenting ble frigjort."));
        }

        // ==============================================================
        //  PROGRAMVARE
        // ==============================================================
        ListView lvApps, lvInstalled;
        TextBox appOut;
        Label lblInstalledSum, lblUpdCount;

        Panel PageApps()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            ActionTile tChk = new ActionTile(L.T("Se etter oppdateringer"),
                L.T("Spør winget hvilke av programmene dine som har en nyere versjon.")).AsPrimary();
            ActionTile tUp = new ActionTile(L.T("Oppdater merkede"),
                L.T("Installerer de nye versjonene i bakgrunnen."));
            ActionTile tAll = new ActionTile(L.T("Merk alle"),
                L.T("Huker av eller vekk alle oppdateringene."));
            ActionTile tUn = new ActionTile(L.T("Avinstaller valgt"),
                L.T("Starter programmets egen avinstallering. Velg i den nedre lista.")).AsDanger();
            tUp.Enabled = false;

            Panel actions = Widgets.Row(110, tChk, tUp, tAll, tUn);

            Panel outHost = new Panel();
            outHost.Dock = DockStyle.Bottom;
            outHost.Height = 110;
            outHost.BackColor = Theme.Bg;
            appOut = Console(outHost, 0);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.BackColor = Theme.Bg;
            split.SplitterWidth = 14;
            split.Panel1.BackColor = Theme.Bg;
            split.Panel2.BackColor = Theme.Bg;
            split.Panel1MinSize = 90;
            split.Panel2MinSize = 90;

            lvApps = ListIn(split.Panel1, true,
                L.T("Program"), "290", L.T("Installert"), "130", L.T("Ny versjon"), "130", L.T("Pakke-ID"), "320");
            split.Panel1.Controls.Add(Widgets.Head(L.T("Programoppdateringer (winget)"), out lblUpdCount));

            lvInstalled = ListIn(split.Panel2, false,
                L.T("Program"), "330", L.T("Størrelse"), "110", L.T("Versjon"), "140",
                L.T("Utgiver"), "220", L.T("Installert"), "110");
            split.Panel2.Controls.Add(Widgets.Head(L.T("Installerte programmer"), out lblInstalledSum));

            p.Controls.Add(split);
            p.Controls.Add(outHost);
            p.Controls.Add(actions);
            SetSplit(split, 250);

            tAll.Click += delegate
            {
                bool any = false;
                foreach (ListViewItem li in lvApps.Items) if (!li.Checked) any = true;
                foreach (ListViewItem li in lvApps.Items) li.Checked = any;
            };

            tChk.Click += async delegate
            {
                if (!WingetTools.IsAvailable())
                {
                    Status(L.T("winget mangler. Installer «App Installer» fra Microsoft Store."));
                    return;
                }
                List<AppUpgrade> ups = null; string note = "";
                await Job(new Control[] { tChk, tUp, tAll }, delegate
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
                tUp.Enabled = lvApps.Items.Count > 0;
                lblUpdCount.Text = L.F("{0} kan oppdateres", lvApps.Items.Count);
                lblUpdCount.ForeColor = lvApps.Items.Count > 0 ? Theme.Warn : Theme.Good;
                Status(lvApps.Items.Count > 0
                    ? L.F("{0} kan oppdateres.", lvApps.Items.Count)
                    : (note.Length > 0 ? note : L.T("Alt er oppdatert.")));
            };

            tUp.Click += async delegate
            {
                List<AppUpgrade> chosen = new List<AppUpgrade>();
                foreach (ListViewItem li in lvApps.Items)
                    if (li.Checked && li.Tag != null) chosen.Add((AppUpgrade)li.Tag);
                if (chosen.Count == 0) { Status(L.T("Ingenting er merket.")); return; }

                int ok = 0;
                await Job(new Control[] { tChk, tUp, tAll }, delegate
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

            tUn.Click += delegate
            {
                if (lvInstalled.SelectedItems.Count == 0)
                { Status(L.T("Velg et program i den nedre lista.")); return; }
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
