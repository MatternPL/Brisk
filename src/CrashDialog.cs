using System;
using System.Drawing;
using System.Windows.Forms;

namespace Brisk
{
    // Viser hva som faktisk skjedde i en blåskjerm, og hva brukeren kan gjøre.
    public class CrashDialog : Form
    {
        readonly DumpAnalysis a;

        public CrashDialog(DumpAnalysis analysis)
        {
            a = analysis;

            Text = L.T("Blåskjermanalyse");
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(860, 660);
            MinimumSize = new Size(720, 560);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F;
            DoubleBuffered = true;
            Theme.ApplyIcon(this);

            Color tone = a.LikelyCause != null && !a.LikelyCause.IsMicrosoft ? Theme.Warn : Theme.Muted;

            // --- topp ---
            Panel head = new Panel();
            head.Dock = DockStyle.Top;
            head.Height = 128;
            head.BackColor = Color.FromArgb(0x11, 0x14, 0x1A);
            head.Paint += delegate(object s, PaintEventArgs e)
            {
                using (SolidBrush b = new SolidBrush(tone))
                    e.Graphics.FillRectangle(b, 0, 0, 4, head.Height);
                using (Pen p = new Pen(Theme.Line))
                    e.Graphics.DrawLine(p, 0, head.Height - 1, head.Width, head.Height - 1);
            };

            Label when = Theme.Lbl(a.Time.ToString("yyyy-MM-dd HH:mm"), Theme.FSmall, Theme.Muted);
            when.Location = new Point(28, 18);

            Label code = Theme.Lbl(a.CodeText, new Font("Segoe UI Light", 18f), Theme.Text);
            code.Location = new Point(26, 38);

            Label mean = Theme.Lbl(a.Meaning.Length > 0 ? a.Meaning : L.T("Ukjent stoppkode."),
                Theme.F, Theme.Muted);
            mean.Location = new Point(28, 80);

            head.Controls.Add(when);
            head.Controls.Add(code);
            head.Controls.Add(mean);

            // --- knapper ---
            Panel foot = new Panel();
            foot.Dock = DockStyle.Bottom;
            foot.Height = 62;
            foot.BackColor = Theme.Bg;
            foot.Padding = new Padding(24, 12, 24, 14);

            FlatBtn copy = new FlatBtn(L.T("Kopier oppsummering"));
            copy.Primary();
            copy.Width = 200; copy.Height = 36; copy.Location = new Point(24, 12);
            copy.Click += delegate
            {
                try
                {
                    Clipboard.SetText(DumpTools.Summary(a));
                    copy.Text = L.T("Kopiert");
                }
                catch { }
            };

            FlatBtn folder = new FlatBtn(L.T("Åpne dumpmappa"));
            folder.Width = 170; folder.Height = 36; folder.Location = new Point(234, 12);
            folder.Click += delegate
            {
                try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + a.File + "\""); }
                catch { }
            };

            FlatBtn mem = new FlatBtn(L.T("Test minnet"));
            mem.Width = 150; mem.Height = 36; mem.Location = new Point(414, 12);
            mem.Click += delegate
            {
                if (MessageBox.Show(this,
                        L.T("Windows Minnediagnostikk må starte maskinen på nytt for å teste RAM-en. Lagre arbeidet ditt først.") +
                        "\n\n" + L.T("Åpne den nå?"),
                        L.T("Test minnet"), MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) != DialogResult.Yes) return;
                try { System.Diagnostics.Process.Start("mdsched.exe"); }
                catch { }
            };

            FlatBtn close = new FlatBtn(L.T("Lukk"));
            close.Width = 110; close.Height = 36;
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Location = new Point(ClientSize.Width - 134, 12);
            close.Click += delegate { Close(); };

            foot.Controls.Add(copy);
            foot.Controls.Add(folder);
            foot.Controls.Add(mem);
            foot.Controls.Add(close);

            // --- innhold ---
            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Theme.Bg;
            body.Padding = new Padding(24, 16, 24, 8);

            ListView lv = Theme.MakeList();
            lv.Dock = DockStyle.Fill;
            lv.MultiSelect = false;
            lv.HideSelection = true;
            lv.Columns.Add(L.T("Driver"), 260);
            lv.Columns.Add(L.T("Opphav"), 300);
            lv.Columns.Add(L.T("Rolle"), 240);

            if (a.Stack.Count > 0)
                foreach (DumpModule m in a.Stack)
                {
                    ListViewItem li = new ListViewItem(m.Name);
                    li.SubItems.Add(m.Origin);
                    li.SubItems.Add(m == a.LikelyCause ? L.T("Sannsynlig årsak")
                                  : m == a.Culprit ? L.T("Feilen slo ut her")
                                  : L.T("Var involvert"));
                    li.ForeColor = m == a.LikelyCause ? Theme.Warn
                                 : m.IsMicrosoft ? Theme.Muted : Theme.Text;
                    lv.Items.Add(li);
                }

            if (a.ThirdParty.Count > 0)
            {
                ListViewItem sep = new ListViewItem("");
                lv.Items.Add(sep);
                ListViewItem hdr = new ListViewItem(L.T("Andre drivere som ikke er fra Windows"));
                hdr.ForeColor = Theme.Text;
                hdr.Font = Theme.FBold;
                lv.Items.Add(hdr);

                int n = 0;
                foreach (DumpModule m in a.ThirdParty)
                {
                    if (n++ >= 30) break;
                    ListViewItem li = new ListViewItem("    " + m.Name);
                    li.SubItems.Add(m.Origin);
                    li.SubItems.Add(m.Version);
                    li.ForeColor = Theme.Muted;
                    lv.Items.Add(li);
                }
            }

            Panel listWrap = Theme.MakeCard();
            listWrap.Dock = DockStyle.Fill;
            listWrap.Padding = new Padding(1);
            listWrap.Controls.Add(lv);

            // --- rådboks ---
            Panel adviceHost = new Panel();
            adviceHost.Dock = DockStyle.Top;
            adviceHost.Height = 150;
            adviceHost.BackColor = Theme.Bg;
            adviceHost.Padding = new Padding(0, 0, 0, 14);

            Panel adviceCard = Theme.MakeCard();
            adviceCard.Dock = DockStyle.Fill;
            adviceCard.Paint += delegate(object s, PaintEventArgs e)
            {
                using (SolidBrush b = new SolidBrush(tone))
                    e.Graphics.FillRectangle(b, 0, 0, 3, adviceCard.Height);
            };

            Label lblCause = Theme.Lbl(
                a.LikelyCause != null
                    ? L.F("Sannsynlig årsak: {0}", a.LikelyCause.Name +
                        (a.LikelyCause.Known ? "  —  " + a.LikelyCause.Origin : ""))
                    : (a.Culprit != null ? L.F("Feilen slo ut i {0}", a.Culprit.Name)
                                         : L.T("Fant ingen navngitt modul")),
                new Font("Segoe UI Semibold", 12f), tone);
            lblCause.Location = new Point(20, 16);

            Label lblAdvice = Theme.Lbl(a.Advice, Theme.F, Theme.Muted);
            lblAdvice.Location = new Point(22, 46);
            lblAdvice.AutoSize = false;
            lblAdvice.Size = new Size(760, 86);
            lblAdvice.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;

            adviceCard.Controls.Add(lblCause);
            adviceCard.Controls.Add(lblAdvice);
            adviceHost.Controls.Add(adviceCard);

            body.Controls.Add(listWrap);
            body.Controls.Add(adviceHost);

            Controls.Add(body);
            Controls.Add(foot);
            Controls.Add(head);

            Load += delegate { Theme.DarkTitleBar(this); };
        }
    }
}
