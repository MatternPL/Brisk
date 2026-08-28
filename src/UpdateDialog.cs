using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Brisk
{
    // Spør om oppdatering, laster ned og starter installasjonen.
    public class UpdateDialog : Form
    {
        readonly UpdateInfo info;
        FlatBtn bYes, bNo;
        Label lblState;
        Bar bar;
        TextBox notes;
        bool busy;

        public UpdateDialog(UpdateInfo u)
        {
            info = u;
            Text = L.T("Ny versjon tilgjengelig");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 340);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F;
            DoubleBuffered = true;
            Theme.ApplyIcon(this);

            Panel head = new Panel();
            head.Dock = DockStyle.Top;
            head.Height = 104;
            head.BackColor = Color.FromArgb(0x11, 0x14, 0x1A);
            head.Paint += delegate(object s, PaintEventArgs e)
            {
                Logo.Paint(e.Graphics, 26, 22, 58, true);
                using (Pen p = new Pen(Theme.Line))
                    e.Graphics.DrawLine(p, 0, head.Height - 1, head.Width, head.Height - 1);
            };
            Label h = Theme.Lbl(L.F("Brisk {0}", u.Version),
                new Font("Segoe UI Light", 17f), Theme.Text);
            h.Location = new Point(102, 28);
            Label sub = Theme.Lbl(L.F("Du har {0}", Updater.CurrentVersion) +
                (u.Size > 0 ? "   ·   " + Util.Bytes(u.Size) : ""),
                Theme.FSmall, Theme.Muted);
            sub.Location = new Point(104, 62);
            head.Controls.Add(h);
            head.Controls.Add(sub);

            Panel foot = new Panel();
            foot.Dock = DockStyle.Bottom;
            foot.Height = 92;
            foot.BackColor = Theme.Bg;
            foot.Padding = new Padding(24, 0, 24, 14);

            bar = new Bar();
            bar.Dock = DockStyle.Top;
            bar.Height = 6;
            bar.Visible = false;

            lblState = Theme.Lbl("", Theme.FSmall, Theme.Muted);
            lblState.AutoSize = false;
            lblState.Dock = DockStyle.Top;
            lblState.Height = 26;

            bYes = new FlatBtn(L.T("Oppdater nå"));
            bYes.Primary();
            bYes.Width = 150; bYes.Height = 38;
            bYes.Location = new Point(386, 46);
            bYes.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            bNo = new FlatBtn(L.T("Ikke nå"));
            bNo.Width = 110; bNo.Height = 38;
            bNo.Location = new Point(264, 46);
            bNo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bNo.Click += delegate { if (!busy) Close(); };

            foot.Controls.Add(bYes);
            foot.Controls.Add(bNo);
            foot.Controls.Add(lblState);
            foot.Controls.Add(bar);

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Theme.Bg;
            body.Padding = new Padding(24, 14, 24, 6);

            notes = new TextBox();
            notes.Multiline = true;
            notes.ReadOnly = true;
            notes.ScrollBars = ScrollBars.Vertical;
            notes.Dock = DockStyle.Fill;
            notes.BackColor = Theme.Card;
            notes.ForeColor = Theme.Muted;
            notes.BorderStyle = BorderStyle.None;
            notes.Font = Theme.F;
            notes.Text = string.IsNullOrEmpty(u.Notes) ? L.T("Ingen endringsbeskrivelse.") : u.Notes;
            body.Controls.Add(notes);

            Controls.Add(body);
            Controls.Add(foot);
            Controls.Add(head);

            bYes.Click += async delegate { await Run(); };
            Load += delegate { Theme.DarkTitleBar(this); };
        }

        async Task Run()
        {
            busy = true;
            bYes.Enabled = false;
            bNo.Enabled = false;
            bar.Visible = true;
            lblState.Text = L.T("Laster ned …");

            string path = null, error = null;
            await Task.Run(delegate
            {
                path = Updater.Download(info, delegate(long got, long total)
                {
                    SetProgress(got, total);
                }, out error);
            });

            if (path == null)
            {
                bar.Visible = false;
                lblState.ForeColor = Theme.Bad;
                lblState.Text = error ?? L.T("Nedlastingen feilet.");
                bNo.Text = L.T("Lukk");
                bNo.Enabled = true;
                busy = false;
                return;
            }

            lblState.ForeColor = Theme.Good;
            lblState.Text = L.T("Sjekksum bekreftet. Starter installasjonen …");
            Application.DoEvents();

            string err2;
            if (!Updater.Apply(path, Updater.InstalledNormally(), out err2))
            {
                lblState.ForeColor = Theme.Bad;
                lblState.Text = err2;
                bNo.Text = "Lukk";
                bNo.Enabled = true;
                busy = false;
                return;
            }

            // Installasjonen avslutter dette programmet og starter den nye utgaven.
            await Task.Delay(1200);
            Application.Exit();
        }

        void SetProgress(long got, long total)
        {
            if (IsDisposed) return;
            if (IsHandleCreated && InvokeRequired)
            {
                try { BeginInvoke((Action)delegate { SetProgress(got, total); }); }
                catch { }
                return;
            }
            if (total > 0)
            {
                bar.Value = (double)got / total;
                lblState.Text = L.F("Laster ned … {0} av {1}", Util.Bytes(got), Util.Bytes(total));
            }
            else
            {
                lblState.Text = L.T("Laster ned …") + " " + Util.Bytes(got);
            }
            bar.Invalidate();
        }
    }
}
