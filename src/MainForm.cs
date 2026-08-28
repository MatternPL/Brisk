using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vaktmester
{
    public partial class MainForm : Form
    {
        Panel side, content, host, statusBar;
        Label lblTitle, lblSubtitle, lblStatus;
        List<NavBtn> navs = new List<NavBtn>();
        Dictionary<string, Panel> pages = new Dictionary<string, Panel>();
        string current = "";
        CancellationTokenSource cts;
        ToolTip tips = new ToolTip();

        public MainForm() : this("oversikt") { }

        public MainForm(string startPage)
        {
            Text = "Vaktmester";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1120, 760);
            MinimumSize = new Size(960, 640);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F;
            DoubleBuffered = true;
            Theme.ApplyIcon(this);

            tips.AutoPopDelay = 12000;
            tips.InitialDelay = 500;
            tips.ReshowDelay = 200;

            BuildShell();
            Show(string.IsNullOrEmpty(startPage) ? "oversikt" : startPage);

            Util.LogWritten += OnLogWritten;
            Load += delegate
            {
                Theme.DarkTitleBar(this);
                if (Updater.AutoCheck && (DateTime.Now - Updater.LastCheck).TotalHours >= 20)
                {
                    Task.Run((Action)delegate
                    {
                        string err;
                        UpdateInfo u = Updater.Check(out err);
                        if (u == null) return;
                        try { BeginInvoke((Action)delegate { ShowUpdateDialog(u); }); }
                        catch { }
                    });
                }
            };
        }

        public void Tip(Control c, string no) { try { tips.SetToolTip(c, L.T(no)); } catch { } }

        // ==============================================================
        void BuildShell()
        {
            content = new Panel();
            content.Dock = DockStyle.Fill;
            content.BackColor = Theme.Bg;
            content.Padding = new Padding(28, 22, 28, 0);
            Controls.Add(content);

            side = new Panel();
            side.Dock = DockStyle.Left;
            side.Width = 218;
            side.BackColor = Theme.Side;
            Controls.Add(side);

            statusBar = new Panel();
            statusBar.Dock = DockStyle.Bottom;
            statusBar.Height = 30;
            statusBar.BackColor = Theme.Bg;
            lblStatus = Theme.Lbl("", Theme.FSmall, Theme.Muted);
            lblStatus.Location = new Point(2, 8);
            lblStatus.AutoSize = false;
            lblStatus.Size = new Size(900, 18);
            lblStatus.AutoEllipsis = true;
            statusBar.Controls.Add(lblStatus);
            content.Controls.Add(statusBar);

            host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Theme.Bg;
            content.Controls.Add(host);
            host.BringToFront();

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 74;
            header.BackColor = Theme.Bg;
            lblTitle = Theme.Lbl("", Theme.FTitle, Theme.Text);
            lblTitle.Location = new Point(0, 0);
            lblSubtitle = Theme.Lbl("", Theme.F, Theme.Muted);
            lblSubtitle.Location = new Point(2, 38);
            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSubtitle);
            content.Controls.Add(header);

            // --- sidemeny ---
            Panel adminBox = new Panel();
            adminBox.Dock = DockStyle.Bottom;
            adminBox.Height = Util.IsAdmin() ? 40 : 76;
            adminBox.BackColor = Theme.Side;
            if (Util.IsAdmin())
            {
                Label ok = Theme.Lbl("● " + L.T("Administrator"), Theme.FSmall, Theme.Good);
                ok.Location = new Point(16, 8);
                adminBox.Controls.Add(ok);
            }
            else
            {
                Label w = Theme.Lbl("● " + L.T("Begrenset"), Theme.FSmall, Theme.Warn);
                w.Location = new Point(16, 6);
                adminBox.Controls.Add(w);
                FlatBtn b = new FlatBtn(L.T("Kjør som administrator"));
                b.Height = 28;
                b.Width = 186;
                b.Location = new Point(16, 28);
                b.Font = Theme.FSmall;
                b.Click += delegate { if (Util.RelaunchAsAdmin()) Application.Exit(); };
                adminBox.Controls.Add(b);
            }
            side.Controls.Add(adminBox);

            Panel langBox = new Panel();
            langBox.Dock = DockStyle.Bottom;
            langBox.Height = 44;
            langBox.BackColor = Theme.Side;
            FlatBtn bEn = new FlatBtn("English");
            FlatBtn bNo = new FlatBtn("Norsk");
            bEn.Width = 91; bEn.Height = 28; bEn.Location = new Point(16, 8); bEn.Font = Theme.FSmall;
            bNo.Width = 91; bNo.Height = 28; bNo.Location = new Point(111, 8); bNo.Font = Theme.FSmall;
            if (L.IsNorwegian) bNo.Primary(); else bEn.Primary();
            EventHandler bytt = delegate(object s2, EventArgs e2)
            {
                string want = (s2 == bNo) ? "no" : "en";
                if (want == L.Lang) return;
                L.Lang = want;
                Util.Log("Språk: " + want + ". Starter på nytt.");
                try
                {
                    System.Diagnostics.ProcessStartInfo psi =
                        new System.Diagnostics.ProcessStartInfo(Util.ExePath());
                    psi.Arguments = "/side:" + current;
                    psi.UseShellExecute = true;
                    System.Diagnostics.Process.Start(psi);
                }
                catch { }
                Application.Exit();
            };
            bEn.Click += bytt;
            bNo.Click += bytt;
            langBox.Controls.Add(bEn);
            langBox.Controls.Add(bNo);
            side.Controls.Add(langBox);

            Panel navHost = new Panel();
            navHost.Dock = DockStyle.Fill;
            navHost.BackColor = Theme.Side;
            side.Controls.Add(navHost);
            navHost.BringToFront();

            // Omvendt rekkefølge — Dock.Top stabler nedenfra.
            AddNav(navHost, "logg", L.T("Logg"));
            AddNav(navHost, "vedlikehold", L.T("Vedlikehold"));
            AddNav(navHost, "programmer", L.T("Programvare"));
            AddNav(navHost, "drivere", L.T("Oppdateringer"));
            AddNav(navHost, "minne", L.T("Minne"));
            AddNav(navHost, "oppstart", L.T("Oppstart"));
            AddNav(navHost, "diskplass", L.T("Diskplass"));
            AddNav(navHost, "rydding", L.T("Rydding"));
            AddNav(navHost, "oversikt", L.T("Oversikt"));

            Panel brand = new Panel();
            brand.Dock = DockStyle.Top;
            brand.Height = 100;
            brand.BackColor = Theme.Side;
            brand.Paint += delegate(object s, PaintEventArgs e)
            {
                Logo.Paint(e.Graphics, 18, 28, 40, true);
            };
            Label b1 = Theme.Lbl("Vaktmester", new Font("Segoe UI Light", 16f), Theme.Text);
            b1.Location = new Point(68, 26);
            Label b2 = Theme.Lbl("v" + Updater.CurrentVersion, Theme.FSmall, Theme.Muted);
            b2.Location = new Point(70, 58);
            brand.Controls.Add(b1);
            brand.Controls.Add(b2);
            navHost.Controls.Add(brand);
        }

        void AddNav(Panel parent, string key, string text)
        {
            NavBtn n = new NavBtn(text);
            n.Click += delegate { Show(key); };
            n.Tag = key;
            parent.Controls.Add(n);
            navs.Add(n);
        }

        void Show(string key)
        {
            if (current == key) return;
            current = key;
            foreach (NavBtn n in navs)
            {
                n.Active = (string)n.Tag == key;
                n.Invalidate();
            }

            SetHead(TitleOf(key), SubOf(key));

            Panel p;
            if (!pages.TryGetValue(key, out p))
            {
                p = BuildPage(key);
                p.Dock = DockStyle.Fill;
                pages[key] = p;
                host.Controls.Add(p);
            }
            foreach (Panel other in pages.Values) other.Visible = (other == p);
            p.BringToFront();
        }

        static string TitleOf(string key)
        {
            switch (key)
            {
                case "oversikt": return L.T("Oversikt");
                case "rydding": return L.T("Rydding");
                case "diskplass": return L.T("Diskplass");
                case "oppstart": return L.T("Oppstart");
                case "minne": return L.T("Minne");
                case "drivere": return L.T("Oppdateringer");
                case "programmer": return L.T("Programvare");
                case "vedlikehold": return L.T("Vedlikehold");
                default: return L.T("Logg");
            }
        }

        static string SubOf(string key)
        {
            switch (key)
            {
                case "oversikt": return L.T("Tilstanden akkurat nå.");
                case "rydding": return L.T("Filer som bare tar plass.");
                case "diskplass": return L.T("Hvor plassen har blitt av.");
                case "oppstart": return L.T("Det som starter med Windows.");
                case "minne": return L.T("Hva RAM-en brukes til.");
                case "drivere": return L.T("Fra Windows Update.");
                case "programmer": return L.T("Oppdater eller fjern programmer.");
                case "vedlikehold": return L.T("Reparasjon og diskhelse.");
                default: return L.T("Alt som er gjort.");
            }
        }

        Panel BuildPage(string key)
        {
            switch (key)
            {
                case "oversikt": return PageOverview();
                case "rydding": return PageClean();
                case "diskplass": return PageDisk();
                case "oppstart": return PageStartup();
                case "minne": return PageMemory();
                case "drivere": return PageDrivers();
                case "programmer": return PageApps();
                case "vedlikehold": return PageMaint();
                default: return PageLog();
            }
        }

        void SetHead(string t, string s)
        {
            lblTitle.Text = t;
            lblSubtitle.Text = s;
        }

        // ==============================================================
        //  Hjelpere
        // ==============================================================
        public void ShowUpdateDialog(UpdateInfo u)
        {
            try
            {
                using (UpdateDialog d = new UpdateDialog(u)) d.ShowDialog(this);
            }
            catch (Exception ex) { Status(L.T("Kunne ikke vise oppdateringen: ") + ex.Message); }
        }

        public async Task CheckForUpdates(bool manual)
        {
            UpdateInfo u = null;
            string err = null;
            Status(L.T("Ser etter oppdateringer …"));
            await Task.Run((Action)delegate { u = Updater.Check(out err); });

            if (u != null)
            {
                Status(L.F("Versjon {0} er tilgjengelig.", u.Version));
                ShowUpdateDialog(u);
            }
            else if (err != null) Status(err);
            else if (manual) Status(L.F("Du har nyeste versjon ({0}).", Updater.CurrentVersion));
        }

        public void Status(string s)
        {
            if (IsHandleCreated && InvokeRequired)
            {
                BeginInvoke((Action)delegate { Status(s); });
                return;
            }
            lblStatus.Text = s;
        }

        void SetNavEnabled(bool on)
        {
            foreach (NavBtn n in navs) n.Enabled = on;
        }

        static Panel Row(int height)
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Top;
            p.Height = height;
            p.BackColor = Theme.Bg;
            return p;
        }

        static Panel Spacer(int h)
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Top;
            p.Height = h;
            p.BackColor = Theme.Bg;
            return p;
        }

        // ==============================================================
        //  OVERSIKT
        // ==============================================================
        Label ovRam, ovRamSub, ovDisk, ovDiskSub, ovStart, ovStartSub, ovJunk, ovJunkSub;
        Bar ovRamBar, ovDiskBar;
        ListView lvFindings;
        long junkFound = -1;

        Panel PageOverview()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            Panel cards = new Panel();
            cards.Dock = DockStyle.Top;
            cards.Height = 152;
            cards.BackColor = Theme.Bg;

            Panel c1 = StatCard(out ovRam, out ovRamSub, L.T("Minne"), out ovRamBar);
            Panel c2 = StatCard(out ovDisk, out ovDiskSub, L.T("Ledig på systemdisken"), out ovDiskBar);
            Panel c3 = StatCard(out ovStart, out ovStartSub, L.T("Starter med Windows"), null);
            Panel c4 = StatCard(out ovJunk, out ovJunkSub, L.T("Søppel"), null);

            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.ColumnCount = 4;
            grid.RowCount = 1;
            grid.BackColor = Theme.Bg;
            for (int i = 0; i < 4; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            grid.Controls.Add(Wrap(c1), 0, 0);
            grid.Controls.Add(Wrap(c2), 1, 0);
            grid.Controls.Add(Wrap(c3), 2, 0);
            grid.Controls.Add(Wrap(c4), 3, 0);
            cards.Controls.Add(grid);

            Panel actions = Row(58);
            FlatBtn scan = new FlatBtn(L.T("Kjør sjekk"));
            scan.Primary();
            scan.Width = 140;
            scan.Location = new Point(0, 12);
            FlatBtn refresh = new FlatBtn(L.T("Oppdater"));
            refresh.Width = 120;
            refresh.Location = new Point(152, 12);
            actions.Controls.Add(scan);
            actions.Controls.Add(refresh);
            Tip(scan, "Måler søppelfiler og ser etter ting som er verdt å gjøre noe med.");

            Label h = Theme.Lbl(L.T("Verdt å se på"), Theme.FBold, Theme.Text);
            h.Dock = DockStyle.Top;
            h.Height = 26;

            Panel listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.BackColor = Theme.Bg;
            lvFindings = ListIn(listHost, false, L.T("Funn"), "620", L.T("Hva du kan gjøre"), "420");
            lvFindings.DoubleClick += delegate
            {
                if (lvFindings.SelectedItems.Count == 0) return;
                string key = Convert.ToString(lvFindings.SelectedItems[0].Tag);
                if (!string.IsNullOrEmpty(key)) Show(key);
            };
            listHost.Controls.Add(h);

            p.Controls.Add(listHost);
            p.Controls.Add(actions);
            p.Controls.Add(cards);

            scan.Click += async delegate
            {
                scan.Enabled = false; refresh.Enabled = false;
                await FullScan();
                scan.Enabled = true; refresh.Enabled = true;
            };
            refresh.Click += delegate { RefreshOverview(); };

            Defer(delegate { RefreshOverview(); });
            return p;
        }

        static Panel Wrap(Panel inner)
        {
            Panel w = new Panel();
            w.Dock = DockStyle.Fill;
            w.BackColor = Theme.Bg;
            w.Padding = new Padding(0, 0, 12, 0);
            inner.Dock = DockStyle.Fill;
            w.Controls.Add(inner);
            return w;
        }

        Panel StatCard(out Label big, out Label sub, string caption, out Bar bar)
        {
            Panel c = Theme.MakeCard();
            Label cap = Theme.Lbl(caption, Theme.FSmall, Theme.Muted);
            cap.Location = new Point(16, 14);
            big = Theme.Lbl("—", Theme.FBig, Theme.Text);
            big.Location = new Point(13, 34);
            sub = Theme.Lbl("", Theme.FSmall, Theme.Muted);
            sub.Location = new Point(16, 96);
            bar = new Bar();
            bar.Location = new Point(16, 82);
            bar.Width = 180;
            c.Controls.Add(cap);
            c.Controls.Add(big);
            c.Controls.Add(sub);
            c.Controls.Add(bar);
            return c;
        }

        Panel StatCard(out Label big, out Label sub, string caption, Bar ignored)
        {
            Bar b;
            Panel p = StatCard(out big, out sub, caption, out b);
            b.Visible = false;
            sub.Location = new Point(16, 84);
            return p;
        }

        void AddFinding(Color dot, string text, string action, string page)
        {
            ListViewItem li = new ListViewItem("● " + text);
            li.SubItems.Add(action);
            li.ForeColor = dot;
            li.Tag = page;
            lvFindings.Items.Add(li);
        }

        void RefreshOverview()
        {
            try
            {
                MemSnapshot m = MemoryTools.Snapshot();
                ovRam.Text = m.LoadPercent + " %";
                ovRamSub.Text = L.F("{0} av {1}", Util.Bytes(m.UsedPhys), Util.Bytes(m.TotalPhys));
                ovRamBar.Value = m.LoadPercent / 100.0;
                ovRamBar.Fill = m.LoadPercent > 88 ? Theme.Bad : m.LoadPercent > 70 ? Theme.Warn : Theme.Good;
                ovRamBar.Invalidate();

                DriveInfo sys = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                double freePct = (double)sys.AvailableFreeSpace / sys.TotalSize;
                ovDisk.Text = Util.Bytes(sys.AvailableFreeSpace);
                ovDiskSub.Text = L.F("{0} % av {1}", (freePct * 100).ToString("0"), Util.Bytes(sys.TotalSize));
                ovDiskBar.Value = 1 - freePct;
                ovDiskBar.Fill = freePct < 0.08 ? Theme.Bad : freePct < 0.15 ? Theme.Warn : Theme.Good;
                ovDiskBar.Invalidate();

                int active = 0, total = 0;
                foreach (StartupItem it in StartupTools.Enumerate(false))
                {
                    total++;
                    if (it.Enabled) active++;
                }
                ovStart.Text = active.ToString();
                ovStartSub.Text = L.F("av {0}", total);

                if (junkFound >= 0)
                {
                    ovJunk.Text = Util.Bytes(junkFound);
                    ovJunkSub.Text = "";
                }

                // --- funn ---
                lvFindings.BeginUpdate();
                lvFindings.Items.Clear();

                if (freePct < 0.15)
                    AddFinding(freePct < 0.08 ? Theme.Bad : Theme.Warn,
                        L.F("Bare {0} ledig på {1}", Util.Bytes(sys.AvailableFreeSpace), sys.Name),
                        L.T("Rydd, eller se hva som tar plassen"), "diskplass");

                if (junkFound > 1024L * 1024 * 1024)
                    AddFinding(Theme.Warn, L.F("{0} søppelfiler", Util.Bytes(junkFound)),
                        L.T("Rens dem"), "rydding");

                if (active > 8)
                    AddFinding(Theme.Warn, L.F("{0} programmer starter med Windows", active),
                        L.T("Slå av det du ikke trenger"), "oppstart");

                if (m.LoadPercent > 85)
                    AddFinding(Theme.Warn, L.F("Minnet er {0} % fullt", m.LoadPercent),
                        L.T("Se hva som bruker det"), "minne");

                try
                {
                    List<ProblemDevice> devs = DriverTools.FindProblemDevices();
                    int real = 0;
                    foreach (ProblemDevice d in devs)
                        if (d.ErrorCode != 22 && d.ErrorCode != 45) real++;
                    if (real > 0)
                        AddFinding(Theme.Bad, L.F("{0} enheter melder feil", real),
                            L.T("Se etter drivere"), "drivere");
                }
                catch { }

                try
                {
                    string wold = Util.Expand("%SystemDrive%\\Windows.old");
                    if (Directory.Exists(wold))
                        AddFinding(Theme.Warn, L.T("Windows.old ligger igjen etter en oppgradering"),
                            L.T("Kan slettes under Rydding"), "rydding");
                }
                catch { }

                foreach (DiskInfo d in MaintenanceTools.PhysicalDisks())
                    if (d.Health != "Frisk" && d.Health != "Ukjent")
                        AddFinding(Theme.Bad, L.F("Disken {0} melder «{1}»", d.Name, L.T(d.Health)),
                            L.T("Ta sikkerhetskopi nå"), "vedlikehold");

                if (lvFindings.Items.Count == 0)
                {
                    ListViewItem li = new ListViewItem("● " + L.T("Ingenting å påpeke."));
                    li.SubItems.Add(junkFound < 0 ? L.T("Kjør en sjekk for å måle søppelfiler") : "");
                    li.ForeColor = Theme.Good;
                    lvFindings.Items.Add(li);
                }
                lvFindings.EndUpdate();
            }
            catch (Exception ex) { Status(L.T("Kunne ikke lese systemtall: ") + ex.Message); }
        }

        async Task FullScan()
        {
            Status(L.T("Måler …"));
            long total = 0;
            List<CleanTarget> targets = Cleaner.BuildTargets();
            CancellationTokenSource c = new CancellationTokenSource();
            try
            {
                await Task.Run(delegate
                {
                    foreach (CleanTarget t in targets)
                    {
                        Status(L.T(t.Name));
                        Cleaner.Scan(t, c.Token, null);
                        total += t.FoundBytes;
                    }
                });
                junkFound = total;
                RefreshOverview();
                Status(L.F("{0} kan ryddes bort.", Util.Bytes(total)));
                Util.Log("Sjekk: " + Util.Bytes(total) + " søppel funnet.");
            }
            catch (Exception ex) { Status(L.T("Avbrutt: ") + ex.Message); }
        }

        // ==============================================================
        //  LOGG
        // ==============================================================
        TextBox logBox;

        Panel PageLog()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            Panel bar = Row(50);
            FlatBtn open = new FlatBtn(L.T("Åpne loggfil"));
            open.Width = 130; open.Location = new Point(0, 8);
            open.Click += delegate { Util.OpenPath(Util.LogPath); };
            FlatBtn clear = new FlatBtn(L.T("Tøm visning"));
            clear.Width = 120; clear.Location = new Point(142, 8);
            clear.Click += delegate { logBox.Clear(); };
            bar.Controls.Add(open);
            bar.Controls.Add(clear);

            logBox = new TextBox();
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Dock = DockStyle.Fill;
            logBox.BackColor = Theme.Card;
            logBox.ForeColor = Theme.Text;
            logBox.BorderStyle = BorderStyle.None;
            logBox.Font = Theme.FMono;
            try
            {
                if (File.Exists(Util.LogPath))
                {
                    string[] all = File.ReadAllLines(Util.LogPath);
                    int from = Math.Max(0, all.Length - 400);
                    for (int i = from; i < all.Length; i++) logBox.AppendText(all[i] + Environment.NewLine);
                }
            }
            catch { }

            p.Controls.Add(logBox);
            p.Controls.Add(bar);
            logBox.BringToFront();
            return p;
        }

        void OnLogWritten(string line)
        {
            if (logBox == null || logBox.IsDisposed) return;
            if (IsHandleCreated && InvokeRequired)
            {
                BeginInvoke((Action)delegate { OnLogWritten(line); });
                return;
            }
            try
            {
                logBox.AppendText(line + Environment.NewLine);
                logBox.SelectionStart = logBox.TextLength;
                logBox.ScrollToCaret();
            }
            catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Util.LogWritten -= OnLogWritten;
            if (cts != null) { try { cts.Cancel(); } catch { } }
            base.OnFormClosed(e);
        }
    }
}
