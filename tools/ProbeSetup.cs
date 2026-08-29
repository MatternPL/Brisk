using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using BriskSetup;

// Viser ett av dialogvinduene og skriver kontrolltreet til fil, saa vi kan
// se og maale dem uten aa installere eller fjerne noe.
//   ProbeSetup.exe                -> avinstallasjonsvinduet
//   BRISK_PROBE=install ...       -> installasjonsvinduet
//   BRISK_PROBE=update ...        -> oppdateringsvinduet
namespace Brisk
{
    static class ProbeSetup
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            string which = Environment.GetEnvironmentVariable("BRISK_PROBE");
            if (string.IsNullOrEmpty(which)) which = "uninstall";

            Form f;
            if (which == "update")
            {
                UpdateInfo u = new UpdateInfo();
                u.Version = "1.4.0";
                u.Url = "https://github.com/MatternPL/Brisk/releases/download/v1.4.0/BriskInstaller.exe";
                u.Sha256 = new string('a', 64);
                u.Size = 564224;
                u.Notes = "Sample release note used only to render this window.";
                f = new UpdateDialog(u);
            }
            else f = new SetupForm(which == "uninstall");

            f.Shown += delegate
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(which + ": ClientSize = " + f.ClientSize);
                Dump(sb, f.Controls, 0);
                try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "brisk-probe.txt"), sb.ToString()); }
                catch (Exception) { }
            };
            Application.Run(f);
        }

        static void Dump(StringBuilder sb, Control.ControlCollection cc, int depth)
        {
            for (int i = 0; i < cc.Count; i++)
            {
                Control c = cc[i];
                sb.AppendLine(new string(' ', depth * 2) +
                    "[" + i + "] " + c.GetType().Name +
                    " dock=" + c.Dock +
                    " bounds=" + c.Bounds +
                    " visible=" + c.Visible +
                    (c.Text.Length > 0 && c.Text.Length < 30 ? " text=\"" + c.Text + "\"" : ""));
                if (c.Controls.Count > 0) Dump(sb, c.Controls, depth + 1);
            }
        }
    }
}
