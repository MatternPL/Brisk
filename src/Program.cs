using System;
using System.Windows.Forms;

namespace Brisk
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Theme.EnableDarkMode();

            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                try { Util.Log("UHÅNDTERT FEIL: " + e.ExceptionObject); }
                catch { }
            };
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
            {
                Util.Log("FEIL: " + e.Exception);
                MessageBox.Show("Noe gikk galt:\n\n" + e.Exception.Message +
                    "\n\nDetaljer er skrevet til loggen.", "Brisk",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };

            // /auto kjorer den trygge ryddingen uten vindu. Brukes av den planlagte oppgaven.
            foreach (string a in args)
            {
                if (string.Equals(a, "/auto", StringComparison.OrdinalIgnoreCase))
                {
                    Environment.Exit(AutoClean.Run());
                    return;
                }
            }

            // /side:<navn> apner en bestemt side direkte. Praktisk ved testing.
            string startPage = "oversikt";
            foreach (string a in args)
            {
                if (a.StartsWith("/side:", StringComparison.OrdinalIgnoreCase))
                    startPage = a.Substring(6).ToLowerInvariant();
            }

            Util.Log("Brisk startet. Administrator: " + Util.IsAdmin());
            Application.Run(new MainForm(startPage));
        }
    }
}
