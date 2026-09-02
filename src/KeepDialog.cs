using System;
using System.Drawing;
using System.Windows.Forms;

namespace Brisk
{
    // ------------------------------------------------------------------
    //  «Beholder du denne innstillingen?»
    //
    //  Windows spor om det samme naar man bytter oppløsning, og av god grunn:
    //  en skjerm kan svare at den stotter 240 Hz og likevel bli helt svart
    //  naar man faktisk setter den. Skjer det, ser ikke brukeren dette vinduet
    //  i det hele tatt - og da er nedtellingen det eneste som redder ham.
    //
    //  Derfor er tidsavbruddet det viktige her, ikke knappene. Trykkes det
    //  ingenting, angres endringen. Det er alltid trygt: er skjermen svart,
    //  vil brukeren ha den tilbake.
    //
    //  Vinduet legges paa hovedskjermen, ikke paa den som ble endret. Er det
    //  en annen skjerm som ble svart, kan brukeren fortsatt lese og trykke.
    // ------------------------------------------------------------------
    public class KeepDialog : Form
    {
        readonly Label tekst;
        readonly Timer klokke = new Timer();
        int igjen;

        public KeepDialog(string sporsmaal, int sekunder)
        {
            igjen = sekunder;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(460, 190);
            BackColor = Theme.Card;
            TopMost = true;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            Theme.ApplyIcon(this);

            Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.Line))
                    e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            };

            Label tittel = Theme.Lbl(L.T("Beholde denne innstillingen?"),
                new Font("Segoe UI Light", 17f), Theme.Text);
            tittel.Location = new Point(24, 22);
            tittel.AutoSize = false;
            tittel.Size = new Size(ClientSize.Width - 48, 30);
            Controls.Add(tittel);

            Label under = Theme.Lbl(sporsmaal, Theme.FSmall, Theme.Muted);
            under.Location = new Point(24, 56);
            under.AutoSize = false;
            under.Size = new Size(ClientSize.Width - 48, 34);
            Controls.Add(under);

            tekst = Theme.Lbl("", Theme.FSmall, Theme.Warn);
            tekst.Location = new Point(24, 96);
            tekst.AutoSize = false;
            tekst.Size = new Size(ClientSize.Width - 48, 20);
            Controls.Add(tekst);

            FlatBtn behold = new FlatBtn(L.T("Behold"));
            behold.Primary();
            behold.Size = new Size(130, 36);
            behold.Location = new Point(24, ClientSize.Height - 36 - 24);
            behold.Click += delegate { Svar(DialogResult.OK); };
            Controls.Add(behold);

            FlatBtn tilbake = new FlatBtn(L.T("Tilbakestill"));
            tilbake.Size = new Size(130, 36);
            tilbake.Location = new Point(24 + 130 + 10, ClientSize.Height - 36 - 24);
            tilbake.Click += delegate { Svar(DialogResult.Cancel); };
            Controls.Add(tilbake);

            // Escape angrer. Enter gjor ingenting med vilje - en tast som
            // sitter fast, eller en bruker som trykker i blinde, skal ikke
            // kunne laase inn et svart bilde.
            CancelButton = tilbake;
            KeyPreview = true;

            Vis();
            klokke.Interval = 1000;
            klokke.Tick += delegate
            {
                igjen--;
                if (igjen <= 0) Svar(DialogResult.Cancel);
                else Vis();
            };
            klokke.Start();

            // Hovedskjermen, ikke den som ble endret.
            Rectangle r = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(r.Left + (r.Width - Width) / 2,
                                 r.Top + (r.Height - Height) / 3);
        }

        void Vis()
        {
            tekst.Text = L.F("Tilbakestilles om {0} sekunder.", igjen);
        }

        void Svar(DialogResult r)
        {
            klokke.Stop();
            DialogResult = r;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) klokke.Dispose();
            base.Dispose(disposing);
        }
    }
}
