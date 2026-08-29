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

            FlatBtn bScan = new FlatBtn(L.T("Analyser")); bScan.Primary(); bScan.Width = 120;
            FlatBtn bClean = new FlatBtn(L.T("Rens")); bClean.Width = 110; bClean.Enabled = false;
            FlatBtn bAll = new FlatBtn(L.T("Merk alle")); bAll.Width = 110;
            lblCleanTotal = Theme.Lbl("", Theme.FBold, Theme.Muted);
            lblCleanTotal.Width = 320;
            Panel bar = Toolbar(bScan, bClean, bAll, lblCleanTotal);
            Tip(bScan, "Måler hvor mye hver kategori inneholder. Sletter ingenting.");

            Panel infoBar = new Panel();
            infoBar.Dock = DockStyle.Bottom;
            infoBar.Height = 40;
            infoBar.BackColor = Theme.Bg;
            lblCleanInfo = Theme.Lbl("", Theme.FSmall, Theme.Muted);
            lblCleanInfo.AutoSize = false;
            lblCleanInfo.Dock = DockStyle.Fill;
            infoBar.Controls.Add(lblCleanInfo);

            Panel listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.BackColor = Theme.Bg;
            lvClean = ListIn(listHost, true,
                L.T("Kategori"), "300", L.T("Størrelse"), "100", L.T("Filer"), "80", L.T("Merknad"), "420");

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
            p.Controls.Add(bar);

            bAll.Click += delegate
            {
                bool anyUnchecked = false;
                foreach (ListViewItem li in lvClean.Items) if (!li.Checked) anyUnchecked = true;
                foreach (ListViewItem li in lvClean.Items) li.Checked = anyUnchecked;
            };

            bScan.Click += async delegate
            {
                cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;
                long total = 0;
                await Job(new Control[] { bScan, bClean, bAll }, delegate
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
                            lblCleanTotal.Text = Util.Bytes(snapshot);
                            lblCleanTotal.ForeColor = Theme.Good;
                        });
                    }
                });
                bClean.Enabled = true;
                junkFound = total;
                Status(L.F("{0} kan slettes.", Util.Bytes(total)));
                Util.Log("Analyse: " + Util.Bytes(total) + " funnet.");
            };

            bClean.Click += async delegate
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
                await Job(new Control[] { bScan, bClean, bAll }, delegate
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
        CheckBox chkTasks;
        List<BootDelay> bootDelays;
        Label lblBoot;

        Panel PageStartup()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            FlatBtn bRef = new FlatBtn(L.T("Oppdater")); bRef.Width = 120;
            FlatBtn bOff = new FlatBtn(L.T("Slå av")); bOff.Danger(); bOff.Width = 110;
            FlatBtn bOn = new FlatBtn(L.T("Slå på")); bOn.Width = 110;
            chkTasks = new CheckBox();
            chkTasks.Text = L.T("Planlagte oppgaver");
            chkTasks.ForeColor = Theme.Muted;
            chkTasks.Width = 175;
            chkTasks.Height = 24;
            chkTasks.FlatStyle = FlatStyle.Flat;
            lblBoot = Theme.Lbl("", Theme.FBold, Theme.Muted);
            lblBoot.Width = 380;
            Panel bar = Toolbar(bRef, bOff, bOn, chkTasks, lblBoot);
            Tip(bOff, "Reversibelt. Samme mekanisme som Oppgavebehandling — programmet avinstalleres ikke.");
            Tip(bRef, "Forsinkelsen er hentet fra Windows' egen måling av de siste oppstartene.");

            Panel listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.BackColor = Theme.Bg;
            lvStart = ListIn(listHost, true,
                L.T("Navn"), "200", L.T("Status"), "80", L.T("Sinker oppstart"), "140",
                L.T("Merknad"), "195", L.T("Utgiver"), "165", L.T("Hvor"), "150",
                L.T("Kommando"), "320");

            p.Controls.Add(listHost);
            p.Controls.Add(bar);

            bRef.Click += async delegate { await LoadStartup(new Control[] { bRef, bOff, bOn }); };
            chkTasks.CheckedChanged += async delegate { await LoadStartup(new Control[] { bRef, bOff, bOn }); };
            bOff.Click += async delegate { await ToggleStartup(false, new Control[] { bRef, bOff, bOn }); };
            bOn.Click += async delegate { await ToggleStartup(true, new Control[] { bRef, bOff, bOn }); };

            Defer(delegate { Task ignored = LoadStartup(new Control[] { bRef, bOff, bOn }); });
            return p;
        }

        async Task LoadStartup(Control[] btns)
        {
            List<StartupItem> items = null;
            List<BootEvent> boots = null;
            bool tasks = chkTasks.Checked;
            await Job(btns, delegate
            {
                items = StartupTools.Enumerate(tasks);
                if (bootDelays == null) bootDelays = BootTools.Delays(400);
                boots = BootTools.RecentBoots(5);
            });
            if (items == null) return;

            if (boots != null && boots.Count > 0)
            {
                long sum = 0;
                foreach (BootEvent b in boots) sum += b.TotalMs;
                long avg = sum / boots.Count;
                lblBoot.Text = L.F("Oppstart tar {0}", BootTools.Seconds(avg));
                lblBoot.ForeColor = avg > 60000 ? Theme.Bad : avg > 30000 ? Theme.Warn : Theme.Good;
            }
            else lblBoot.Text = "";

            lvStart.BeginUpdate();
            lvStart.Items.Clear();
            foreach (StartupItem it in items)
            {
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
            int active = 0;
            foreach (StartupItem it in items) if (it.Enabled) active++;
            Status(L.F("{0} oppføringer, {1} på.", items.Count, active));
        }

        async Task ToggleStartup(bool enable, Control[] btns)
        {
            List<ListViewItem> sel = new List<ListViewItem>();
            foreach (ListViewItem li in lvStart.Items) if (li.Checked) sel.Add(li);
            if (sel.Count == 0) { Status(L.T("Merk av det du vil endre først.")); return; }

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
                        li.SubItems[2].ForeColor = li.ForeColor;
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
        Label mTotal, mUsed, mAvail, mStandby;
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
            top.Height = 118;
            top.BackColor = Theme.Bg;
            Panel card = Theme.MakeCard();
            card.Dock = DockStyle.Fill;
            mUsed = Theme.Lbl("—", Theme.FBig, Theme.Text); mUsed.Location = new Point(14, 16);
            mTotal = Theme.Lbl("", Theme.F, Theme.Muted); mTotal.Location = new Point(17, 62);
            mAvail = Theme.Lbl("", Theme.F, Theme.Muted); mAvail.Location = new Point(320, 22);
            mStandby = Theme.Lbl("", Theme.F, Theme.Muted); mStandby.Location = new Point(320, 46);
            mBar = new Bar();
            mBar.Location = new Point(17, 88);
            mBar.Width = 620;
            mBar.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            card.Controls.Add(mUsed); card.Controls.Add(mTotal);
            card.Controls.Add(mAvail); card.Controls.Add(mStandby); card.Controls.Add(mBar);
            top.Controls.Add(card);

            FlatBtn bRef = new FlatBtn(L.T("Oppdater")); bRef.Width = 110;
            FlatBtn bTrim = new FlatBtn(L.T("Frigjør arbeidssett")); bTrim.Width = 170;
            FlatBtn bStandby = new FlatBtn(L.T("Tøm standby-cache")); bStandby.Width = 175;
            Panel bar = Toolbar(bRef, bTrim, bStandby);
            Tip(bTrim, "Dytter data fra RAM til disk. Tallet faller, men programmene leser det inn igjen. Sjelden noen reell gevinst.");
            Tip(bStandby, "Sletter Windows sin filcache. Kan hjelpe rett før et stort spill eller en tung render. Ellers gjør den maskinen tregere en stund.");

            Panel note = new Panel();
            note.Dock = DockStyle.Bottom;
            note.Height = 26;
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

            p.Controls.Add(listHost);
            p.Controls.Add(note);
            p.Controls.Add(bar);
            p.Controls.Add(top);

            bRef.Click += delegate { RefreshMemory(); };
            bTrim.Click += async delegate
            {
                await Job(new Control[] { bRef, bTrim, bStandby }, delegate { MemoryTools.TrimAll(); });
                RefreshMemory();
                Status(L.T("Arbeidssett frigjort."));
            };
            bStandby.Click += async delegate
            {
                if (!Util.IsAdmin()) { Status(L.T("Krever administrator.")); return; }
                MemSnapshot before = MemoryTools.Snapshot();
                bool ok = false;
                await Job(new Control[] { bRef, bTrim, bStandby },
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
            }
            catch { }
        }

        // ==============================================================
        //  VEDLIKEHOLD
        // ==============================================================
        TextBox maintOut;
        ListView lvDisk;

        Panel PageMaint()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            FlatBtn bRp = new FlatBtn(L.T("Gjenopprettingspunkt")); bRp.Width = 185;
            FlatBtn bSfc = new FlatBtn("sfc /scannow"); bSfc.Width = 150;
            FlatBtn bDism = new FlatBtn("DISM /RestoreHealth"); bDism.Width = 190;
            Panel bar1 = Toolbar(bRp, bSfc, bDism);

            FlatBtn bComp = new FlatBtn(L.T("Rydd komponentlager")); bComp.Width = 185;
            FlatBtn bOpt = new FlatBtn(L.T("Optimaliser disker")); bOpt.Width = 165;
            FlatBtn bDns = new FlatBtn(L.T("Tøm DNS-cache")); bDns.Width = 150;
            Panel bar2 = Toolbar(bComp, bOpt, bDns);
            bar2.Height = 46;

            FlatBtn bPlan = new FlatBtn(L.T("Planlagt rydding")); bPlan.Width = 165;
            FlatBtn bRap = new FlatBtn(L.T("Systemrapport")); bRap.Width = 150;
            FlatBtn bLogg = new FlatBtn(L.T("Vis logg")); bLogg.Width = 130;
            FlatBtn bOppd = new FlatBtn(L.T("Se etter oppdatering")); bOppd.Width = 185;
            CheckBox chkAuto = new CheckBox();
            chkAuto.Text = L.T("Automatisk");
            chkAuto.Checked = Updater.AutoCheck;
            chkAuto.ForeColor = Theme.Muted;
            chkAuto.FlatStyle = FlatStyle.Flat;
            chkAuto.Width = 120;
            chkAuto.Height = 24;
            Panel bar3 = Toolbar(bPlan, bRap, bLogg, bOppd, chkAuto);
            bar3.Height = 46;

            Tip(bRp, "Lager et tilbakerullingspunkt før du endrer noe.");
            Tip(bSfc, "Finner og reparerer ødelagte systemfiler. Tar 5–15 minutter.");
            Tip(bDism, "Reparerer kilden sfc henter friske filer fra. Kjør denne først hvis sfc feiler.");
            Tip(bComp, "Fjerner gamle oppdateringsversjoner i WinSxS. Kan ta lang tid og frigjøre flere GB.");
            Tip(bOpt, "TRIM på SSD, defragmentering på harddisk.");
            Tip(bPlan, "Lar Windows kjøre den trygge ryddingen ukentlig av seg selv.");
            Tip(bRap, "Lagrer en tekstfil du kan sende til den som hjelper deg.");
            Tip(bOppd, "Henter versjonsfilen og sjekker nedlastingen mot sha256 før noe kjøres.");

            Panel outHost = new Panel();
            outHost.Dock = DockStyle.Fill;
            outHost.BackColor = Theme.Bg;
            maintOut = Console(outHost, 0);

            p.Controls.Add(outHost);
            p.Controls.Add(bar3);
            p.Controls.Add(bar2);
            p.Controls.Add(bar1);

            Control[] all = new Control[] { bRp, bSfc, bDism, bComp, bOpt, bDns, bPlan, bRap, bOppd };
            Action<string> w = delegate(string l) { Append(maintOut, l); };

            chkAuto.CheckedChanged += delegate { Updater.AutoCheck = chkAuto.Checked; };

            bRp.Click += async delegate { await Job(all, delegate { MaintenanceTools.CreateRestorePoint(w); }); };
            bSfc.Click += async delegate { await Job(all, delegate { MaintenanceTools.RunSfc(w); }); };
            bDism.Click += async delegate { await Job(all, delegate { MaintenanceTools.RunDismRestore(w); }); };
            bComp.Click += async delegate
            {
                await Job(all, delegate { MaintenanceTools.RunComponentCleanup(w); });
                RefreshOverview();
            };
            bOpt.Click += async delegate { await Job(all, delegate { MaintenanceTools.OptimizeDrives(w); }); };
            bDns.Click += async delegate { await Job(all, delegate { MaintenanceTools.FlushDns(w); }); };
            bLogg.Click += delegate { Show("logg"); };
            bOppd.Click += async delegate
            {
                bOppd.Enabled = false;
                await CheckForUpdates(true);
                bOppd.Enabled = true;
            };

            bPlan.Click += async delegate
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
                            L.T("Kjøre den trygge ryddingen hver søndag kl. 12? Windows.old blir aldri tatt."),
                            L.T("Planlagt rydding"), MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question) != DialogResult.Yes) return;
                    bool ok = false;
                    await Job(all, delegate { ok = ScheduleTools.Create("SUN", "12:00", w); });
                    Status(ok ? L.T("Satt opp: hver søndag kl. 12.") : L.T("Klarte ikke opprette oppgaven."));
                }
            };

            bRap.Click += async delegate
            {
                string text = null;
                await Job(all, delegate { text = Report.Build(); });
                if (text == null) return;
                string path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "Brisk-rapport.txt");
                try
                {
                    System.IO.File.WriteAllText(path, text, System.Text.Encoding.UTF8);
                    Append(maintOut, path);
                    Status(L.T("Rapport lagret på skrivebordet."));
                    Util.OpenPath(path);
                }
                catch (Exception ex) { Status(L.T("Kunne ikke lagre rapporten: ") + ex.Message); }
            };

            if (!Util.IsAdmin())
                Append(maintOut, L.T("Uten administrator vil de fleste av disse feile."));
            return p;
        }

    }
}
