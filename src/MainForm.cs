using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Brisk
{
    public partial class MainForm : Form
    {
        Panel side, content, host, statusBar;
        Label lblTitle, lblSubtitle, lblStatus;
        Bar busyBar;
        List<NavBtn> navs = new List<NavBtn>();
        Dictionary<string, Panel> pages = new Dictionary<string, Panel>();
        string current = "";
        CancellationTokenSource cts;
        ToolTip tips = new ToolTip();

        public MainForm() : this("oversikt") { }

        StartupScan startScan;

        public MainForm(string startPage) : this(startPage, null) { }

        public MainForm(string startPage, StartupScan scan)
        {
            Text = "Brisk";
            StartPosition = FormStartPosition.CenterScreen;
            // Aapnes stort nok til at all tekst faar plass i alle kort paa alle
            // sider. Spill-sida er den som krever mest hoyde, med seks kort i
            // to rader pluss omstartsvarselet.
            //
            // Passer det ikke paa skjermen, krympes det til det som er ledig -
            // ellers ville vinduet havnet delvis utenfor paa mindre skjermer.
            //
            // Tittellinja er vaar egen og ligger inne i klientflata, saa den
            // maa legges til i hoyden - ellers ville sidene mistet 38 piksler
            // mot for.
            FormBorderStyle = FormBorderStyle.None;
            Size onsket = new Size(1320, 900 + TitleBar.H);
            Rectangle plass = Screen.FromPoint(Cursor.Position).WorkingArea;
            ClientSize = new Size(
                Math.Min(onsket.Width, plass.Width - 60),
                Math.Min(onsket.Height, plass.Height - 60));

            // Under dette begynner tekst aa bli kuttet, saa lenger ned skal det
            // ikke gaa aa dra vinduet.
            // Forsida har ni kort i tre rader. Under dette blir «Denne maskinen»
            // kuttet paa midten, saa lenger ned skal det ikke gaa aa dra
            // vinduet. Er skjermen lavere enn dette, vinner skjermen - et vindu
            // som ikke faar plass er verre enn et som er trangt.
            MinimumSize = new Size(
                Math.Min(1120, plass.Width),
                Math.Min(900 + TitleBar.H, plass.Height));
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F;
            DoubleBuffered = true;
            Theme.ApplyIcon(this);

            tips.AutoPopDelay = 15000;
            tips.InitialDelay = 400;
            tips.ReshowDelay = 150;

            BuildShell();
            startScan = scan;
            if (scan != null && scan.Junk >= 0)
            {
                junkFound = scan.Junk;
                junkDefault = scan.JunkDefault;
            }

            Show(string.IsNullOrEmpty(startPage) ? "oversikt" : startPage);

            Util.LogWritten += OnLogWritten;
            Load += delegate
            {
                Theme.DarkTitleBar(this);
                Theme.RoundCorners(this);
                // Sjekker ved hver oppstart. Det er én liten tekstfil over nett,
                // hentet paa en egen traad, saa det koster ingenting merkbart -
                // og et vedlikeholdsprogram som selv gaar et doegn uten aa vite
                // at det finnes en ny versjon, er daarlig reklame for seg selv.
                if (Updater.AutoCheck)
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
            content.Padding = new Padding(30, 22, 30, 0);
            Controls.Add(content);

            side = new Panel();
            side.Dock = DockStyle.Left;
            side.Width = 224;
            side.BackColor = Theme.Side;
            Controls.Add(side);

            // Dokking gaar bakfra i z-rekkefolgen: den bakerste plasseres
            // forst og faar hele bredda. Uten SendToBack ville linja startet
            // til hoyre for sidemenyen.
            chrome = new TitleBar(this);
            Controls.Add(chrome);
            chrome.SendToBack();

            statusBar = new Panel();
            statusBar.Dock = DockStyle.Bottom;
            statusBar.Height = 34;
            statusBar.BackColor = Theme.Bg;
            busyBar = new Bar();
            busyBar.Height = 3;
            busyBar.Dock = DockStyle.Top;
            busyBar.Visible = false;
            lblStatus = Theme.Lbl("", Theme.FSmall, Theme.Muted);
            lblStatus.Location = new Point(2, 12);
            lblStatus.AutoSize = false;
            lblStatus.Size = new Size(960, 18);
            lblStatus.AutoEllipsis = true;
            statusBar.Controls.Add(lblStatus);
            statusBar.Controls.Add(busyBar);
            content.Controls.Add(statusBar);

            host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Theme.Bg;
            content.Controls.Add(host);
            host.BringToFront();

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 70;
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
            adminBox.Height = Util.IsAdmin() ? 36 : 74;
            adminBox.BackColor = Theme.Side;
            if (Util.IsAdmin())
            {
                Label ok = Theme.Lbl("● " + L.T("Administrator"), Theme.FSmall, Theme.Good);
                ok.Location = new Point(20, 6);
                adminBox.Controls.Add(ok);
            }
            else
            {
                Label w = Theme.Lbl("● " + L.T("Begrenset tilgang"), Theme.FSmall, Theme.Warn);
                w.Location = new Point(20, 4);
                adminBox.Controls.Add(w);
                FlatBtn b = new FlatBtn(L.T("Kjør som administrator"));
                b.Warn();
                b.Height = 30; b.Width = 184;
                b.Location = new Point(20, 26);
                b.Font = Theme.FSmall;
                b.Click += delegate { if (Util.RelaunchAsAdmin()) Application.Exit(); };
                adminBox.Controls.Add(b);
                Tip(b, "Rydding av systemfiler, drivere og reparasjon krever administrator.");
            }
            side.Controls.Add(adminBox);

            Panel langBox = new Panel();
            langBox.Dock = DockStyle.Bottom;
            langBox.Height = 42;
            langBox.BackColor = Theme.Side;
            FlatBtn bEn = new FlatBtn("English");
            FlatBtn bNo = new FlatBtn("Norsk");
            bEn.Width = 90; bEn.Height = 28; bEn.Location = new Point(20, 6); bEn.Font = Theme.FSmall;
            bNo.Width = 90; bNo.Height = 28; bNo.Location = new Point(114, 6); bNo.Font = Theme.FSmall;
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
            // Sikkerhetsnett: paa en skjerm som er lavere enn minstehoyden vaar
            // skal menypunkter kunne rulles til, ikke bli borte.
            navHost.AutoScroll = true;
            side.Controls.Add(navHost);
            navHost.BringToFront();

            // Tolv like punkter etter hverandre gir ingen antydning om hva som
            // hoerer sammen. Overskriftene deler dem i fire flokker etter hva
            // man er ute etter: plass, fart, tilstand, og aa holde maskina i
            // orden. Oversikt staar alene overst uten overskrift.
            //
            // Omvendt rekkefølge — Dock.Top stabler nedenfra.
            AddNav(navHost, "verktoy", L.T("Verktøy"));
            AddNav(navHost, "vedlikehold", L.T("Vedlikehold"));
            AddNav(navHost, "programmer", L.T("Programvare"));
            AddNav(navHost, "drivere", L.T("Oppdateringer"));
            AddNavHead(navHost, L.T("Oppdater og fiks"));

            AddNav(navHost, "nettverk", L.T("Nettverk"));
            AddNav(navHost, "helse", L.T("Helse"));
            AddNavHead(navHost, L.T("Diagnose"));

            AddNav(navHost, "spill", L.T("Spill"));
            AddNav(navHost, "minne", L.T("Minne"));
            AddNav(navHost, "oppstart", L.T("Oppstart"));
            AddNavHead(navHost, L.T("Ytelse"));

            AddNav(navHost, "diskplass", L.T("Diskplass"));
            AddNav(navHost, "rydding", L.T("Rydding"));
            AddNavHead(navHost, L.T("Plass"));

            AddNav(navHost, "oversikt", L.T("Oversikt"));

            Panel brand = new Panel();
            brand.Dock = DockStyle.Top;
            // Lufta over logoen var beregnet paa at Windows sin tittellinje laa
            // rett over. Naa ligger vaar egen der, og da ble det for mye.
            brand.Height = 78;
            brand.BackColor = Theme.Side;
            brand.Paint += delegate(object s, PaintEventArgs e)
            {
                Logo.Paint(e.Graphics, 20, 8, 40, true);
            };
            Label b1 = Theme.Lbl("Brisk", new Font("Segoe UI Light", 19f), Theme.Text);
            b1.Location = new Point(70, 6);
            Label b2 = Theme.Lbl("v" + Updater.CurrentVersion, Theme.FSmall, Theme.Muted);
            b2.Location = new Point(72, 40);
            brand.Controls.Add(b1);
            brand.Controls.Add(b2);
            navHost.Controls.Add(brand);
        }

        // Overskrift over en flokk i sidemenyen. Store bokstaver og dempet
        // farge, saa den leses som en etikett og ikke som enda et menypunkt.
        static void AddNavHead(Panel parent, string text)
        {
            Label l = Theme.Lbl(text.ToUpperInvariant(), Theme.FSmall,
                Color.FromArgb(0x5E, 0x67, 0x78));
            l.AutoSize = false;
            l.Dock = DockStyle.Top;
            l.Height = 30;
            l.Padding = new Padding(21, 12, 0, 0);
            parent.Controls.Add(l);
        }

        void AddNav(Panel parent, string key, string text)
        {
            NavBtn n = new NavBtn(text);
            n.Key = key;
            n.Click += delegate { Show(key); };
            n.Tag = key;
            parent.Controls.Add(n);
            navs.Add(n);
        }

        public void Show(string key)
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
                case "helse": return L.T("Helse");
                case "spill": return L.T("Spill");
                case "nettverk": return L.T("Nettverk");
                case "drivere": return L.T("Oppdateringer");
                case "programmer": return L.T("Programvare");
                case "vedlikehold": return L.T("Vedlikehold");
                case "verktoy": return L.T("Verktøy");
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
                case "helse": return L.T("Disker, kræsj og batteri.");
                case "spill": return L.T("Det som står i veien for bilder per sekund.");
                case "nettverk": return L.T("Er tilkoblingen som den skal?");
                case "drivere": return L.T("Fra Windows Update.");
                case "programmer": return L.T("Oppdater eller fjern programmer.");
                case "vedlikehold": return L.T("Reparasjon og verktøy.");
                case "verktoy": return L.T("Gode gratisverktøy fra andre.");
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
                case "helse": return PageHealth();
                case "spill": return PageGame();
                case "nettverk": return PageNetwork();
                case "drivere": return PageDrivers();
                case "programmer": return PageApps();
                case "vedlikehold": return PageMaint();
                case "verktoy": return PageTools();
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

        public void Busy(bool on)
        {
            if (IsHandleCreated && InvokeRequired)
            {
                BeginInvoke((Action)delegate { Busy(on); });
                return;
            }
            try { busyBar.Pulse(on); }
            catch { }
            Cursor = on ? Cursors.AppStarting : Cursors.Default;
            if (!on) Status("");
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

        // Overskrift over en liste.
        // Overskrift over en boks. Hoyden inkluderer luft under teksten, ellers
        // klistrer boksen seg rett opp i bokstavene.
        static Label SectionLabel(string text)
        {
            Label l = Theme.Lbl(text, Theme.FBold, Theme.Text);
            l.Dock = DockStyle.Top;
            l.AutoSize = false;
            l.Height = 34;
            l.TextAlign = ContentAlignment.TopLeft;
            return l;
        }

        // ==============================================================
        //  OVERSIKT
        // ==============================================================
        Label ovVerdict, ovVerdictSub;
        Panel heroCard;
        Color heroColor = Theme.Good;
        Label ovRam, ovRamSub, ovDisk, ovDiskSub, ovStart, ovStartSub, ovJunk, ovJunkSub;
        Label ovWear, ovWearSub, ovCrash, ovCrashSub;
        Label ovTemp, ovTempSub, ovGpu, ovGpuSub, ovUp, ovUpSub;
        Control ovCrashCell;
        GpuInfo gpu;
        GpuDriver gpuLatest;

        // Tynn strek mot naboen. Den stopper 10 piksler for hjornene, saa
        // rutenettet ser luftig ut i stedet for som et regneark.
        static void Skille(Panel c, bool hoyre, bool bunn)
        {
            if (!hoyre && !bunn) return;
            c.Paint += delegate(object s, PaintEventArgs e)
            {
                Panel me = (Panel)s;
                using (Pen p = new Pen(Theme.Line))
                {
                    if (hoyre)
                        e.Graphics.DrawLine(p, me.Width - 1, 10, me.Width - 1, me.Height - 10);
                    if (bunn)
                        e.Graphics.DrawLine(p, 10, me.Height - 1, me.Width - 10, me.Height - 1);
                }
            };
        }

        static string Kort(string s, int maks)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= maks ? s : s.Substring(0, maks - 1) + "…";
        }
        Panel ovMachine;
        Bar ovRamBar, ovDiskBar;
        ListView lvFindings;
        FlatBtn btnScan, btnCleanNow;
        int crashUnseen = 0;        // blaaskjermer brukeren ikke har kvittert ut
        long junkFound = -1;        // alt som finnes
        long junkDefault = -1;      // bare det som er huket av som standard

        Panel PageOverview()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            // --- hovedkort ---
            Panel heroHost = new Panel();
            heroHost.Dock = DockStyle.Top;
            heroHost.Height = 116;
            heroHost.BackColor = Theme.Bg;

            heroCard = Theme.MakeCard();
            heroCard.Dock = DockStyle.Fill;
            heroCard.Paint += delegate(object s, PaintEventArgs e)
            {
                using (SolidBrush b = new SolidBrush(heroColor))
                    e.Graphics.FillRectangle(b, 0, 0, 4, heroCard.Height);
            };

            ovVerdict = Theme.Lbl("", new Font("Segoe UI Light", 21f), Theme.Text);
            ovVerdict.Location = new Point(28, 24);
            ovVerdictSub = Theme.Lbl("", Theme.F, Theme.Muted);
            ovVerdictSub.Location = new Point(30, 62);

            btnScan = new FlatBtn(L.T("Sjekk PC-en"));
            btnScan.Primary().Big();
            btnScan.Width = 170;
            btnScan.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Tip(btnScan, "Måler søppelfiler og ser etter ting som er verdt å gjøre noe med. Endrer ingenting.");

            btnCleanNow = new FlatBtn(L.T("Se hva som kan ryddes"));
            btnCleanNow.Primary().Big();
            btnCleanNow.Width = 210;
            btnCleanNow.Visible = false;
            btnCleanNow.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Tip(btnCleanNow, "Åpner Rydding, der du merker av hva som skal slettes. Ingenting slettes herfra.");

            heroCard.Controls.Add(ovVerdict);
            heroCard.Controls.Add(ovVerdictSub);
            heroCard.Controls.Add(btnScan);
            heroCard.Controls.Add(btnCleanNow);
            Theme.Arrange(heroCard, delegate { LayoutHero(); });
            heroHost.Controls.Add(heroCard);

            // --- ni tall ---
            Panel cards = new Panel();
            cards.Dock = DockStyle.Top;
            // Uten mellomrom mellom feltene trengs det mindre hoyde enn da
            // dette var ni bokser hver for seg.
            cards.Height = 14 + 3 * 126;
            cards.BackColor = Theme.Bg;
            cards.Padding = new Padding(0, 14, 0, 0);

            Panel c1 = StatCard(out ovRam, out ovRamSub, L.T("Minne"), out ovRamBar);
            Panel c2 = StatCard(out ovDisk, out ovDiskSub, L.T("Ledig plass"), out ovDiskBar);
            Panel c3 = StatCard(out ovJunk, out ovJunkSub, L.T("Søppel"), null);
            Panel c4 = StatCard(out ovStart, out ovStartSub, L.T("Starter med Windows"), null);
            Panel c5 = StatCard(out ovWear, out ovWearSub, L.T("Diskslitasje"), null);
            Panel c6 = StatCard(out ovTemp, out ovTempSub, L.T("Disktemperatur"), null);
            Panel c7 = StatCard(out ovGpu, out ovGpuSub, L.T("Skjermdriver"), null);
            Panel c8 = StatCard(out ovUp, out ovUpSub, L.T("Oppetid"), null);
            Panel c9 = StatCard(out ovCrash, out ovCrashSub, L.T("Blåskjermer"), null);

            // Ett kort, ni felt. Ni frittstaaende bokser ble et rot av rammer
            // og mellomrom; her er det én ramme rundt det hele og tynne streker
            // mellom feltene, saa oyet leser det som én tabell.
            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.ColumnCount = 3;
            grid.RowCount = 3;
            grid.BackColor = Theme.Card;
            for (int i = 0; i < 3; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));
            for (int i = 0; i < 3; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3f));
            grid.Controls.Add(c1, 0, 0);
            grid.Controls.Add(c2, 1, 0);
            grid.Controls.Add(c3, 2, 0);
            grid.Controls.Add(c4, 0, 1);
            grid.Controls.Add(c5, 1, 1);
            grid.Controls.Add(c6, 2, 1);
            grid.Controls.Add(c7, 0, 2);
            grid.Controls.Add(c8, 1, 2);
            // Blaaskjermfeltet ligger nederst til hoyre og skjules naar det
            // ikke finnes noen kraesj aa vise. Da blir hjornet staaende tomt,
            // og det ser ut som luft - ikke som et hull midt i tabellen.
            ovCrashCell = c9;
            grid.Controls.Add(c9, 2, 2);

            // Strekene maa tegnes i feltene selv. Tegnet paa kortet under ville
            // de blitt dekket av feltene, som ligger oppaa.
            Skille(c1, true, true);   Skille(c2, true, true);   Skille(c3, false, true);
            Skille(c4, true, true);   Skille(c5, true, true);   Skille(c6, false, true);
            Skille(c7, true, false);  Skille(c8, true, false);  Skille(c9, false, false);

            Panel storkort = Theme.MakeCard();
            storkort.Dock = DockStyle.Fill;
            storkort.Padding = new Padding(1);
            storkort.Controls.Add(grid);
            cards.Controls.Add(storkort);

            // --- funn ---
            Panel bunn = new Panel();
            bunn.Dock = DockStyle.Fill;
            bunn.BackColor = Theme.Bg;
            bunn.Padding = new Padding(0, 14, 0, 0);

            TableLayoutPanel to = new TableLayoutPanel();
            to.Dock = DockStyle.Fill;
            to.BackColor = Theme.Bg;
            to.ColumnCount = 2;
            to.RowCount = 1;
            to.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 63f));
            to.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37f));

            Panel listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.BackColor = Theme.Bg;
            listHost.Padding = new Padding(0, 0, 14, 0);
            lvFindings = ListIn(listHost, false, L.T("Funn"), "310", L.T("Hva du kan gjøre"), "215");
            lvFindings.DoubleClick += delegate { OpenFinding(); };
            listHost.Controls.Add(SectionLabel(L.T("Verdt å se på")));

            // Maskinen til hoyre. Fylles i bakgrunnen, se RefreshMachine.
            Panel maskinHost = new Panel();
            maskinHost.Dock = DockStyle.Fill;
            maskinHost.BackColor = Theme.Bg;
            ovMachine = Theme.MakeCard();
            ovMachine.Dock = DockStyle.Fill;
            maskinHost.Controls.Add(ovMachine);
            maskinHost.Controls.Add(SectionLabel(L.T("Denne maskinen")));

            to.Controls.Add(listHost, 0, 0);
            to.Controls.Add(maskinHost, 1, 0);
            bunn.Controls.Add(to);

            p.Controls.Add(bunn);
            p.Controls.Add(cards);
            p.Controls.Add(heroHost);

            btnScan.Click += async delegate { await FullScan(); };
            // Sender til Rydding i stedet for aa slette herfra. Foer gikk den
            // rett paa alt som var merket trygt, uten at brukeren fikk se
            // hva det var eller velge bort noe.
            btnCleanNow.Click += delegate { Show("rydding"); };

            Defer(delegate { LayoutHero(); RefreshOverview(); RefreshMachine(); });
            return p;
        }

        // Henter maskininfo i bakgrunnen og tegner den som etikettpar.
        // WMI-oppslagene tar noen hundre millisekunder hver, saa de skal ikke
        // holde vinduet igjen mens forsida aapnes.
        async void RefreshMachine()
        {
            List<MachineLine> linjer = null;
            try
            {
                await System.Threading.Tasks.Task.Run(delegate { linjer = MachineInfo.Read(); });
            }
            catch (Exception) { return; }
            if (linjer == null || ovMachine == null || ovMachine.IsDisposed) return;

            ovMachine.Controls.Clear();
            int y = 10;
            foreach (MachineLine ml in linjer)
            {
                Label k = Theme.Lbl(L.T(ml.Label), Theme.FSmall, Theme.Muted);
                k.Location = new Point(18, y);
                Label v = Theme.Lbl(ml.Value, Theme.FSmall, Theme.Text);
                v.AutoSize = false;
                v.Location = new Point(18, y + 16);
                v.Height = 18;
                v.Width = Math.Max(120, ovMachine.Width - 36);
                ovMachine.Controls.Add(k);
                ovMachine.Controls.Add(v);
                y += 34;
            }

            Panel kort = ovMachine;
            Theme.Arrange(ovMachine, delegate
            {
                foreach (Control c in kort.Controls)
                    if (c is Label && c.Font == Theme.FSmall && c.ForeColor == Theme.Text)
                        c.Width = Math.Max(120, kort.Width - 36);
            });
        }

        void LayoutHero()
        {
            if (heroCard == null) return;
            int right = heroCard.Width - 28;
            btnScan.Location = new Point(right - btnScan.Width, 36);
            btnCleanNow.Location = new Point(right - btnScan.Width - btnCleanNow.Width - 12, 36);
        }

        void OpenFinding()
        {
            if (lvFindings.SelectedItems.Count == 0) return;
            string key = Convert.ToString(lvFindings.SelectedItems[0].Tag);
            if (!string.IsNullOrEmpty(key)) Show(key);
        }

        // Kortet maa folge bredden sin. Med seks kort paa rad er hvert av dem
        // rundt 110 px, og bade stripa og tekstene rant utenfor da de hadde
        // faste bredder.
        Panel StatCard(out Label big, out Label sub, string caption, out Bar bar)
        {
            // Ingen egen ramme: feltet sitter inne i det store kortet, og
            // strekene mellom feltene tegnes der.
            Panel c = new Panel();
            c.BackColor = Theme.Card;
            c.Dock = DockStyle.Fill;

            Label cap = Theme.Lbl(caption, Theme.FSmall, Theme.Muted);
            cap.Location = new Point(18, 14);
            cap.AutoSize = false;
            cap.Height = 18;
            cap.AutoEllipsis = true;

            big = Theme.Lbl("—", Theme.FBig, Theme.Text);
            big.Location = new Point(15, 32);
            big.AutoSize = false;
            big.Height = 44;
            big.AutoEllipsis = true;

            // Stripa og teksten under den trenger luft mellom seg, og teksten
            // trenger luft ned til kanten av kortet.
            bar = new Bar();
            bar.Location = new Point(18, 84);

            sub = Theme.Lbl("", Theme.FSmall, Theme.Muted);
            sub.Location = new Point(18, 102);
            sub.AutoSize = false;
            sub.Height = 18;
            sub.AutoEllipsis = true;

            Label c2 = cap; Label b2 = big; Label s2 = sub; Bar r2 = bar;
            Theme.Arrange(c, delegate
            {
                int w = Math.Max(40, c.Width - 36);
                c2.Width = w;
                b2.Width = Math.Max(40, c.Width - 30);
                s2.Width = w;
                r2.Width = w;
            });

            c.Controls.Add(cap);
            c.Controls.Add(big);
            c.Controls.Add(sub);
            c.Controls.Add(bar);
            c.Tag = cap;        // overskrifta, for de feltene som bytter innhold
            return c;
        }

        Panel StatCard(out Label big, out Label sub, string caption, Bar ignored)
        {
            Bar b;
            Panel p = StatCard(out big, out sub, caption, out b);
            b.Visible = false;
            sub.Location = new Point(18, 88);
            return p;
        }

        int findingWorst;

        void AddFinding(int level, string text, string action, string page)
        {
            ListViewItem li = new ListViewItem("●  " + text);
            li.SubItems.Add(action);
            li.ForeColor = level >= 2 ? Theme.Bad : level == 1 ? Theme.Warn : Theme.Muted;
            li.Tag = page;
            lvFindings.Items.Add(li);
            if (level > findingWorst) findingWorst = level;
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

                int active, total;
                if (startScan != null && startScan.StartupTotal >= 0)
                {
                    active = startScan.StartupActive;
                    total = startScan.StartupTotal;
                }
                else
                {
                    active = 0; total = 0;
                    foreach (StartupItem it in StartupTools.Enumerate(false))
                    {
                        total++;
                        if (it.Enabled) active++;
                    }
                }
                ovStart.Text = active.ToString();
                ovStartSub.Text = L.F("av {0}", total);

                // Under 10 GB er ikke verdt aa bry seg om. Fargen folger det
                // som faktisk ville blitt ryddet, ikke alt som ligger der - de
                // kategoriene som er av som standard skal ikke gjore tallet rodt.
                long teller = junkDefault >= 0 ? junkDefault : junkFound;
                ovJunk.Text = junkFound >= 0 ? Util.Bytes(junkFound) : "—";
                ovJunk.ForeColor = junkFound < 0 ? Theme.Text
                                 : teller > 10L * 1024 * 1024 * 1024 ? Theme.Warn
                                 : Theme.Good;
                ovJunkSub.Text = junkFound < 0 ? L.T("ikke målt")
                               : junkDefault >= 0 && junkDefault < junkFound
                                 ? L.F("{0} er huket av", Util.Bytes(junkDefault))
                                 : "";

                // Verste disk. Tallet kommer fra disken selv naar den svarer,
                // ellers fra Windows - se NvmeTools.
                int verst = -1;
                string verstNavn = "";
                if (startScan != null && startScan.Wear >= 0)
                {
                    verst = startScan.Wear;
                    verstNavn = startScan.WearDrive;
                }
                else
                {
                    try
                    {
                        foreach (DriveWear d in HealthTools.Drives())
                            if (d.Wear > verst) { verst = d.Wear; verstNavn = d.Name; }
                    }
                    catch (Exception) { }
                }
                if (verst >= 0)
                {
                    ovWear.Text = verst + " %";
                    ovWear.ForeColor = verst >= 80 ? Theme.Bad : verst >= 50 ? Theme.Warn : Theme.Text;
                    ovWearSub.Text = verstNavn.Length > 22 ? verstNavn.Substring(0, 22) : verstNavn;
                }
                else
                {
                    ovWear.Text = "—";
                    ovWearSub.Text = L.T("ikke rapportert");
                }

                // Varmeste disk. Over 60 grader begynner en SSD aa strupe seg
                // selv, over 70 er det verdt aa se paa lufta i kabinettet.
                int varm = -1;
                string varmNavn = "";
                if (startScan != null && startScan.Temp >= 0)
                {
                    varm = startScan.Temp;
                    varmNavn = startScan.TempDrive;
                }
                else
                {
                    try
                    {
                        foreach (DriveWear d in HealthTools.Drives())
                            if (d.Temperature > varm) { varm = d.Temperature; varmNavn = d.Name; }
                    }
                    catch (Exception) { }
                }
                if (varm >= 0)
                {
                    ovTemp.Text = varm + " °C";
                    ovTemp.ForeColor = varm >= 70 ? Theme.Bad : varm >= 60 ? Theme.Warn : Theme.Good;
                    ovTempSub.Text = varmNavn.Length > 22 ? varmNavn.Substring(0, 22) : varmNavn;
                }
                else
                {
                    ovTemp.Text = "—";
                    ovTempSub.Text = L.T("ikke rapportert");
                }

                // Skjermdriver. Tallet er det leverandoren selv kaller
                // versjonen, ikke Windows sitt interne nummer - det er det som
                // staar i utgivelsesnotatene og paa nedlastingssida.
                if (gpu == null)
                    gpu = startScan != null && startScan.Gpu != null ? startScan.Gpu : GpuTools.Read();
                ovGpu.Text = gpu.Installed.Length > 0 ? gpu.Installed : "—";
                ovGpu.ForeColor = Theme.Text;
                ovGpuSub.Text = Kort(gpu.Name, 24);
                if (gpuLatest != null && gpuLatest.Newer)
                {
                    ovGpu.ForeColor = Theme.Warn;
                    ovGpuSub.Text = L.F("{0} er ute", gpuLatest.Version);
                }

                ovUp.Text = MachineInfo.Uptime();
                ovUpSub.Text = L.T("siden forrige omstart");

                // Blaaskjermer siste 30 dager, talt som dumpfiler og ikke som
                // hendelser i systemloggen. Er dumpen ryddet bort, kan Helse
                // ikke vise noe som helst om kraesjet - og da skal ikke forsida
                // sende brukeren dit heller. De to sidene skal fortelle det
                // samme. bsodNy er de som ikke er kvittert ut.
                int bsod = 0, bsodNy = 0;
                if (startScan != null && startScan.BlueScreens >= 0)
                {
                    bsod = startScan.BlueScreens;
                    bsodNy = startScan.BlueScreensNew >= 0 ? startScan.BlueScreensNew : bsod;
                }
                else
                {
                    try
                    {
                        bsod = DumpTools.RecentCount(30);
                        bsodNy = DumpTools.Newest() > HealthTools.CrashesSeenUntil ? bsod : 0;
                    }
                    catch (Exception) { bsod = -1; }
                }
                crashUnseen = bsodNy;

                // Siste feltet er stabilitet. Blaaskjermer er alvorligst og
                // vinner naar det finnes noen aa vise; ellers staar det
                // programkraesj der, saa feltet aldri blir staaende tomt.
                Label kapt = ovCrashCell == null ? null : ovCrashCell.Tag as Label;
                if (bsod > 0)
                {
                    if (kapt != null) kapt.Text = L.T("Blåskjermer");
                    ovCrash.Text = bsod.ToString();
                    ovCrash.ForeColor = bsodNy > 0 ? Theme.Warn : Theme.Good;
                    ovCrashSub.Text = bsodNy == 0 ? L.T("siste 30 dager · sett")
                                                  : L.T("siste 30 dager");
                }
                else
                {
                    int appK = -1;
                    string appVerst = "";
                    if (startScan != null && startScan.AppCrashes >= 0)
                    {
                        appK = startScan.AppCrashes;
                        appVerst = startScan.AppCrashWorst;
                    }
                    else
                    {
                        try
                        {
                            int antall = 0, flest = 0;
                            foreach (AppCrash c in AppCrashTools.Recent(30, 900))
                            {
                                antall += c.Count;
                                if (c.Count > flest) { flest = c.Count; appVerst = c.App; }
                            }
                            appK = antall;
                        }
                        catch (Exception) { }
                    }

                    if (kapt != null) kapt.Text = L.T("Programkræsj");
                    ovCrash.Text = appK >= 0 ? appK.ToString() : "—";
                    ovCrash.ForeColor = appK < 0 ? Theme.Text
                                      : appK > 20 ? Theme.Warn
                                      : appK > 0 ? Theme.Text : Theme.Good;
                    ovCrashSub.Text = appK <= 0 ? L.T("siste 30 dager")
                                    : appVerst.Length > 0
                                      ? L.F("{0} oftest", Kort(appVerst, 20))
                                      : L.T("siste 30 dager");
                }

                // --- funn ---
                findingWorst = 0;
                lvFindings.BeginUpdate();
                lvFindings.Items.Clear();

                if (freePct < 0.15)
                    AddFinding(freePct < 0.08 ? 2 : 1,
                        L.F("Bare {0} ledig på {1}", Util.Bytes(sys.AvailableFreeSpace), sys.Name),
                        L.T("Rydd, eller se hva som tar plassen"), "diskplass");

                long verdtAaRydde = junkDefault >= 0 ? junkDefault : junkFound;
                if (verdtAaRydde > 10L * 1024 * 1024 * 1024)
                    AddFinding(1, L.F("{0} søppelfiler", Util.Bytes(verdtAaRydde)),
                        L.T("Dobbeltklikk for å se dem"), "rydding");

                if (active > 8)
                    AddFinding(1, L.F("{0} programmer starter med Windows", active),
                        L.T("Slå av det du ikke trenger"), "oppstart");

                if (m.LoadPercent > 85)
                    AddFinding(1, L.F("Minnet er {0} % fullt", m.LoadPercent),
                        L.T("Se hva som bruker det"), "minne");

                try
                {
                    List<ProblemDevice> devs = DriverTools.FindProblemDevices();
                    int real = 0;
                    foreach (ProblemDevice d in devs)
                        if (d.ErrorCode != 22 && d.ErrorCode != 45) real++;
                    if (real > 0)
                        AddFinding(2, L.F("{0} enheter melder feil", real),
                            L.T("Se etter drivere"), "drivere");
                }
                catch { }

                try
                {
                    if (Directory.Exists(Util.Expand("%SystemDrive%\\Windows.old")))
                        AddFinding(1, L.T("Windows.old ligger igjen etter en oppgradering"),
                            L.T("Kan slettes under Rydding"), "rydding");
                }
                catch { }

                foreach (DiskInfo d in MaintenanceTools.PhysicalDisks())
                    if (d.Health != "Frisk" && d.Health != "Ukjent")
                        AddFinding(2, L.F("Disken {0} melder «{1}»", d.Name, L.T(d.Health)),
                            L.T("Ta sikkerhetskopi nå"), "helse");

                // Bare de kraesjene brukeren ikke allerede har sett paa. Har han
                // vaert innom Helse og kvittert dem ut, er det ikke noe han
                // trenger aa bli minnet om hver gang han aapner programmet.
                if (crashUnseen > 0)
                    AddFinding(crashUnseen > 2 ? 2 : 1,
                        L.F("{0} blåskjermer siste måned", crashUnseen),
                        L.T("Se detaljene under Helse"), "helse");

                if (lvFindings.Items.Count == 0)
                {
                    ListViewItem li = new ListViewItem("●  " + L.T("Ingenting å påpeke."));
                    li.SubItems.Add(junkFound < 0 ? L.T("Trykk «Sjekk PC-en» for å måle søppelfiler") : "");
                    li.ForeColor = Theme.Good;
                    lvFindings.Items.Add(li);
                }
                lvFindings.EndUpdate();

                SetVerdict();
            }
            catch (Exception ex) { Status(L.T("Kunne ikke lese systemtall: ") + ex.Message); }
        }

        void SetVerdict()
        {
            int n = 0;
            foreach (ListViewItem li in lvFindings.Items)
                if (li.Tag != null) n++;

            if (n == 0)
            {
                heroColor = Theme.Good;
                ovVerdict.ForeColor = Theme.Good;
                ovVerdict.Text = L.T("Alt ser bra ut");
                ovVerdictSub.Text = junkFound < 0
                    ? L.T("Kjør en sjekk for å være sikker.")
                    : L.T("Ingenting trenger oppmerksomhet nå.");
            }
            else
            {
                heroColor = findingWorst >= 2 ? Theme.Bad : Theme.Warn;
                ovVerdict.ForeColor = heroColor;
                ovVerdict.Text = n == 1
                    ? L.T("Én ting er verdt å se på")
                    : L.F("{0} ting er verdt å se på", n);
                ovVerdictSub.Text = L.T("Dobbeltklikk en rad under for å gå dit.");
            }

            btnCleanNow.Visible = junkFound > 50L * 1024 * 1024;
            if (btnCleanNow.Visible)
                btnCleanNow.Text = L.F("Se de {0}", Util.Bytes(junkDefault >= 0 ? junkDefault : junkFound));
            LayoutHero();
            heroCard.Invalidate();
        }

        async Task FullScan()
        {
            long total = 0;
            List<CleanTarget> targets = Cleaner.BuildTargets();
            cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            long standard = 0;
            await Job(new Control[] { btnScan, btnCleanNow }, delegate
            {
                foreach (CleanTarget t in targets)
                {
                    Status(L.T(t.Name));
                    Cleaner.Scan(t, ct, null);
                    total += t.FoundBytes;
                    if (t.DefaultChecked) standard += t.FoundBytes;
                }
            });
            junkFound = total;
            junkDefault = standard;
            RefreshOverview();
            Status(L.F("{0} kan ryddes bort.", Util.Bytes(total)));
            Util.Log("Sjekk: " + Util.Bytes(total) + " søppel funnet.");
        }

        // Rydder bare de trygge kategoriene, med tydelig bekreftelse først.
        // ==============================================================
        //  LOGG
        // ==============================================================
        TextBox logBox;

        Panel PageLog()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            Panel bar = Row(52);
            FlatBtn open = new FlatBtn(L.T("Åpne loggfil"));
            open.Width = 140; open.Location = new Point(0, 8);
            open.Click += delegate { Util.OpenPath(Util.LogPath); };
            FlatBtn clear = new FlatBtn(L.T("Tøm visning"));
            clear.Width = 130; clear.Location = new Point(152, 8);
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
