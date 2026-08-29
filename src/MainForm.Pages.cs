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
        // Kjører en bakgrunnsjobb og deaktiverer knapper mens den går.
        async Task Job(Control[] toDisable, Action work)
        {
            foreach (Control c in toDisable) c.Enabled = false;
            SetNavEnabled(false);
            Busy(true);
            try { await Task.Run(work); }
            catch (OperationCanceledException) { Status(L.T("Avbrutt.")); }
            catch (Exception ex) { Status(L.T("Feil: ") + ex.Message); Util.Log("Feil: " + ex); }
            finally
            {
                Busy(false);
                foreach (Control c in toDisable) c.Enabled = true;
                SetNavEnabled(true);
            }
        }

        // Kjører noe på UI-tråden så snart meldingsløkken går. Trygt selv om
        // vindushåndtaket ikke er laget ennå — BeginInvoke ville kastet der.
        void Defer(Action a)
        {
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 1;
            t.Tick += delegate
            {
                t.Stop();
                t.Dispose();
                try { a(); }
                catch (Exception ex) { Util.Log("Utsatt kall feilet: " + ex); }
            };
            t.Start();
        }

        static Panel Toolbar(params Control[] cs)
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Top;
            p.Height = 52;
            p.BackColor = Theme.Bg;
            int x = 0;
            foreach (Control c in cs)
            {
                c.Location = new Point(x, c is Label ? 16 : 9);
                if (c is Label) c.AutoSize = true;
                p.Controls.Add(c);
                x += c.Width + 10;
            }
            return p;
        }

        static ListView ListIn(Panel parent, bool checkboxes, params string[] cols)
        {
            ListView lv = Theme.MakeList();
            lv.CheckBoxes = checkboxes;
            lv.Dock = DockStyle.Fill;
            for (int i = 0; i < cols.Length; i += 2)
                lv.Columns.Add(cols[i], int.Parse(cols[i + 1]));
            Panel card = Theme.MakeCard();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(1);
            card.Controls.Add(lv);
            parent.Controls.Add(card);
            return lv;
        }

        static TextBox Console(Panel parent, int height)
        {
            TextBox tb = new TextBox();
            tb.Multiline = true;
            tb.ReadOnly = true;
            tb.ScrollBars = ScrollBars.Vertical;
            tb.Dock = height > 0 ? DockStyle.Bottom : DockStyle.Fill;
            if (height > 0) tb.Height = height;
            tb.BackColor = Color.FromArgb(0x0E, 0x10, 0x14);
            tb.ForeColor = Theme.Muted;
            tb.BorderStyle = BorderStyle.None;
            tb.Font = Theme.FMono;
            parent.Controls.Add(tb);
            return tb;
        }

        void Append(TextBox tb, string line)
        {
            if (tb.IsDisposed) return;
            if (tb.IsHandleCreated && tb.InvokeRequired)
            {
                tb.BeginInvoke((Action)delegate { Append(tb, line); });
                return;
            }
            tb.AppendText(line + Environment.NewLine);
            if (!tb.IsHandleCreated) return;
            tb.SelectionStart = tb.TextLength;
            tb.ScrollToCaret();
        }

        static List<ListViewItem> ItemsSnapshot(ListView lv)
        {
            List<ListViewItem> l = new List<ListViewItem>();
            foreach (ListViewItem li in lv.Items) l.Add(li);
            return l;
        }

        // ==============================================================
        //  RYDDING
        // ==============================================================
        List<CleanTarget> cleanTargets;
        ListView lvClean;
        Label lblCleanTotal, lblCleanInfo;

        Panel PageClean()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            ActionTile tScan = new ActionTile(L.T("Analyser"),
                L.T("Måler hvor mye hver kategori inneholder. Sletter ingenting.")).AsPrimary();
            ActionTile tClean = new ActionTile(L.T("Rens merkede"),
                L.T("Sletter det du har huket av. Analyser først.")).AsDanger();
            ActionTile tAll = new ActionTile(L.T("Merk alle"),
                L.T("Huker av eller vekk alle kategoriene på én gang."));
            tClean.Enabled = false;

            Panel actions = Widgets.Row(98, tScan, tClean, tAll);

            Panel head = Widgets.Head(L.T("Kategorier"), out lblCleanTotal);

            Panel infoBar = new Panel();
            infoBar.Dock = DockStyle.Bottom;
            infoBar.Height = 44;
            infoBar.BackColor = Theme.Bg;
            lblCleanInfo = Theme.Lbl(L.T("Velg en kategori for å se nøyaktig hva som slettes."),
                Theme.FSmall, Theme.Muted);
            lblCleanInfo.AutoSize = false;
            lblCleanInfo.Dock = DockStyle.Fill;
            infoBar.Controls.Add(lblCleanInfo);

            Panel listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.BackColor = Theme.Bg;
            lvClean = ListIn(listHost, true,
                L.T("Kategori"), "300", L.T("Størrelse"), "110", L.T("Filer"), "90", L.T("Merknad"), "420");
            listHost.Controls.Add(head);

            cleanTargets = Cleaner.BuildTargets();
            foreach (CleanTarget t in cleanTargets)
            {
                ListViewItem li = new ListViewItem(L.T(t.Name));
                li.SubItems.Add("—");
                li.SubItems.Add("—");
                li.SubItems.Add(t.Risk == Risk.Merk ? L.T("Les beskrivelsen")
                              : !t.DefaultChecked ? L.T("Av som standard") : "");
                li.Checked = t.DefaultChecked;
                li.Tag = t;
                if (t.Risk == Risk.Merk) li.ForeColor = Theme.Warn;
                else if (!t.DefaultChecked) li.ForeColor = Theme.Muted;
                lvClean.Items.Add(li);
            }
            lvClean.SelectedIndexChanged += delegate
            {
                if (lvClean.SelectedItems.Count == 0) return;
                CleanTarget t = (CleanTarget)lvClean.SelectedItems[0].Tag;
                lblCleanInfo.Text = L.T(t.Info);
                lblCleanInfo.ForeColor = t.Risk == Risk.Merk ? Theme.Warn : Theme.Muted;
            };

            p.Controls.Add(listHost);
            p.Controls.Add(infoBar);
            p.Controls.Add(actions);

            tAll.Click += delegate
            {
                bool anyUnchecked = false;
                foreach (ListViewItem li in lvClean.Items) if (!li.Checked) anyUnchecked = true;
                foreach (ListViewItem li in lvClean.Items) li.Checked = anyUnchecked;
            };

            tScan.Click += async delegate
            {
                cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;
                long total = 0;
                await Job(new Control[] { tScan, tClean, tAll }, delegate
                {
                    foreach (ListViewItem li in ItemsSnapshot(lvClean))
                    {
                        CleanTarget t = (CleanTarget)li.Tag;
                        Status(L.T(t.Name));
                        Cleaner.Scan(t, ct, delegate(string dir) { Status(L.T(t.Name) + " — " + dir); });
                        total += t.FoundBytes;
                        long snapshot = total;
                        BeginInvoke((Action)delegate
                        {
                            li.SubItems[1].Text = t.FoundBytes > 0 ? Util.Bytes(t.FoundBytes) : "—";
                            li.SubItems[2].Text = t.FoundFiles > 0 ? t.FoundFiles.ToString("N0") : "—";
                            lblCleanTotal.Text = L.F("{0} funnet", Util.Bytes(snapshot));
                            lblCleanTotal.ForeColor = Theme.Good;
                        });
                    }
                });
                tClean.Enabled = true;
                tClean.Info = L.T("Sletter det du har huket av.");
                tClean.Invalidate();
                junkFound = total;
                Status(L.F("{0} kan slettes.", Util.Bytes(total)));
                Util.Log("Analyse: " + Util.Bytes(total) + " funnet.");
            };

            tClean.Click += async delegate
            {
                List<CleanTarget> chosen = new List<CleanTarget>();
                bool risky = false;
                foreach (ListViewItem li in lvClean.Items)
                {
                    if (!li.Checked) continue;
                    CleanTarget t = (CleanTarget)li.Tag;
                    chosen.Add(t);
                    if (t.Risk == Risk.Merk) risky = true;
                }
                if (chosen.Count == 0) { Status(L.T("Ingenting er merket.")); return; }

                string msg = L.F("Sletter {0} kategorier. Dine egne filer, passord og bokmerker røres ikke.",
                                 chosen.Count);
                if (risky) msg += "\n\n" + L.T("Én av dem er merket med forbehold. Les beskrivelsen først.");
                if (MessageBox.Show(this, msg + "\n\n" + L.T("Fortsette?"), L.T("Rydding"),
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

                cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;
                long freed = 0; int deleted = 0, skipped = 0;
                await Job(new Control[] { tScan, tClean, tAll }, delegate
                {
                    foreach (ListViewItem li in ItemsSnapshot(lvClean))
                    {
                        if (!li.Checked) continue;
                        CleanTarget t = (CleanTarget)li.Tag;
                        Status(L.T(t.Name));
                        Cleaner.CleanResult r = Cleaner.Clean(t, ct,
                            delegate(string dir) { Status(L.T(t.Name) + " — " + dir); });
                        freed += r.Freed; deleted += r.Deleted; skipped += r.Skipped;
                        BeginInvoke((Action)delegate
                        {
                            li.SubItems[1].Text = "—";
                            li.SubItems[2].Text = r.Deleted.ToString("N0");
                            li.SubItems[3].Text = r.Skipped > 0
                                ? L.F("{0} i bruk", r.Skipped) : L.T("Ferdig");
                            li.ForeColor = Theme.Good;
                        });
                    }
                });
                junkFound = 0;
                lblCleanTotal.Text = L.F("Frigjorde {0}", Util.Bytes(freed));
                lblCleanTotal.ForeColor = Theme.Good;
                Status(L.F("Frigjorde {0}. {1} filer slettet, {2} var i bruk.",
                    Util.Bytes(freed), deleted.ToString("N0"), skipped));
                Util.Log("Rydding: " + Util.Bytes(freed) + " frigjort, " + deleted +
                         " filer, " + skipped + " hoppet over.");
                RefreshOverview();
            };

            return p;
        }

        // ==============================================================
        //  OPPSTART
        // ==============================================================
        ListView lvStart;
        bool showTasks;
        List<BootDelay> bootDelays;
        Label lblBoot;

        Panel PageStartup()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            ActionTile tOff = new ActionTile(L.T("Slå av merkede"),
                L.T("Programmet starter ikke lenger med Windows. Ingenting avinstalleres.")).AsWarn();
            ActionTile tOn = new ActionTile(L.T("Slå på merkede"),
                L.T("Lar det starte med Windows igjen."));
            ActionTile tTasks = new ActionTile(L.T("Vis planlagte oppgaver"),
                L.T("Tar med oppgaver som starter ved pålogging, ikke bare vanlige oppstartsprogrammer."));
            ActionTile tRef = new ActionTile(L.T("Oppdater listen"),
                L.T("Leser oppføringene og henter oppstartstidene på nytt.")).AsPrimary();

            Panel actions = Widgets.Row(98, tRef, tOff, tOn, tTasks);
            Panel head = Widgets.Head(L.T("Starter med Windows"), out lblBoot);

            Panel listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.BackColor = Theme.Bg;
            lvStart = ListIn(listHost, true,
                L.T("Navn"), "200", L.T("Status"), "80", L.T("Sinker oppstart"), "140",
                L.T("Merknad"), "195", L.T("Utgiver"), "165", L.T("Hvor"), "150",
                L.T("Kommando"), "320");
            listHost.Controls.Add(head);

            Panel note = new Panel();
            note.Dock = DockStyle.Bottom;
            note.Height = 30;
            note.BackColor = Theme.Bg;
            Label nl = Theme.Lbl(
                L.T("Huk av det du vil endre, og trykk «Slå av merkede». Tallene kommer fra Windows' egen måling av de siste oppstartene."),
                Theme.FSmall, Theme.Muted);
            nl.AutoSize = false; nl.Dock = DockStyle.Fill;
            note.Controls.Add(nl);

            p.Controls.Add(listHost);
            p.Controls.Add(note);
            p.Controls.Add(actions);

            Control[] btns = new Control[] { tRef, tOff, tOn, tTasks };
            tRef.Click += async delegate { await LoadStartup(btns); };
            tTasks.Click += async delegate
            {
                showTasks = !showTasks;
                tTasks.Title = showTasks ? L.T("Skjul planlagte oppgaver") : L.T("Vis planlagte oppgaver");
                tTasks.Invalidate();
                await LoadStartup(btns);
            };
            tOff.Click += async delegate { await ToggleStartup(false, btns); };
            tOn.Click += async delegate { await ToggleStartup(true, btns); };

            Defer(delegate { Task ignored = LoadStartup(btns); });
            return p;
        }

        async Task LoadStartup(Control[] btns)
        {
            List<StartupItem> items = null;
            List<BootEvent> boots = null;
            bool tasks = showTasks;
            await Job(btns, delegate
            {
                items = StartupTools.Enumerate(tasks);
                if (bootDelays == null) bootDelays = BootTools.Delays(400);
                boots = BootTools.RecentBoots(5);
            });
            if (items == null) return;

            string bootText = "";
            Color bootColor = Theme.Muted;
            if (boots != null && boots.Count > 0)
            {
                long sum = 0;
                foreach (BootEvent b in boots) sum += b.TotalMs;
                long avg = sum / boots.Count;
                bootText = L.F("Oppstart tar {0}", BootTools.Seconds(avg));
                bootColor = avg > 60000 ? Theme.Bad : avg > 30000 ? Theme.Warn : Theme.Good;
            }

            lvStart.BeginUpdate();
            lvStart.Items.Clear();
            int active = 0;
            foreach (StartupItem it in items)
            {
                if (it.Enabled) active++;
                BootDelay bd = BootTools.MatchFor(bootDelays, it);
                ListViewItem li = new ListViewItem(it.Name);
                li.SubItems.Add(it.Enabled ? L.T("På") : L.T("Av"));
                li.SubItems.Add(bd != null ? BootTools.Seconds(bd.AverageMs) : "");
                li.SubItems.Add(it.Critical ? L.T("Behold") + " — " + it.Note : "");
                li.SubItems.Add(it.Publisher);
                li.SubItems.Add(it.KindText);
                li.SubItems.Add(it.Command);
                li.Tag = it;
                li.ForeColor = it.Critical ? Theme.Warn
                             : !it.Enabled ? Theme.Muted
                             : (bd != null && bd.AverageMs > 4000) ? Theme.Bad
                             : (bd != null && bd.AverageMs > 1500) ? Theme.Warn
                             : Theme.Text;
                lvStart.Items.Add(li);
            }
            lvStart.EndUpdate();

            lblBoot.Text = L.F("{0} oppføringer, {1} på", items.Count, active) +
                (bootText.Length > 0 ? "   ·   " + bootText : "");
            lblBoot.ForeColor = bootColor;
            Status("");
        }

        async Task ToggleStartup(bool enable, Control[] btns)
        {
            List<ListViewItem> sel = new List<ListViewItem>();
            foreach (ListViewItem li in lvStart.Items) if (li.Checked) sel.Add(li);
            if (sel.Count == 0) { Status(L.T("Huk av oppføringene du vil endre først.")); return; }

            if (!enable)
            {
                List<string> warn = new List<string>();
                foreach (ListViewItem li in sel)
                {
                    StartupItem si = (StartupItem)li.Tag;
                    if (si.Critical) warn.Add("  • " + si.Name + " — " + si.Note);
                }
                if (warn.Count > 0)
                {
                    string t = L.T("Disse gjør noe du sannsynligvis vil beholde:") + "\n\n" +
                               string.Join("\n", warn.ToArray()) + "\n\n" + L.T("Slå av likevel?");
                    if (MessageBox.Show(this, t, L.T("Oppstart"),
                            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                        return;
                }
            }

            int ok = 0, fail = 0;
            await Job(btns, delegate
            {
                foreach (ListViewItem li in sel)
                {
                    StartupItem it = (StartupItem)li.Tag;
                    Status(it.Name);
                    bool r = StartupTools.SetEnabled(it, enable);
                    if (r) ok++; else fail++;
                    BeginInvoke((Action)delegate
                    {
                        li.SubItems[1].Text = it.Enabled ? L.T("På") : L.T("Av");
                        li.ForeColor = !r ? Theme.Bad
                            : it.Critical ? Theme.Warn
                            : it.Enabled ? Theme.Text : Theme.Muted;
                        li.Checked = false;
                    });
                }
            });
            Status(fail > 0
                ? L.F("Endret {0}. {1} feilet — krever administrator.", ok, fail)
                : L.F("Endret {0}.", ok));
            RefreshOverview();
        }

        // ==============================================================
        //  MINNE
        // ==============================================================
        Label mTotal, mUsed, mAvail, mStandby, mProcCount;
        Bar mBar;
        ListView lvProc;
        System.Windows.Forms.Timer memTimer;

        Panel PageMemory()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 130;
            top.BackColor = Theme.Bg;
            top.Padding = new Padding(0, 0, 0, 12);
            Panel card = Theme.MakeCard();
            card.Dock = DockStyle.Fill;
            mUsed = Theme.Lbl("—", Theme.FBig, Theme.Text); mUsed.Location = new Point(18, 16);
            mTotal = Theme.Lbl("", Theme.F, Theme.Muted); mTotal.Location = new Point(21, 62);
            mAvail = Theme.Lbl("", Theme.F, Theme.Muted); mAvail.Location = new Point(340, 22);
            mStandby = Theme.Lbl("", Theme.F, Theme.Muted); mStandby.Location = new Point(340, 46);
            mBar = new Bar();
            mBar.Location = new Point(21, 90);
            mBar.Width = 640;
            mBar.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            card.Controls.Add(mUsed); card.Controls.Add(mTotal);
            card.Controls.Add(mAvail); card.Controls.Add(mStandby); card.Controls.Add(mBar);
            top.Controls.Add(card);

            ActionTile tRef = new ActionTile(L.T("Oppdater"),
                L.T("Leser minnetallene på nytt.")).AsPrimary();
            ActionTile tTrim = new ActionTile(L.T("Frigjør arbeidssett"),
                L.T("Dytter data fra RAM til disk. Tallet faller, men programmene leser det inn igjen."));
            ActionTile tStandby = new ActionTile(L.T("Tøm standby-cache"),
                L.T("Sletter Windows sin filcache. Kan hjelpe rett før et stort spill. Ellers gjør den maskinen tregere en stund."));

            Panel actions = Widgets.Row(98, tRef, tTrim, tStandby);
            Panel head = Widgets.Head(L.T("Hva som bruker minnet"), out mProcCount);

            Panel note = new Panel();
            note.Dock = DockStyle.Bottom;
            note.Height = 30;
            note.BackColor = Theme.Bg;
            Label nl = Theme.Lbl(
                L.T("Windows bruker ledig RAM som cache med vilje. Vil du ha varig lavere forbruk: kutt oppstartsprogrammer."),
                Theme.FSmall, Theme.Muted);
            nl.AutoSize = false; nl.Dock = DockStyle.Fill;
            note.Controls.Add(nl);

            Panel listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.BackColor = Theme.Bg;
            lvProc = ListIn(listHost, false,
                L.T("Program"), "300", L.T("Minnebruk"), "130", L.T("Prosesser"), "100", L.T("Andel"), "160");
            listHost.Controls.Add(head);

            p.Controls.Add(listHost);
            p.Controls.Add(note);
            p.Controls.Add(actions);
            p.Controls.Add(top);

            tRef.Click += delegate { RefreshMemory(); };
            tTrim.Click += async delegate
            {
                await Job(new Control[] { tRef, tTrim, tStandby }, delegate { MemoryTools.TrimAll(); });
                RefreshMemory();
                Status(L.T("Arbeidssett frigjort."));
            };
            tStandby.Click += async delegate
            {
                if (!Util.IsAdmin()) { Status(L.T("Krever administrator.")); return; }
                MemSnapshot before = MemoryTools.Snapshot();
                bool ok = false;
                await Job(new Control[] { tRef, tTrim, tStandby },
                    delegate { ok = MemoryTools.PurgeStandby(); });
                MemSnapshot after = MemoryTools.Snapshot();
                RefreshMemory();
                if (ok)
                {
                    long diff = (long)after.AvailPhys - (long)before.AvailPhys;
                    Status(L.F("Tilgjengelig minne endret seg med {0}{1}.",
                        diff >= 0 ? "+" : "−", Util.Bytes(Math.Abs(diff))));
                }
                else Status(L.T("Klarte ikke tømme standby-cachen."));
            };

            memTimer = new System.Windows.Forms.Timer();
            memTimer.Interval = 4000;
            memTimer.Tick += delegate { if (current == "minne" && Visible) RefreshMemory(); };
            memTimer.Start();

            RefreshMemory();
            return p;
        }

        void RefreshMemory()
        {
            try
            {
                MemSnapshot m = MemoryTools.Snapshot();
                mUsed.Text = Util.Bytes(m.UsedPhys);
                mTotal.Text = L.F("av {0}  ·  {1} %", Util.Bytes(m.TotalPhys), m.LoadPercent);
                mAvail.Text = L.F("Tilgjengelig: {0}", Util.Bytes(m.AvailPhys));
                mStandby.Text = L.F("Standby-cache: {0}", Util.Bytes(m.Standby));
                mBar.Value = m.LoadPercent / 100.0;
                mBar.Fill = m.LoadPercent > 88 ? Theme.Bad : m.LoadPercent > 70 ? Theme.Warn : Theme.Good;
                mBar.Invalidate();

                List<ProcMem> top = MemoryTools.TopProcesses(20);
                lvProc.BeginUpdate();
                lvProc.Items.Clear();
                foreach (ProcMem pm in top)
                {
                    double share = m.TotalPhys > 0 ? (double)pm.Bytes / m.TotalPhys : 0;
                    ListViewItem li = new ListViewItem(pm.Name);
                    li.SubItems.Add(Util.Bytes(pm.Bytes));
                    li.SubItems.Add(pm.Count.ToString());
                    li.SubItems.Add((share * 100).ToString("0.0") + " %");
                    lvProc.Items.Add(li);
                }
                lvProc.EndUpdate();
                if (mProcCount != null)
                    mProcCount.Text = L.F("{0} programmer", top.Count);
            }
            catch { }
        }

        // ==============================================================
        //  VEDLIKEHOLD
        // ==============================================================
        TextBox maintOut;
        ActionTile tileAuto;

        Panel PageMaint()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            // --- reparasjon ---
            ActionTile tSfc = new ActionTile(L.T("Sjekk systemfiler"),
                L.T("sfc /scannow finner og reparerer ødelagte Windows-filer. Tar 5–15 minutter.")).AsPrimary();
            ActionTile tDism = new ActionTile(L.T("Reparer Windows-image"),
                L.T("DISM reparerer kilden sfc henter friske filer fra. Kjør denne først hvis sfc feiler."));
            ActionTile tRp = new ActionTile(L.T("Gjenopprettingspunkt"),
                L.T("Lager et punkt du kan rulle tilbake til før du endrer noe."));

            // --- disk og nettverk ---
            ActionTile tComp = new ActionTile(L.T("Rydd komponentlager"),
                L.T("Fjerner gamle oppdateringsversjoner i WinSxS. Kan ta lang tid og frigjøre flere GB."));
            ActionTile tOpt = new ActionTile(L.T("Optimaliser disker"),
                L.T("TRIM på SSD, defragmentering på harddisk."));
            ActionTile tDns = new ActionTile(L.T("Tøm DNS-cache"),
                L.T("Hjelper når en nettside peker feil etter en flytting."));

            // --- automatikk og hjelp ---
            ActionTile tPlan = new ActionTile(L.T("Planlagt rydding"),
                L.T("Lar Windows kjøre den trygge ryddingen hver uke av seg selv."));
            ActionTile tRap = new ActionTile(L.T("Systemrapport"),
                L.T("Lagrer en tekstfil på skrivebordet du kan sende til den som hjelper deg."));
            tileAuto = new ActionTile(L.T("Se etter oppdatering"),
                L.T("Henter versjonsfilen og sjekker nedlastingen mot sha256 før noe kjøres."));
            ActionTile tLog = new ActionTile(L.T("Vis logg"),
                L.T("Alt programmet har gjort, med tidspunkt."));

            Panel r1 = Widgets.Row(98, tSfc, tDism, tRp);
            Panel r2 = Widgets.Row(98, tComp, tOpt, tDns);
            Panel r3 = Widgets.Row(98, tPlan, tRap, tileAuto, tLog);

            Label outCount;
            Panel head = Widgets.Head(L.T("Utdata"), out outCount);
            outCount.Text = Util.IsAdmin() ? "" : L.T("Uten administrator vil de fleste av disse feile.");
            outCount.ForeColor = Theme.Warn;

            Panel outHost = new Panel();
            outHost.Dock = DockStyle.Fill;
            outHost.BackColor = Theme.Bg;
            maintOut = Console(outHost, 0);
            outHost.Controls.Add(head);

            p.Controls.Add(outHost);
            p.Controls.Add(r3);
            p.Controls.Add(r2);
            p.Controls.Add(r1);

            Control[] all = new Control[] { tSfc, tDism, tRp, tComp, tOpt, tDns, tPlan, tRap, tileAuto };
            Action<string> w = delegate(string l) { Append(maintOut, l); };

            tRp.Click += async delegate { await Job(all, delegate { MaintenanceTools.CreateRestorePoint(w); }); };
            tSfc.Click += async delegate { await Job(all, delegate { MaintenanceTools.RunSfc(w); }); };
            tDism.Click += async delegate { await Job(all, delegate { MaintenanceTools.RunDismRestore(w); }); };
            tComp.Click += async delegate
            {
                await Job(all, delegate { MaintenanceTools.RunComponentCleanup(w); });
                RefreshOverview();
            };
            tOpt.Click += async delegate { await Job(all, delegate { MaintenanceTools.OptimizeDrives(w); }); };
            tDns.Click += async delegate { await Job(all, delegate { MaintenanceTools.FlushDns(w); }); };
            tLog.Click += delegate { Show("logg"); };

            tileAuto.Click += async delegate
            {
                tileAuto.Enabled = false;
                await CheckForUpdates(true);
                tileAuto.Enabled = true;
            };

            tPlan.Click += async delegate
            {
                bool exists = false;
                await Job(all, delegate { exists = ScheduleTools.Exists(); });
                if (exists)
                {
                    if (MessageBox.Show(this, L.T("Ukentlig rydding er allerede satt opp. Fjerne den?"),
                            L.T("Planlagt rydding"), MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question) != DialogResult.Yes) return;
                    await Job(all, delegate { ScheduleTools.Remove(w); });
                    Status(L.T("Fjernet."));
                }
                else
                {
                    if (MessageBox.Show(this,
                            L.T("Kjøre den trygge ryddingen hver søndag kl. 12? Windows.old, krasjdumper og nettleser-cache tas aldri."),
                            L.T("Planlagt rydding"), MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question) != DialogResult.Yes) return;
                    bool ok = false;
                    await Job(all, delegate { ok = ScheduleTools.Create("SUN", "12:00", w); });
                    Status(ok ? L.T("Satt opp: hver søndag kl. 12.") : L.T("Klarte ikke opprette oppgaven."));
                }
            };

            tRap.Click += async delegate
            {
                string text = null;
                await Job(all, delegate { text = Report.Build(); });
                if (text == null) return;
                SaveReport(text);
            };

            Defer(delegate
            {
                Append(maintOut, L.T("Velg en handling over. Utdataene fra Windows vises her."));
            });
            return p;
        }
    }
}

