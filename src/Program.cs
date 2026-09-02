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

            // Maal maskinen foerst, saa forsida har tall med en gang i stedet
            // for aa staa med streker til brukeren trykker Sjekk PC-en.
            // Hovedvinduet bygges inne i lastevinduet, ikke etter det. Foerste
            // gang koden kjorer maa den JIT-kompileres, og det tar sekunder -
            // laa byggingen etter Close(), sto brukeren igjen med tomt
            // skrivebord i mellomtiden.
            StartupScan maalt = null;
            MainForm hoved = null;
            try
            {
                using (SplashForm splash = new SplashForm())
                {
                    splash.Forbered = delegate(StartupScan s)
                    {
                        hoved = new MainForm(startPage, s);
                    };
                    splash.ShowDialog();
                    maalt = splash.Result;
                }
            }
            catch (Exception ex) { Util.Log("Oppstartsvindu feilet: " + ex.Message); }

            // Feilet lastevinduet, eller ble det lukket for maalingen var ferdig,
            // skal programmet fortsatt starte.
            if (hoved == null) hoved = new MainForm(startPage, maalt);

            Application.Run(hoved);
        }
    }
}
