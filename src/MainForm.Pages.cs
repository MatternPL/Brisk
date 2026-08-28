using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vaktmester
{
    public partial class MainForm
    {
        // Felles: kjør en bakgrunnsjobb og deaktiver knapper mens den går.
        async Task Job(Control[] toDisable, Action work)
        {
            foreach (Control c in toDisable) c.Enabled = false;
            SetNavEnabled(false);
            try { await Task.Run(work); }
            catch (OperationCanceledException) { Status("Avbrutt."); }
            catch (Exception ex) { Status("Feil: " + ex.Message); Util.Log("Feil: " + ex); }
            finally
            {
                foreach (Control c in toDisable) c.Enabled = true;
                SetNavEnabled(true);
            }
        }

        // Kjorer noe pa UI-traden sa snart meldingslokken gar. Trygt selv om
        // vindushandtaket ikke er laget enda (BeginInvoke ville kastet der).
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

            FlatBtn bScan = new FlatBtn("Analyser"); bScan.Primary(); bScan.Width = 120;
            FlatBtn bClean = new FlatBtn("Rens valgte"); bClean.Width = 130; bClean.Enabled = false;
            FlatBtn bAll = new FlatBtn("Merk alle"); bAll.Width = 110;
            lblCleanTotal = Theme.Lbl("Ikke analysert ennå.", Theme.FBold, Theme.Muted);
            lblCleanTotal.Width = 320;
            Panel bar = Toolbar(bScan, bClean, bAll, lblCleanTotal);

            Panel infoBar = new Panel();
            infoBar.Dock = DockStyle.Bottom;
            infoBar.Height = 46;
            infoBar.BackColor = Theme.Bg;
            lblCleanInfo = Theme.Lbl("Velg en kategori for å se hva den inneholder.", Theme.FSmall, Theme.Muted);
            lblCleanInfo.AutoSize = false;
            lblCleanInfo.Dock = DockStyle.Fill;
            infoBar.Controls.Add(lblCleanInfo);

            Panel listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.BackColor = Theme.Bg;
            lvClean = ListIn(listHost, true,
                "Kategori", "300", "Størrelse", "100", "Filer", "80", "Merknad", "420");

            cleanTargets = Cleaner.BuildTargets();
            foreach (CleanTarget t in cleanTargets)
            {
                ListViewItem li = new ListViewItem(t.Name);
                li.SubItems.Add("—");
                li.SubItems.Add("—");
                li.SubItems.Add(t.Risk == Risk.Merk ? "Les beskrivelsen først" : "Trygt");
                li.Checked = t.DefaultChecked;
                li.Tag = t;
                if (t.Risk == Risk.Merk) li.ForeColor = Theme.Warn;
                lvClean.Items.Add(li);
            }
            lvClean.SelectedIndexChanged += delegate
            {
                if (lvClean.SelectedItems.Count == 0) return;
                CleanTarget t = (CleanTarget)lvClean.SelectedItems[0].Tag;
                lblCleanInfo.Text = t.Info;
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
                        Status("Analyserer: " + t.Name);
                        Cleaner.Scan(t, ct, delegate(string dir) { Status("Analyserer: " + t.Name + " — " + dir); });
                        total += t.FoundBytes;
                        long snapshot = total;
                        BeginInvoke((Action)delegate
                        {
                            li.SubItems[1].Text = t.FoundBytes > 0 ? Util.Bytes(t.FoundBytes) : "—";
                            li.SubItems[2].Text = t.FoundFiles > 0 ? t.FoundFiles.ToString("N0") : "—";
                            lblCleanTotal.Text = "Funnet: " + Util.Bytes(snapshot);
                            lblCleanTotal.ForeColor = Theme.Good;
                        });
                    }
                });
                bClean.Enabled = true;
                Status("Analyse ferdig. " + Util.Bytes(total) + " kan slettes.");
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
                if (chosen.Count == 0) { Status("Ingenting er merket."); return; }

                string msg = "Sletter " + chosen.Count + " kategorier.\n\n" +
                             "Dine egne dokumenter, bilder, nedlastinger, passord og bokmerker røres ikke.";
                if (risky)
                    msg += "\n\nOBS: Du har merket en kategori med forbehold — les beskrivelsen. " +
                           "Sletting av Windows.old fjerner muligheten til å rulle tilbake Windows.";
                if (MessageBox.Show(this, msg + "\n\nFortsette?", "Bekreft rydding",
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
                        Status("Rydder: " + t.Name);
                        Cleaner.CleanResult r = Cleaner.Clean(t, ct,
                            delegate(string dir) { Status("Rydder: " + t.Name + " — " + dir); });
                        freed += r.Freed; deleted += r.Deleted; skipped += r.Skipped;
                        BeginInvoke((Action)delegate
                        {
                            li.SubItems[1].Text = "renset";
                            li.SubItems[2].Text = r.Deleted.ToString("N0");
                            li.SubItems[3].Text = r.Skipped > 0 ? r.Skipped + " i bruk, hoppet over" : "OK";
                            li.ForeColor = Theme.Good;
                        });
                    }
                });
                lblCleanTotal.Text = "Frigjort: " + Util.Bytes(freed);
                lblCleanTotal.ForeColor = Theme.Good;
                Status("Ferdig. Frigjorde " + Util.Bytes(freed) + " (" + deleted.ToString("N0") +
                       " filer slettet, " + skipped + " var i bruk).");
                Util.Log("Rydding fullført: " + Util.Bytes(freed) + " frigjort, " + deleted +
                         " filer slettet, " + skipped + " hoppet over.");
                RefreshOverview();
            };

            return p;
        }

        static List<ListViewItem> ItemsSnapshot(ListView lv)
        {
            List<ListViewItem> l = new List<ListViewItem>();
            foreach (ListViewItem li in lv.Items) l.Add(li);
            return l;
        }

        // ==============================================================
        //  OPPSTART
        // ==============================================================
        ListView lvStart;
        CheckBox chkTasks;

        Panel PageStartup()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            FlatBtn bRef = new FlatBtn("Oppdater liste"); bRef.Width = 140;
            FlatBtn bOff = new FlatBtn("Deaktiver merkede"); bOff.Danger(); bOff.Width = 165;
            FlatBtn bOn = new FlatBtn("Aktiver merkede"); bOn.Width = 145;
            chkTasks = new CheckBox();
            chkTasks.Text = "Vis planlagte oppgaver";
            chkTasks.ForeColor = Theme.Muted;
            chkTasks.Width = 175;
            chkTasks.Height = 24;
            chkTasks.FlatStyle = FlatStyle.Flat;
            Panel bar = Toolbar(bRef, bOff, bOn, chkTasks);

            Panel note = new Panel();
            note.Dock = DockStyle.Bottom;
            note.Height = 44;
            note.BackColor = Theme.Bg;
            Label nl = Theme.Lbl(
                "Deaktivering er reversibel og bruker samme mekanisme som Oppgavebehandling — programmet " +
                "avinstalleres ikke, det bare slutter å starte automatisk.", Theme.FSmall, Theme.Muted);
            nl.AutoSize = false; nl.Dock = DockStyle.Fill;
            note.Controls.Add(nl);

            Panel listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.BackColor = Theme.Bg;
            lvStart = ListIn(listHost, true,
                "Navn", "205", "Status", "85", "Merknad", "215", "Utgiver", "170",
                "Hvor", "155", "Kommando", "380");

            p.Controls.Add(listHost);
            p.Controls.Add(note);
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
            bool tasks = chkTasks.Checked;
            await Job(btns, delegate
            {
                Status("Leser oppstartsoppføringer …");
                items = StartupTools.Enumerate(tasks);
            });
            if (items == null) return;
            lvStart.BeginUpdate();
            lvStart.Items.Clear();
            foreach (StartupItem it in items)
            {
                ListViewItem li = new ListViewItem(it.Name);
                li.SubItems.Add(it.Enabled ? "Aktiv" : "Deaktivert");
                li.SubItems.Add(it.Critical ? "Behold — " + it.Note : "");
                li.SubItems.Add(it.Publisher);
                li.SubItems.Add(it.KindText);
                li.SubItems.Add(it.Command);
                li.Tag = it;
                li.ForeColor = it.Critical ? Theme.Warn : (it.Enabled ? Theme.Text : Theme.Muted);
                lvStart.Items.Add(li);
            }
            lvStart.EndUpdate();
            int active = 0;
            foreach (StartupItem it in items) if (it.Enabled) active++;
            Status(items.Count + " oppføringer, " + active + " aktive.");
        }

        async Task ToggleStartup(bool enable, Control[] btns)
        {
            List<ListViewItem> sel = new List<ListViewItem>();
            foreach (ListViewItem li in lvStart.Items) if (li.Checked) sel.Add(li);
            if (sel.Count == 0) { Status("Merk av oppføringene du vil endre først."); return; }

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
                    string t = "Disse gjør noe du sannsynligvis vil beholde:\n\n" +
                               string.Join("\n", warn.ToArray()) +
                               "\n\nDeaktiver dem bare hvis du vet hva du gjør. Fortsette likevel?";
                    if (MessageBox.Show(this, t, "Systemnære oppføringer",
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
                    Status((enable ? "Aktiverer " : "Deaktiverer ") + it.Name);
                    bool r = StartupTools.SetEnabled(it, enable);
                    if (r) ok++; else fail++;
                    BeginInvoke((Action)delegate
                    {
                        li.SubItems[1].Text = it.Enabled ? "Aktiv" : "Deaktivert";
                        li.ForeColor = !r ? Theme.Bad
                            : it.Critical ? Theme.Warn
                            : it.Enabled ? Theme.Text : Theme.Muted;
                        li.Checked = false;
                    });
                }
            });
            Status("Endret " + ok + " oppføringer." +
                   (fail > 0 ? " " + fail + " feilet — de krever sannsynligvis administrator." : ""));
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

            FlatBtn bRef = new FlatBtn("Oppdater"); bRef.Width = 110;
            FlatBtn bTrim = new FlatBtn("Frigjør arbeidssett"); bTrim.Width = 170;
            FlatBtn bStandby = new FlatBtn("Tøm standby-cache"); bStandby.Width = 175;
            Panel bar = Toolbar(bRef, bTrim, bStandby);

            Panel note = new Panel();
            note.Dock = DockStyle.Bottom;
            note.Height = 82;
            note.BackColor = Theme.Bg;
            Label nl = Theme.Lbl(
                "Ærlig om disse to knappene:\n" +
                "«Frigjør arbeidssett» dytter data fra RAM til disk. Tallet «i bruk» faller, men programmene " +
                "leser det inn igjen straks du bruker dem — som regel uten reell gevinst.\n" +
                "«Tøm standby-cache» sletter Windows sin filcache. Kan hjelpe rett før du starter et stort spill " +
                "eller en tung render. Ellers gjør den maskinen litt tregere en stund.\n" +
                "Vil du ha varig lavere RAM-bruk: kutt oppstartsprogrammer under Oppstart.",
                Theme.FSmall, Theme.Muted);
            nl.AutoSize = false; nl.Dock = DockStyle.Fill;
            note.Controls.Add(nl);

            Panel listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.BackColor = Theme.Bg;
            lvProc = ListIn(listHost, false,
                "Program", "300", "Minnebruk", "130", "Prosesser", "100", "Andel av RAM", "160");

            p.Controls.Add(listHost);
            p.Controls.Add(note);
            p.Controls.Add(bar);
            p.Controls.Add(top);

            bRef.Click += delegate { RefreshMemory(); };
            bTrim.Click += async delegate
            {
                await Job(new Control[] { bRef, bTrim, bStandby }, delegate
                {
                    Status("Frigjør arbeidssett …");
                    MemoryTools.TrimAll();
                });
                RefreshMemory();
                Status("Arbeidssett frigjort. Se merknaden under — effekten er ofte midlertidig.");
            };
            bStandby.Click += async delegate
            {
                if (!Util.IsAdmin())
                {
                    Status("Tømming av standby-cache krever administrator.");
                    return;
                }
                MemSnapshot before = MemoryTools.Snapshot();
                bool ok = false;
                await Job(new Control[] { bRef, bTrim, bStandby }, delegate
                {
                    Status("Tømmer standby-cache …");
                    ok = MemoryTools.PurgeStandby();
                });
                MemSnapshot after = MemoryTools.Snapshot();
                RefreshMemory();
                if (ok)
                {
                    long diff = (long)after.AvailPhys - (long)before.AvailPhys;
                    Status("Standby-cache tømt. Tilgjengelig minne endret seg med " +
                           (diff >= 0 ? "+" : "-") + Util.Bytes(Math.Abs(diff)) + ".");
                }
                else Status("Klarte ikke tømme standby-cachen.");
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
                mUsed.Text = Util.Bytes(m.UsedPhys) + " i bruk";
                mTotal.Text = "av " + Util.Bytes(m.TotalPhys) + " installert  ·  " + m.LoadPercent + " % belastning";
                mAvail.Text = "Tilgjengelig nå:  " + Util.Bytes(m.AvailPhys);
                mStandby.Text = "Standby-cache:  " + Util.Bytes(m.Standby) +
                                "   (kan gjenbrukes umiddelbart)";
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

            FlatBtn bRp = new FlatBtn("Gjenopprettingspunkt"); bRp.Width = 185;
            FlatBtn bSfc = new FlatBtn("Sjekk systemfiler (sfc)"); bSfc.Width = 190;
            FlatBtn bDism = new FlatBtn("Reparer Windows-image"); bDism.Width = 195;
            Panel bar1 = Toolbar(bRp, bSfc, bDism);

            FlatBtn bComp = new FlatBtn("Rydd komponentlager"); bComp.Width = 185;
            FlatBtn bOpt = new FlatBtn("Optimaliser disker"); bOpt.Width = 165;
            FlatBtn bDns = new FlatBtn("Tøm DNS-cache"); bDns.Width = 150;
            Panel bar2 = Toolbar(bComp, bOpt, bDns);
            bar2.Height = 46;

            FlatBtn bPlan = new FlatBtn("Planlagt rydding …"); bPlan.Width = 175;
            FlatBtn bRap = new FlatBtn("Lag systemrapport"); bRap.Width = 170;
            FlatBtn bLogg = new FlatBtn("Åpne loggmappe"); bLogg.Width = 155;
            FlatBtn bOppd = new FlatBtn("Se etter oppdatering"); bOppd.Width = 185;
            CheckBox chkAuto = new CheckBox();
            chkAuto.Text = "Sjekk automatisk";
            chkAuto.Checked = Updater.AutoCheck;
            chkAuto.ForeColor = Theme.Muted;
            chkAuto.FlatStyle = FlatStyle.Flat;
            chkAuto.Width = 150;
            chkAuto.Height = 24;
            chkAuto.CheckedChanged += delegate
            {
                Updater.AutoCheck = chkAuto.Checked;
                Status(chkAuto.Checked
                    ? "Ser etter oppdateringer høyst én gang i døgnet."
                    : "Automatisk oppdateringssjekk er slått av.");
            };
            Panel bar3 = Toolbar(bPlan, bRap, bLogg, bOppd, chkAuto);
            bar3.Height = 46;

            Panel diskHost = new Panel();
            diskHost.Dock = DockStyle.Top;
            diskHost.Height = 168;
            diskHost.BackColor = Theme.Bg;
            lvDisk = ListIn(diskHost, false,
                "Disk / volum", "330", "Type", "110", "Helse", "120", "Plass", "300");

            Panel outHost = new Panel();
            outHost.Dock = DockStyle.Fill;
            outHost.BackColor = Theme.Bg;
            maintOut = Console(outHost, 0);

            p.Controls.Add(outHost);
            p.Controls.Add(diskHost);
            p.Controls.Add(bar3);
            p.Controls.Add(bar2);
            p.Controls.Add(bar1);

            Control[] all = new Control[] { bRp, bSfc, bDism, bComp, bOpt, bDns, bPlan, bRap };
            Action<string> w = delegate(string l) { Append(maintOut, l); };

            bRp.Click += async delegate { await Job(all, delegate { MaintenanceTools.CreateRestorePoint(w); }); Status("Ferdig."); };
            bSfc.Click += async delegate { await Job(all, delegate { MaintenanceTools.RunSfc(w); }); Status("sfc ferdig."); };
            bDism.Click += async delegate { await Job(all, delegate { MaintenanceTools.RunDismRestore(w); }); Status("DISM ferdig."); };
            bComp.Click += async delegate
            {
                await Job(all, delegate { MaintenanceTools.RunComponentCleanup(w); });
                Status("Komponentlager ryddet."); RefreshOverview(); LoadDisks();
            };
            bOpt.Click += async delegate { await Job(all, delegate { MaintenanceTools.OptimizeDrives(w); }); Status("Diskoptimalisering ferdig."); };
            bDns.Click += async delegate { await Job(all, delegate { MaintenanceTools.FlushDns(w); }); Status("DNS-cache tømt."); };

            bOppd.Click += async delegate
            {
                bOppd.Enabled = false;
                await CheckForUpdates(true);
                bOppd.Enabled = true;
            };

            bLogg.Click += delegate
            {
                try { Util.OpenPath(System.IO.Path.GetDirectoryName(Util.LogPath)); }
                catch { }
            };

            bPlan.Click += async delegate
            {
                bool exists = false;
                await Job(all, delegate { exists = ScheduleTools.Exists(); });
                if (exists)
                {
                    if (MessageBox.Show(this,
                            "Automatisk ukentlig rydding er allerede satt opp.\n\nVil du fjerne den?",
                            "Planlagt rydding", MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question) != DialogResult.Yes) return;
                    await Job(all, delegate { ScheduleTools.Remove(w); });
                    Status("Planlagt rydding er fjernet.");
                }
                else
                {
                    if (MessageBox.Show(this,
                            "Sette opp automatisk rydding hver søndag kl. 12:00?\n\n" +
                            "Den kjører stille i bakgrunnen og tar bare de kategoriene som er merket " +
                            "som trygge — aldri Windows.old.\n\nAlt som skjer havner i loggen.",
                            "Planlagt rydding", MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question) != DialogResult.Yes) return;
                    bool ok = false;
                    await Job(all, delegate { ok = ScheduleTools.Create("SUN", "12:00", w); });
                    Status(ok ? "Automatisk rydding satt opp: hver søndag kl. 12:00."
                              : "Klarte ikke opprette oppgaven — se utdataene under.");
                }
            };

            bRap.Click += async delegate
            {
                string text = null;
                await Job(all, delegate
                {
                    Status("Samler systeminformasjon …");
                    text = Report.Build();
                });
                if (text == null) return;
                string path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "Vaktmester-rapport.txt");
                try
                {
                    System.IO.File.WriteAllText(path, text, System.Text.Encoding.UTF8);
                    Append(maintOut, "Rapport lagret: " + path);
                    Status("Rapport lagret på skrivebordet.");
                    Util.OpenPath(path);
                }
                catch (Exception ex) { Status("Kunne ikke lagre rapporten: " + ex.Message); }
            };

            LoadDisks();
            Append(maintOut, "Hva knappene gjør:");
            Append(maintOut, "  Gjenopprettingspunkt  – lager et tilbakerullingspunkt før du endrer noe.");
            Append(maintOut, "  Sjekk systemfiler     – sfc /scannow finner og reparerer ødelagte systemfiler.");
            Append(maintOut, "  Reparer Windows-image – DISM /RestoreHealth fikser kilden sfc reparerer fra.");
            Append(maintOut, "  Rydd komponentlager   – fjerner gamle oppdateringsversjoner i WinSxS (kan ta lang tid).");
            Append(maintOut, "  Optimaliser disker    – TRIM på SSD, defragmentering på harddisk.");
            Append(maintOut, "  Planlagt rydding      – lar Windows kjøre den trygge ryddingen ukentlig av seg selv.");
            Append(maintOut, "  Systemrapport         – lagrer en tekstfil du kan sende til den som hjelper deg.");
            Append(maintOut, "  Se etter oppdatering  – henter versjonsfilen fra oppdateringskilden og");
            Append(maintOut, "                          sjekker nedlastingen mot sha256 før den kjøres.");
            Append(maintOut, "");
            if (!Util.IsAdmin())
                Append(maintOut, "MERK: uten administrator vil de fleste av disse feile.");
            return p;
        }

        void LoadDisks()
        {
            try
            {
                lvDisk.Items.Clear();
                foreach (DiskInfo d in MaintenanceTools.PhysicalDisks())
                {
                    ListViewItem li = new ListViewItem(d.Name);
                    li.SubItems.Add(d.Media);
                    li.SubItems.Add(d.Health);
                    li.SubItems.Add(Util.Bytes(d.Size));
                    li.ForeColor = d.Health == "Frisk" ? Theme.Good : Theme.Bad;
                    lvDisk.Items.Add(li);
                }
                foreach (VolumeInfo v in MaintenanceTools.Volumes())
                {
                    double freePct = v.Total > 0 ? (double)v.Free / v.Total : 0;
                    ListViewItem li = new ListViewItem("  " + v.Letter +
                        (string.IsNullOrEmpty(v.Label) ? "" : " (" + v.Label + ")"));
                    li.SubItems.Add("Volum");
                    li.SubItems.Add(freePct < 0.1 ? "Lite plass" : "OK");
                    li.SubItems.Add(Util.Bytes(v.Free) + " ledig av " + Util.Bytes(v.Total));
                    li.ForeColor = freePct < 0.1 ? Theme.Warn : Theme.Muted;
                    lvDisk.Items.Add(li);
                }
            }
            catch { }
        }
    }
}
