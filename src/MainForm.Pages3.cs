using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Brisk
{
    public partial class MainForm
    {
        // ==============================================================
        //  OPPDATERINGER
        // ==============================================================
        ListView lvUpd, lvDev;
        Label lblGpu, lblUpdSum, lblDevSum;

        Panel PageDrivers()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            ActionTile tSearch = new ActionTile(L.T("Søk"),
                L.T("Spør Windows Update om drivere og systemoppdateringer. Tar gjerne et minutt.")).AsPrimary();
            ActionTile tInst = new ActionTile(L.T("Installer merkede"),
                L.T("Henter og installerer direkte fra Microsoft. Noe krever omstart."));
            ActionTile tDev = new ActionTile(L.T("Enhetsbehandling"),
                L.T("Åpner Windows sitt eget verktøy for maskinvare."));
            ActionTile tWu = new ActionTile(L.T("Windows Update"),
                L.T("Åpner Windows-innstillingene for oppdatering."));
            tInst.Enabled = false;

            Panel actions = Widgets.Row(98, tSearch, tInst, tDev, tWu);

            // --- skjermkort ---
            Panel gpuHost = new Panel();
            gpuHost.Dock = DockStyle.Top;
            gpuHost.Height = 88;
            gpuHost.BackColor = Theme.Bg;
            gpuHost.Padding = new Padding(0, 0, 0, 12);

            Panel gpuCard = Theme.MakeCard();
            gpuCard.Dock = DockStyle.Fill;

            lblGpu = Theme.Lbl("", Theme.FCard, Theme.Text);
            lblGpu.Location = new Point(20, 12);
            Label gpuNote = Theme.Lbl(
                L.T("Windows Update ligger ofte etter for skjermkort. Nyeste driver får du hos produsenten."),
                Theme.FSmall, Theme.Muted);
            gpuNote.Location = new Point(22, 38);

            FlatBtn bGpu = new FlatBtn(L.T("Hent hos produsenten"));
            bGpu.Width = 200; bGpu.Height = 34;
            bGpu.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bGpu.Visible = false;

            gpuCard.Controls.Add(lblGpu);
            gpuCard.Controls.Add(gpuNote);
            gpuCard.Controls.Add(bGpu);
            gpuCard.Resize += delegate
            {
                bGpu.Location = new Point(gpuCard.Width - bGpu.Width - 20, 20);
            };
            gpuHost.Controls.Add(gpuCard);

            Defer(delegate
            {
                try
                {
                    List<GpuInfo> gpus = DriverTools.Graphics();
                    if (gpus.Count == 0) { lblGpu.Text = L.T("Fant ingen skjermkort."); return; }

                    GpuInfo g = gpus[0];
                    string alder = g.AgeDays < 0 ? "" : "   ·   " + L.F("{0} dager gammel", g.AgeDays);
                    lblGpu.Text = g.Name + "   ·   " + L.T("driver") + " " + g.Version + alder;
                    lblGpu.ForeColor = g.AgeDays > 120 ? Theme.Warn : Theme.Text;

                    if (g.Url.Length > 0)
                    {
                        bGpu.Visible = true;
                        bGpu.Text = L.F("Hent hos {0}", g.Vendor);
                        string url = g.Url;
                        bGpu.Click += delegate { Util.OpenPath(url); };
                    }
                    bGpu.Location = new Point(gpuCard.Width - bGpu.Width - 20, 20);
                }
                catch (Exception ex) { lblGpu.Text = ex.Message; }
            });

            // --- enheter med problem ---
            Panel devHost = new Panel();
            devHost.Dock = DockStyle.Bottom;
            devHost.Height = 168;
            devHost.BackColor = Theme.Bg;
            devHost.Padding = new Padding(0, 12, 0, 0);
            lvDev = ListIn(devHost, false,
                L.T("Enhet"), "380", L.T("Problem"), "330", L.T("Enhets-ID"), "420");
            devHost.Controls.Add(Widgets.Head(L.T("Enheter med problem"), out lblDevSum));

            // --- tilgjengelige oppdateringer ---
            Panel updHost = new Panel();
            updHost.Dock = DockStyle.Fill;
            updHost.BackColor = Theme.Bg;
            lvUpd = ListIn(updHost, true,
                L.T("Type"), "95", L.T("Oppdatering"), "520", L.T("Detaljer"), "230", L.T("Størrelse"), "105");
            updHost.Controls.Add(Widgets.Head(L.T("Tilgjengelig fra Microsoft"), out lblUpdSum));

            p.Controls.Add(updHost);
            p.Controls.Add(devHost);
            p.Controls.Add(gpuHost);
            p.Controls.Add(actions);

            tDev.Click += delegate { Util.OpenPath("devmgmt.msc"); };
            tWu.Click += delegate { Util.OpenPath("ms-settings:windowsupdate"); };

            tSearch.Click += async delegate
            {
                string dnote = "", wnote = "";
                List<DriverUpdate> drv = null;
                List<WinUpdate> win = null;
                List<ProblemDevice> devs = null;

                await Job(new Control[] { tSearch, tInst, tDev, tWu }, delegate
                {
                    Status(L.T("Leser enhetsliste …"));
                    devs = DriverTools.FindProblemDevices();
                    Status(L.T("Spør Windows Update om drivere …"));
                    drv = DriverTools.SearchDrivers(out dnote);
                    Status(L.T("Spør Windows Update om systemoppdateringer …"));
                    win = UpdateTools.Search(out wnote);
                });

                lvDev.Items.Clear();
                int devBad = 0;
                if (devs != null)
                    foreach (ProblemDevice d in devs)
                    {
                        ListViewItem li = new ListViewItem(d.Name ?? "");
                        li.SubItems.Add(d.ErrorText ?? "");
                        li.SubItems.Add(d.DeviceId ?? "");
                        bool alvorlig = d.ErrorCode != 22 && d.ErrorCode != 45;
                        li.ForeColor = alvorlig ? Theme.Warn : Theme.Muted;
                        if (alvorlig) devBad++;
                        lvDev.Items.Add(li);
                    }
                if (lvDev.Items.Count == 0)
                {
                    ListViewItem li = new ListViewItem(L.T("Ingen enheter med problemer."));
                    li.ForeColor = Theme.Good;
                    lvDev.Items.Add(li);
                }
                lblDevSum.Text = devBad > 0 ? L.F("{0} enheter melder feil", devBad) : "";
                lblDevSum.ForeColor = Theme.Bad;

                lvUpd.Items.Clear();
                int nd = 0, nw = 0;
                if (win != null)
                    foreach (WinUpdate u in win)
                    {
                        // Alvorlighetsgrad mangler paa de fleste oppdateringer som ikke
                        // gjelder sikkerhet. Les den aldri uten vern.
                        string sev = u.Severity ?? "";
                        ListViewItem li = new ListViewItem(L.T("Windows"));
                        li.SubItems.Add(u.Title ?? "");
                        li.SubItems.Add(sev.Length > 0 ? L.F("Alvorlighet: {0}", sev) : "");
                        li.SubItems.Add(u.Size > 0 ? Util.Bytes(u.Size) : "—");
                        li.Checked = true;
                        li.Tag = u;
                        li.ForeColor = sev == "Critical" ? Theme.Warn : Theme.Text;
                        lvUpd.Items.Add(li);
                        nw++;
                    }
                if (drv != null)
                    foreach (DriverUpdate d in drv)
                    {
                        ListViewItem li = new ListViewItem(L.T("Driver"));
                        li.SubItems.Add(d.Title ?? "");
                        li.SubItems.Add(d.Driver ?? "");
                        li.SubItems.Add(d.Size > 0 ? Util.Bytes(d.Size) : "—");
                        li.Checked = true;
                        li.Tag = d;
                        lvUpd.Items.Add(li);
                        nd++;
                    }

                tInst.Enabled = lvUpd.Items.Count > 0;
                if (lvUpd.Items.Count > 0)
                {
                    lblUpdSum.Text = L.F("{0} Windows-oppdateringer og {1} drivere.", nw, nd);
                    lblUpdSum.ForeColor = Theme.Warn;
                }
                else
                {
                    ListViewItem li = new ListViewItem("");
                    li.SubItems.Add(L.T("Alt er oppdatert."));
                    li.ForeColor = Theme.Good;
                    lvUpd.Items.Add(li);
                    lblUpdSum.Text = "";
                }
                Status("");
                Util.Log("Oppdateringssøk: " + nw + " Windows, " + nd + " drivere.");
            };

            tInst.Click += async delegate
            {
                List<DriverUpdate> drivers = new List<DriverUpdate>();
                List<WinUpdate> wins = new List<WinUpdate>();
                foreach (ListViewItem li in lvUpd.Items)
                {
                    if (!li.Checked) continue;
                    if (li.Tag is DriverUpdate) drivers.Add((DriverUpdate)li.Tag);
                    else if (li.Tag is WinUpdate) wins.Add((WinUpdate)li.Tag);
                }
                int n = drivers.Count + wins.Count;
                if (n == 0) { Status(L.T("Ingenting er merket.")); return; }

                if (MessageBox.Show(this,
                        L.F("Installerer {0} fra Microsoft. Skjermen kan blinke, og noe krever omstart.", n) +
                        "\n\n" + L.T("Fortsette?"), L.T("Oppdateringer"),
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

                int done = 0;
                bool reboot = false, r1 = false, r2 = false;
                await Job(new Control[] { tSearch, tInst, tDev, tWu }, delegate
                {
                    if (wins.Count > 0)
                        done += UpdateTools.Install(wins, out r1, delegate(string s) { Status(s); });
                    if (drivers.Count > 0)
                        done += DriverTools.InstallDrivers(drivers, out r2, delegate(string s) { Status(s); });
                });
                reboot = r1 || r2;
                Status(L.F("Installerte {0} av {1}.", done, n) + (reboot ? "  " + L.T("Omstart kreves.") : ""));
                if (reboot)
                    MessageBox.Show(this, L.T("Noe av dette krever omstart for å bli aktivt."),
                        L.T("Omstart"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            if (!Util.IsAdmin())
                Defer(delegate { Status(L.T("Uten administrator kan du søke, men ikke installere.")); });

            return p;
        }
    }
}
