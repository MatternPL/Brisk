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

            BuildShell();
            Show(string.IsNullOrEmpty(startPage) ? "oversikt" : startPage);

            Util.LogWritten += OnLogWritten;
            Load += delegate
            {
                Theme.DarkTitleBar(this);
                // Sjekker i bakgrunnen, hoyst en gang i dognet, og bare hvis pa.
                if (Updater.AutoCheck &&
                    (DateTime.Now - Updater.LastCheck).TotalHours >= 20)
                {
                    System.Threading.Tasks.Task.Run((Action)delegate
                    {
                        string err;
                        UpdateInfo u = Updater.Check(out err);
                        if (u == null) return;
                        try
                        {
                            BeginInvoke((Action)delegate { ShowUpdateDialog(u); });
                        }
                        catch { }
                    });
                }
            };
        }

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

            // --- innhold: overskrift + verts-panel + statuslinje ---
            statusBar = new Panel();
            statusBar.Dock = DockStyle.Bottom;
            statusBar.Height = 30;
            statusBar.BackColor = Theme.Bg;
            lblStatus = Theme.Lbl("Klar.", Theme.FSmall, Theme.Muted);
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
            adminBox.Height = Util.IsAdmin() ? 58 : 96;
            adminBox.BackColor = Theme.Side;
            adminBox.Padding = new Padding(14, 8, 14, 12);
            if (Util.IsAdmin())
            {
                Label ok = Theme.Lbl("● Kjører som administrator", Theme.FSmall, Theme.Good);
                ok.Location = new Point(16, 14);
                adminBox.Controls.Add(ok);
            }
            else
            {
                Label w = Theme.Lbl("● Begrenset modus", Theme.FSmall, Theme.Warn);
                w.Location = new Point(16, 6);
                adminBox.Controls.Add(w);
                Label w2 = Theme.Lbl("Systemfiler og drivere krever\nadministrator.", Theme.FSmall, Theme.Muted);
                w2.Location = new Point(16, 24);
                adminBox.Controls.Add(w2);
                FlatBtn b = new FlatBtn("Start på nytt som admin");
                b.Dock = DockStyle.Bottom;
                b.Height = 30;
                b.Font = Theme.FSmall;
                b.Click += delegate
                {
                    if (Util.RelaunchAsAdmin()) Application.Exit();
                };
                adminBox.Controls.Add(b);
            }
            side.Controls.Add(adminBox);

            Panel navHost = new Panel();
            navHost.Dock = DockStyle.Fill;
            navHost.BackColor = Theme.Side;
            side.Controls.Add(navHost);
            navHost.BringToFront();

            // Legges i omvendt rekkefølge fordi Dock.Top stabler nedenfra.
            AddNav(navHost, "logg", "Logg");
            AddNav(navHost, "vedlikehold", "Vedlikehold");
            AddNav(navHost, "programmer", "Programvare");
            AddNav(navHost, "drivere", "Oppdateringer");
            AddNav(navHost, "minne", "Minne");
            AddNav(navHost, "oppstart", "Oppstart");
            AddNav(navHost, "diskplass", "Diskplass");
            AddNav(navHost, "rydding", "Rydding");
            AddNav(navHost, "oversikt", "Oversikt");

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
            Label b2 = Theme.Lbl("PC-vedlikehold uten tull", Theme.FSmall, Theme.Muted);
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
                case "oversikt": return "Oversikt";
                case "rydding": return "Rydding";
                case "diskplass": return "Diskplass";
                case "oppstart": return "Oppstart";
                case "minne": return "Minne";
                case "drivere": return "Oppdateringer";
                case "programmer": return "Programvare";
                case "vedlikehold": return "Vedlikehold";
                default: return "Logg";
            }
        }

        static string SubOf(string key)
        {
            switch (key)
            {
                case "oversikt": return "Tilstanden på maskinen akkurat nå.";
                case "rydding": return "Finn og slett filer som bare tar plass.";
                case "diskplass": return "Hvor det er blitt av lagringsplassen.";
                case "oppstart": return "Programmer som starter med Windows.";
                case "minne": return "Hva RAM-en faktisk brukes til.";
                case "drivere": return "Drivere og Windows-oppdateringer rett fra Microsoft.";
                case "programmer": return "Oppdater eller fjern installerte programmer.";
                case "vedlikehold": return "Reparasjon, diskhelse og systemsjekk.";
                default: return "Alt programmet har gjort.";
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

        // Overskriften må også oppdateres når man bytter til en side som alt finnes.
        protected override void OnControlAdded(ControlEventArgs e) { base.OnControlAdded(e); }

        // ==============================================================
        //  Hjelpere
        // ==============================================================
        // Viser oppdateringsdialogen. Kalles bade fra automatisk sjekk og fra knappen.
        public void ShowUpdateDialog(UpdateInfo u)
        {
            try
            {
                using (UpdateDialog d = new UpdateDialog(u)) d.ShowDialog(this);
            }
            catch (Exception ex) { Status("Kunne ikke vise oppdateringen: " + ex.Message); }
        }

        // manual = true gir tilbakemelding ogsa nar alt er oppdatert.
        public async System.Threading.Tasks.Task CheckForUpdates(bool manual)
        {
            UpdateInfo u = null;
            string err = null;
            Status("Ser etter oppdateringer \u2026");
            await System.Threading.Tasks.Task.Run((Action)delegate
            {
                u = Updater.Check(out err);
            });

            if (u != null) { Status("Ny versjon " + u.Version + " er tilgjengelig."); ShowUpdateDialog(u); }
            else if (err != null) Status(err);
            else if (manual) Status("Du har nyeste versjon (" + Updater.CurrentVersion + ").");
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

        Panel PageOverview()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            Panel cards = new Panel();
            cards.Dock = DockStyle.Top;
            cards.Height = 152;
            cards.BackColor = Theme.Bg;

            Panel c1 = StatCard(out ovRam, out ovRamSub, "Minne i bruk", out ovRamBar);
            Panel c2 = StatCard(out ovDisk, out ovDiskSub, "Ledig plass på systemdisken", out ovDiskBar);
            Panel c3 = StatCard(out ovStart, out ovStartSub, "Aktive oppstartsprogrammer", null);
            Panel c4 = StatCard(out ovJunk, out ovJunkSub, "Søppel funnet", null);

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
            FlatBtn scan = new FlatBtn("Kjør full sjekk");
            scan.Primary();
            scan.Width = 160;
            scan.Location = new Point(0, 12);
            FlatBtn refresh = new FlatBtn("Oppdater tall");
            refresh.Width = 130;
            refresh.Location = new Point(172, 12);
            actions.Controls.Add(scan);
            actions.Controls.Add(refresh);

            Panel info = Theme.MakeCard();
            info.Dock = DockStyle.Fill;
            RichTextBox rt = new RichTextBox();
            rt.Dock = DockStyle.Fill;
            rt.BackColor = Theme.Card;
            rt.ForeColor = Theme.Muted;
            rt.BorderStyle = BorderStyle.None;
            rt.ReadOnly = true;
            rt.Font = Theme.F;
            rt.Text =
                "Hva dette programmet faktisk gjør — og ikke gjør\r\n\r\n" +
                "VIRKER:\r\n" +
                "  Rydding — sletter ekte søppelfiler: temp, oppdateringsrester, krasjdumper,\r\n" +
                "  nettleser-cache og logger. Frigjør ofte flere GB. Rører aldri dine egne filer.\r\n\r\n" +
                "  Oppstart — den største reelle hastighetsgevinsten. Færre programmer som starter\r\n" +
                "  med Windows gir raskere oppstart og mindre RAM-bruk hele tiden.\r\n\r\n" +
                "  Drivere — henter drivere fra Microsofts egen katalog via Windows Update.\r\n" +
                "  Signerte og trygge. Gratis, og uten den svindelen «driver updater»-nettsider driver med.\r\n\r\n" +
                "  Vedlikehold — sfc og DISM reparerer faktisk ødelagte systemfiler. TRIM holder SSD-en rask.\r\n\r\n" +
                "ÆRLIG FORBEHOLD:\r\n" +
                "  «Frigjør RAM»-knapper er stort sett bløff i kommersielle verktøy. Windows bruker\r\n" +
                "  ledig RAM som cache med vilje — det er sånn det skal være. Knappene under Minne\r\n" +
                "  gjør noe ekte, men hjelper bare i spesielle tilfeller. De er merket deretter.\r\n\r\n" +
                "  Å «rense registeret» gir ingen målbar hastighetsgevinst. Derfor finnes det ikke her.\r\n\r\n" +
                "  Ingen betalingsmur, ingen abonnement, ingen telemetri. Programmet snakker med\r\n" +
                "  Windows Update, winget, og — hvis automatisk oppdatering står på — oppdateringskilden.\r\n" +
                "  Ingenting om deg eller maskinen din sendes noe sted.\r\n\r\n" +
                "  Oppdaterer seg selv: ser etter ny versjon høyst én gang i døgnet, spør deg først, og\r\n" +
                "  kontrollerer nedlastingen mot en sha256-sum før noe kjøres.";
            info.Controls.Add(rt);

            p.Controls.Add(info);
            p.Controls.Add(Spacer(14));
            p.Controls.Add(actions);
            p.Controls.Add(cards);
            info.BringToFront();

            scan.Click += async delegate
            {
                scan.Enabled = false; refresh.Enabled = false;
                await FullScan();
                scan.Enabled = true; refresh.Enabled = true;
            };
            refresh.Click += delegate { RefreshOverview(); };

            RefreshOverview();
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

        void RefreshOverview()
        {
            try
            {
                MemSnapshot m = MemoryTools.Snapshot();
                ovRam.Text = m.LoadPercent + " %";
                ovRamSub.Text = Util.Bytes(m.UsedPhys) + " av " + Util.Bytes(m.TotalPhys) + " i bruk";
                ovRamBar.Value = m.LoadPercent / 100.0;
                ovRamBar.Fill = m.LoadPercent > 88 ? Theme.Bad : m.LoadPercent > 70 ? Theme.Warn : Theme.Good;
                ovRamBar.Invalidate();

                DriveInfo sys = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                double freePct = (double)sys.AvailableFreeSpace / sys.TotalSize;
                ovDisk.Text = Util.Bytes(sys.AvailableFreeSpace);
                ovDiskSub.Text = "av " + Util.Bytes(sys.TotalSize) + " totalt (" +
                                 (freePct * 100).ToString("0") + " % ledig)";
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
                ovStartSub.Text = "av " + total + " oppføringer";
            }
            catch (Exception ex) { Status("Kunne ikke lese systemtall: " + ex.Message); }
        }

        async Task FullScan()
        {
            RefreshOverview();
            Status("Skanner etter søppelfiler …");
            long total = 0;
            List<CleanTarget> targets = Cleaner.BuildTargets();
            CancellationTokenSource c = new CancellationTokenSource();
            try
            {
                await Task.Run(delegate
                {
                    foreach (CleanTarget t in targets)
                    {
                        Status("Skanner: " + t.Name);
                        Cleaner.Scan(t, c.Token, null);
                        total += t.FoundBytes;
                    }
                });
                ovJunk.Text = Util.Bytes(total);
                ovJunkSub.Text = "kan slettes trygt — se Rydding";
                Status("Full sjekk ferdig. " + Util.Bytes(total) + " kan ryddes bort.");
                Util.Log("Full sjekk: " + Util.Bytes(total) + " søppel funnet.");
            }
            catch (Exception ex) { Status("Skanning avbrutt: " + ex.Message); }
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
            FlatBtn open = new FlatBtn("Åpne loggfil");
            open.Width = 130; open.Location = new Point(0, 8);
            open.Click += delegate { Util.OpenPath(Util.LogPath); };
            FlatBtn clear = new FlatBtn("Tøm visning");
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
