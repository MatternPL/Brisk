using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vaktmester
{
    public partial class MainForm
    {
        // ==============================================================
        //  OPPDATERINGER — Windows-oppdateringer, drivere og problemenheter
        // ==============================================================
        ListView lvUpd, lvDev;

        Panel PageDrivers()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            FlatBtn bSearch = new FlatBtn("Søk hos Windows Update"); bSearch.Primary(); bSearch.Width = 210;
            FlatBtn bInst = new FlatBtn("Installer merkede"); bInst.Width = 160; bInst.Enabled = false;
            FlatBtn bDev = new FlatBtn("Enhetsbehandling"); bDev.Width = 155;
            FlatBtn bWu = new FlatBtn("Windows Update"); bWu.Width = 155;
            Panel bar = Toolbar(bSearch, bInst, bDev, bWu);

            Panel devHost = new Panel();
            devHost.Dock = DockStyle.Bottom;
            devHost.Height = 172;
            devHost.BackColor = Theme.Bg;
            Label dl = Theme.Lbl("Enheter Windows melder problem på", Theme.FBold, Theme.Text);
            dl.Dock = DockStyle.Top; dl.Height = 24;
            lvDev = ListIn(devHost, false, "Enhet", "380", "Problem", "330", "Enhets-ID", "420");
            devHost.Controls.Add(dl);

            Panel updHost = new Panel();
            updHost.Dock = DockStyle.Fill;
            updHost.BackColor = Theme.Bg;
            Label dl2 = Theme.Lbl("Tilgjengelig fra Microsoft — drivere og Windows-oppdateringer",
                Theme.FBold, Theme.Text);
            dl2.Dock = DockStyle.Top; dl2.Height = 24;
            lvUpd = ListIn(updHost, true,
                "Type", "95", "Oppdatering", "520", "Detaljer", "230", "Størrelse", "105");
            updHost.Controls.Add(dl2);

            p.Controls.Add(updHost);
            p.Controls.Add(devHost);
            p.Controls.Add(bar);

            bDev.Click += delegate { Util.OpenPath("devmgmt.msc"); };
            bWu.Click += delegate { Util.OpenPath("ms-settings:windowsupdate"); };

            bSearch.Click += async delegate
            {
                string dnote = "", wnote = "";
                List<DriverUpdate> drv = null;
                List<WinUpdate> win = null;
                List<ProblemDevice> devs = null;

                await Job(new Control[] { bSearch, bInst, bDev, bWu }, delegate
                {
                    Status("Leser enhetsliste …");
                    devs = DriverTools.FindProblemDevices();
                    Status("Spør Windows Update om drivere — kan ta et minutt …");
                    drv = DriverTools.SearchDrivers(out dnote);
                    Status("Spør Windows Update om systemoppdateringer …");
                    win = UpdateTools.Search(out wnote);
                });

                lvDev.Items.Clear();
                if (devs != null)
                    foreach (ProblemDevice d in devs)
                    {
                        ListViewItem li = new ListViewItem(d.Name);
                        li.SubItems.Add(d.ErrorText);
                        li.SubItems.Add(d.DeviceId);
                        li.ForeColor = (d.ErrorCode == 22 || d.ErrorCode == 45) ? Theme.Muted : Theme.Warn;
                        lvDev.Items.Add(li);
                    }
                if (lvDev.Items.Count == 0)
                {
                    ListViewItem li = new ListViewItem("Ingen enheter med problemer.");
                    li.ForeColor = Theme.Good;
                    lvDev.Items.Add(li);
                }

                lvUpd.Items.Clear();
                int nd = 0, nw = 0;
                if (win != null)
                    foreach (WinUpdate u in win)
                    {
                        ListViewItem li = new ListViewItem("Windows");
                        li.SubItems.Add(u.Title);
                        li.SubItems.Add(u.Severity.Length > 0 ? "Alvorlighet: " + u.Severity : "");
                        li.SubItems.Add(u.Size > 0 ? Util.Bytes(u.Size) : "—");
                        li.Checked = true;
                        li.Tag = u;
                        li.ForeColor = u.Severity == "Critical" ? Theme.Warn : Theme.Text;
                        lvUpd.Items.Add(li);
                        nw++;
                    }
                if (drv != null)
                    foreach (DriverUpdate d in drv)
                    {
                        ListViewItem li = new ListViewItem("Driver");
                        li.SubItems.Add(d.Title);
                        li.SubItems.Add(d.Driver);
                        li.SubItems.Add(d.Size > 0 ? Util.Bytes(d.Size) : "—");
                        li.Checked = true;
                        li.Tag = d;
                        lvUpd.Items.Add(li);
                        nd++;
                    }

                bInst.Enabled = lvUpd.Items.Count > 0;
                if (lvUpd.Items.Count > 0)
                    Status(nw + " Windows-oppdatering(er) og " + nd + " driver(e) tilgjengelig.");
                else
                    Status("Alt er oppdatert. " + wnote + " " + dnote);
                Util.Log("Oppdateringssøk: " + nw + " Windows, " + nd + " drivere.");
            };

            bInst.Click += async delegate
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
                if (n == 0) { Status("Ingenting er merket."); return; }

                if (MessageBox.Show(this,
                        "Installerer " + n + " oppdatering(er) direkte fra Microsoft.\n\n" +
                        "Skjermen kan blinke under driverinstallasjon, og noe krever omstart.\n\n" +
                        "Fortsette?", "Bekreft installasjon",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

                int done = 0;
                bool reboot = false, r1 = false, r2 = false;
                await Job(new Control[] { bSearch, bInst, bDev, bWu }, delegate
                {
                    if (wins.Count > 0)
                        done += UpdateTools.Install(wins, out r1, delegate(string s) { Status(s); });
                    if (drivers.Count > 0)
                        done += DriverTools.InstallDrivers(drivers, out r2, delegate(string s) { Status(s); });
                });
                reboot = r1 || r2;
                Status("Installerte " + done + " av " + n + "." + (reboot ? "  Omstart kreves." : ""));
                if (reboot)
                    MessageBox.Show(this, "Noe av dette krever omstart for å bli aktivt.",
                        "Omstart nødvendig", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            if (!Util.IsAdmin())
                Defer(delegate { Status("Uten administrator kan oppdateringer søkes opp, men ikke installeres."); });

            return p;
        }
    }
}
