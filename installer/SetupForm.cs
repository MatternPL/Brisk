using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vaktmester;

namespace VaktmesterSetup
{
    public class SetupForm : Form
    {
        readonly bool uninstallMode;
        Panel body;
        TextBox log;
        FlatBtn primary, secondary;
        CheckBox chkDesktop, chkLaunch;
        Label lblHead, lblSub;
        bool done;

        public SetupForm(bool uninstall)
        {
            uninstallMode = uninstall;

            Text = L.T(uninstall ? "Avinstaller Vaktmester" : "Installer Vaktmester");
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(660, uninstall ? 400 : 470);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F;
            DoubleBuffered = true;
            Theme.ApplyIcon(this);

            Build();
            Load += delegate { Theme.DarkTitleBar(this); };
        }

        void Build()
        {
            // --- topp med merke ---
            Panel head = new Panel();
            head.Dock = DockStyle.Top;
            head.Height = 130;
            head.BackColor = Color.FromArgb(0x11, 0x14, 0x1A);
            head.Paint += delegate(object s, PaintEventArgs e)
            {
                Logo.Paint(e.Graphics, 32, 27, 76, true);
                using (Pen p = new Pen(Theme.Line))
                    e.Graphics.DrawLine(p, 0, head.Height - 1, head.Width, head.Height - 1);
            };

            lblHead = Theme.Lbl(uninstallMode ? L.T("Avinstaller Vaktmester") : "Vaktmester",
                new Font("Segoe UI Light", 22f), Theme.Text);
            lblHead.Location = new Point(132, 34);
            lblSub = Theme.Lbl(uninstallMode
                    ? L.T("Fjerner programmet og snarveiene fra denne maskinen.")
                    : "v" + Setup.Version,
                Theme.F, Theme.Muted);
            lblSub.Location = new Point(135, 76);
            head.Controls.Add(lblHead);
            head.Controls.Add(lblSub);

            // --- knapperad ---
            Panel foot = new Panel();
            foot.Dock = DockStyle.Bottom;
            foot.Height = 62;
            foot.BackColor = Theme.Bg;

            primary = new FlatBtn(L.T(uninstallMode ? "Avinstaller" : "Installer"));
            primary.Primary();
            if (uninstallMode) primary.Danger();
            primary.Width = 150;
            primary.Height = 38;
            primary.Location = new Point(468, 12);
            primary.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            secondary = new FlatBtn(L.T("Avbryt"));
            secondary.Width = 110;
            secondary.Height = 38;
            secondary.Location = new Point(346, 12);
            secondary.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            secondary.Click += delegate { Close(); };

            foot.Controls.Add(primary);
            foot.Controls.Add(secondary);

            // --- midtdel ---
            body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Theme.Bg;
            body.Padding = new Padding(32, 20, 32, 8);

            Controls.Add(body);
            Controls.Add(foot);
            Controls.Add(head);

            if (uninstallMode) BuildUninstallBody();
            else BuildInstallBody();

            primary.Click += async delegate { await Go(); };
        }

        void BuildInstallBody()
        {
            Label what = new Label();
            what.Dock = DockStyle.Top;
            what.Height = 74;
            what.ForeColor = Theme.Muted;
            what.Text =
                L.T("Rydder søppelfiler, viser hva som starter med Windows, henter drivere og Windows-oppdateringer fra Microsoft, og finner hvor lagringsplassen har blitt av.") +
                "\r\n\r\n" + L.T("Ingen betalingsmur, ingen abonnement, ingen datainnsamling.");

            Label where = new Label();
            where.Dock = DockStyle.Top;
            where.Height = 44;
            where.ForeColor = Theme.Muted;
            where.Text = L.T("Installeres i:") + "\r\n" + Setup.InstallDir;

            Label admin = new Label();
            admin.Dock = DockStyle.Top;
            admin.Height = 26;
            admin.ForeColor = Theme.Good;
            admin.Text = L.T("Trenger ikke administrator.");

            chkDesktop = Chk(L.T("Lag snarvei på skrivebordet"), true);
            chkLaunch = Chk(L.T("Start etter installasjon"), true);

            Panel spacer = new Panel();
            spacer.Dock = DockStyle.Top;
            spacer.Height = 10;
            spacer.BackColor = Theme.Bg;

            body.Controls.Add(chkLaunch);
            body.Controls.Add(chkDesktop);
            body.Controls.Add(spacer);
            body.Controls.Add(admin);
            body.Controls.Add(where);
            body.Controls.Add(what);

            if (Setup.IsInstalled())
            {
                lblSub.Text = L.T("Allerede installert.");
                primary.Text = L.F("Oppdater til {0}", Setup.Version);
            }
        }

        void BuildUninstallBody()
        {
            Label what = new Label();
            what.Dock = DockStyle.Top;
            what.Height = 110;
            what.ForeColor = Theme.Muted;
            what.Text = L.T("Dette fjerner programfilene, snarveiene og oppføringen i «Apper og funksjoner». Endringer du har gjort i oppstartsprogrammer beholdes.") +
                        "\r\n\r\n" + Setup.InstallDir;
            body.Controls.Add(what);
        }

        CheckBox Chk(string text, bool on)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.Checked = on;
            c.Dock = DockStyle.Top;
            c.Height = 30;
            c.ForeColor = Theme.Text;
            c.FlatStyle = FlatStyle.Flat;
            return c;
        }

        // ---------------------------------------------------------------
        async Task Go()
        {
            if (done)
            {
                if (!uninstallMode && chkLaunch != null && chkLaunch.Checked)
                {
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo(Setup.ExePath);
                        psi.UseShellExecute = true;
                        Process.Start(psi);
                    }
                    catch { }
                }
                Close();
                return;
            }

            bool desktop = chkDesktop != null && chkDesktop.Checked;
            primary.Enabled = false;
            secondary.Enabled = false;

            body.Controls.Clear();
            log = new TextBox();
            log.Multiline = true;
            log.ReadOnly = true;
            log.ScrollBars = ScrollBars.Vertical;
            log.Dock = DockStyle.Fill;
            log.BackColor = Color.FromArgb(0x0E, 0x10, 0x14);
            log.ForeColor = Theme.Muted;
            log.BorderStyle = BorderStyle.None;
            log.Font = Theme.FMono;
            body.Controls.Add(log);
            lblHead.Text = L.T(uninstallMode ? "Avinstallerer …" : "Installerer …");
            lblSub.Text = "";

            string error = null;
            try
            {
                await Task.Run(delegate
                {
                    Action<string> w = delegate(string s) { Write(s); };
                    if (uninstallMode) Setup.Uninstall(w);
                    else Setup.Install(desktop, w);
                });
            }
            catch (Exception ex) { error = ex.Message; }

            done = true;
            secondary.Visible = false;
            primary.Enabled = true;

            if (error != null)
            {
                lblHead.Text = L.T("Det gikk galt");
                lblSub.Text = "";
                lblSub.ForeColor = Theme.Bad;
                Write("");
                Write("FEIL: " + error);
                primary.Text = L.T("Lukk");
                if (chkLaunch != null) chkLaunch.Checked = false;
            }
            else if (uninstallMode)
            {
                lblHead.Text = L.T("Vaktmester er fjernet");
                lblSub.Text = "";
                primary.Text = L.T("Lukk");
            }
            else
            {
                lblHead.Text = L.T("Ferdig installert");
                lblSub.Text = L.T("Du finner Vaktmester i Start-menyen.");
                primary.Text = (chkLaunch != null && chkLaunch.Checked)
                    ? L.T("Start Vaktmester") : L.T("Lukk");
            }
        }

        void Write(string s)
        {
            if (log == null || log.IsDisposed) return;
            if (log.IsHandleCreated && log.InvokeRequired)
            {
                log.BeginInvoke((Action)delegate { Write(s); });
                return;
            }
            log.AppendText(s + Environment.NewLine);
            if (!log.IsHandleCreated) return;
            log.SelectionStart = log.TextLength;
            log.ScrollToCaret();
        }
    }
}
