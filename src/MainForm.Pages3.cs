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
        TextBox updOut;
        Label lblGpu, lblUpdSum, lblDevSum;

        // Viser hva som staar paa maskina naa. Versjonen er den leverandoren
        // selv bruker, ikke Windows sitt interne nummer.
        void GpuVis()
        {
            if (gpu == null) return;
            string alder = gpu.DriverDate == DateTime.MinValue ? ""
                : "   ·   " + L.F("{0} dager gammel",
                    (int)(DateTime.Now - gpu.DriverDate).TotalDays);
            lblGpu.Text = gpu.Name + "   ·   " + L.T("driver") + " " + gpu.Installed + alder;
        }

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

            Panel actions = Widgets.Row(110, tSearch, tInst, tDev, tWu);

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
                L.T("Windows Update ligger ofte etter for skjermkort. Brisk spør NVIDIA og AMD direkte."),
                Theme.FSmall, Theme.Muted);
            gpuNote.Location = new Point(22, 38);

            // To knapper: den forste sjekker, den andre dukker opp forst naar
            // det faktisk finnes noe nytt. En knapp som laster ned uten aa ha
            // sjekket ville bare gjettet.
            FlatBtn bGpuGet = new FlatBtn(L.T("Last ned"));
            bGpuGet.Primary();
            bGpuGet.Width = 150; bGpuGet.Height = 34;
            bGpuGet.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bGpuGet.Visible = false;

            FlatBtn bGpu = new FlatBtn(L.T("Se etter ny driver"));
            bGpu.Width = 180; bGpu.Height = 34;
            bGpu.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bGpu.Visible = false;

            gpuCard.Controls.Add(lblGpu);
            gpuCard.Controls.Add(gpuNote);
            gpuCard.Controls.Add(bGpu);
            gpuCard.Controls.Add(bGpuGet);
            Theme.Arrange(gpuCard, delegate
            {
                bGpuGet.Location = new Point(gpuCard.Width - bGpuGet.Width - 20, 20);
                bGpu.Location = new Point(
                    (bGpuGet.Visible ? bGpuGet.Left : gpuCard.Width - 20) - bGpu.Width - 10, 20);
                // Teksten maa stoppe for knappene, ellers legger de seg oppaa
                // hverandre naar vinduet er smalt.
                int plass = Math.Max(160, bGpu.Left - 40);
                gpuNote.AutoSize = false;
                gpuNote.Width = plass;
                gpuNote.Height = 34;
                lblGpu.AutoSize = false;
                lblGpu.Width = plass;
                lblGpu.Height = 22;
            });
            gpuHost.Controls.Add(gpuCard);

            Defer(delegate
            {
                try
                {
                    if (gpu == null) gpu = GpuTools.Read();
                    if (gpu.Name.Length == 0) { lblGpu.Text = L.T("Fant ingen skjermkort."); return; }
                    GpuVis();

                    bGpu.Visible = gpu.Known;
                    if (!gpu.Known)
                        gpuNote.Text = L.T("Brisk kan bare sjekke drivere for NVIDIA- og AMD-skjermkort.");
                    gpuCard.PerformLayout();
                }
                catch (Exception ex) { lblGpu.Text = ex.Message; }
            });

            bGpu.Click += async delegate
            {
                string feil = null;
                GpuDriver d = null;
                gpuNote.Text = L.F("Spør {0} …", gpu.Vendor);
                await Job(new Control[] { bGpu, bGpuGet }, delegate
                {
                    d = GpuTools.Latest(gpu, out feil);
                });

                if (d == null)
                {
                    gpuNote.Text = feil;
                    gpuNote.ForeColor = Theme.Warn;
                    Append(updOut, feil); Status(feil);
                    return;
                }

                gpuLatest = d;
                bGpuGet.Visible = d.Newer;
                gpuNote.ForeColor = d.Newer ? Theme.Warn : Theme.Good;
                gpuNote.Text = d.Newer
                    ? L.F("{0} er ute. Du har {1}.", d.Version, gpu.Installed)
                      + (d.Released.Length > 0 ? "   ·   " + d.Released : "")
                    : L.F("Du har nyeste driver ({0}).", gpu.Installed);
                Append(updOut, gpuNote.Text); Status(gpuNote.Text);
                gpuCard.PerformLayout();
                RefreshOverview();
            };

            bGpuGet.Click += async delegate
            {
                if (gpuLatest == null) return;
                string feil = null, fil = null;
                GpuDriver d = gpuLatest;
                string merke = gpu.Vendor;

                Append(updOut, L.F("Laster ned {0}-driver {1} …", merke, d.Version));
                await Job(new Control[] { bGpu, bGpuGet }, delegate
                {
                    long sist = 0;
                    fil = GpuTools.Download(d, merke, delegate(long got, long total)
                    {
                        // Én linje per 10 MB, ellers drukner utdata i tall.
                        if (got - sist < 10L * 1024 * 1024 && got != total) return;
                        sist = got;
                        Status(total > 0
                            ? L.F("{0} av {1}", Util.Bytes(got), Util.Bytes(total))
                            : Util.Bytes(got));
                    }, out feil);
                });

                if (fil == null) { Append(updOut, feil); Status(feil); return; }

                // Brisk kjorer ikke installatoren selv. En driverinstallasjon
                // slaar av skjermen underveis og skal startes naar brukeren er
                // klar - vi aapner mappa i stedet.
                Append(updOut, L.F("Lagret som {0}. Brisk starter den ikke — kjør den når du er klar.", fil));
                Util.OpenPath(System.IO.Path.GetDirectoryName(fil));
            };

            // --- utdata ---
            // Foer gikk all framdrift til statuslinja nederst i vinduet, en
            // linje av gangen. Under en nedlasting som tar minutter sto den
            // helt stille, og feilmeldinger ble overskrevet av sluttmeldingen.
            Panel outHost = new Panel();
            outHost.Dock = DockStyle.Bottom;
            outHost.Height = 132;
            outHost.BackColor = Theme.Bg;
            outHost.Padding = new Padding(0, 12, 0, 0);
            updOut = Console(outHost, 0);
            Label outNote;
            outHost.Controls.Add(Widgets.Head(L.T("Utdata"), out outNote));

            // --- enheter med problem ---
            Panel devHost = new Panel();
            devHost.Dock = DockStyle.Bottom;
            devHost.Height = 150;
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
            p.Controls.Add(outHost);
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
                if (n == 0)
                {
                    MessageBox.Show(this, L.T("Ingenting er merket. Huk av oppdateringene du vil installere først."),
                        L.T("Oppdateringer"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (!Util.IsAdmin())
                {
                    MessageBox.Show(this,
                        L.T("Installering av oppdateringer krever administrator. Start Brisk som administrator og prøv igjen."),
                        L.T("Oppdateringer"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show(this,
                        L.F("Installerer {0} fra Microsoft. Skjermen kan blinke, og noe krever omstart.", n) +
                        "\n\n" + L.T("Fortsette?"), L.T("Oppdateringer"),
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

                Append(updOut, "");
                Append(updOut, L.F("Installerer {0} …", n));

                int done = 0;
                bool reboot = false, r1 = false, r2 = false;
                Action<string> w = delegate(string line) { Append(updOut, line); Status(line); };
                await Job(new Control[] { tSearch, tInst, tDev, tWu }, delegate
                {
                    if (wins.Count > 0)
                        done += UpdateTools.Install(wins, out r1, w);
                    if (drivers.Count > 0)
                        done += DriverTools.InstallDrivers(drivers, out r2, w);
                });
                reboot = r1 || r2;
                string oppsum = L.F("Installerte {0} av {1}.", done, n) + (reboot ? "  " + L.T("Omstart kreves.") : "");
                Append(updOut, oppsum);
                Status(oppsum);
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
