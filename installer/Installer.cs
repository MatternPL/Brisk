using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Brisk;

namespace BriskSetup
{
    static class Setup
    {
        public const string AppName = "Brisk";
        public const string Version = "1.7.5";
        public const string RegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Brisk";

        public static string InstallDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", AppName);
            }
        }

        public static string ExePath { get { return Path.Combine(InstallDir, "Brisk.exe"); } }
        public static string UninstPath { get { return Path.Combine(InstallDir, "Uninstall.exe"); } }

        public static string StartMenuLink
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");
            }
        }

        public static string DesktopLink
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk");
            }
        }

        public static bool IsInstalled()
        {
            try { return File.Exists(ExePath); }
            catch { return false; }
        }

        // Ligger denne filen inne i installasjonsmappa?
        public static bool IsInside(string file, string dir)
        {
            try
            {
                string f = Path.GetFullPath(file).ToLowerInvariant();
                string d = Path.GetFullPath(dir).ToLowerInvariant().TrimEnd('\\') + "\\";
                return f.StartsWith(d, StringComparison.Ordinal);
            }
            catch { return false; }
        }

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Theme.EnableDarkMode();

            bool uninstall = false, stage2 = false, silent = false, launchAfter = false;
            foreach (string a in args)
            {
                if (string.Equals(a, "/uninstall", StringComparison.OrdinalIgnoreCase)) uninstall = true;
                if (string.Equals(a, "/uninstall2", StringComparison.OrdinalIgnoreCase)) { uninstall = true; stage2 = true; }
                if (string.Equals(a, "/S", StringComparison.OrdinalIgnoreCase)) silent = true;
                if (string.Equals(a, "/start", StringComparison.OrdinalIgnoreCase)) launchAfter = true;
            }

            // Avinstallering kan ikke slette mappa den selv kjører fra.
            // Kopier oss til %TEMP% og fortsett derfra — også i stille modus.
            if (uninstall && !stage2 && IsInside(Application.ExecutablePath, InstallDir))
            {
                try
                {
                    string tmp = Path.Combine(Path.GetTempPath(), "brisk-avinstaller.exe");
                    File.Copy(Application.ExecutablePath, tmp, true);
                    ProcessStartInfo psi = new ProcessStartInfo(tmp, silent ? "/uninstall2 /S" : "/uninstall2");
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                    return;
                }
                catch { /* faller tilbake til å kjøre direkte */ }
            }

            // Stille modus: ingen vindu. Nyttig for utrulling og for a teste bygget.
            if (silent)
            {
                try
                {
                    if (uninstall) Uninstall(delegate(string s) { Util.Log("[avinst] " + s); });
                    else
                    {
                        Install(true, delegate(string s) { Util.Log("[inst] " + s); });
                        if (launchAfter)
                        {
                            try
                            {
                                ProcessStartInfo ps = new ProcessStartInfo(ExePath);
                                ps.UseShellExecute = true;
                                Process.Start(ps);
                            }
                            catch { }
                        }
                    }
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    Util.Log("Stille installasjon feilet: " + ex.Message);
                    Environment.Exit(1);
                }
                return;
            }

            Application.Run(new SetupForm(uninstall));
        }

        // ---------------------------------------------------------------
        public static void KillRunning()
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("Brisk"))
                {
                    try { p.Kill(); p.WaitForExit(4000); }
                    catch { }
                }
            }
            catch { }
        }

        // Programmet het Vaktmester fram til 1.1. Rydder bort den gamle
        // installasjonen så brukeren ikke sitter igjen med to av samme program.
        static void RemoveOldName(Action<string> log)
        {
            try
            {
                string oldDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "Vaktmester");
                string oldStart = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Vaktmester.lnk");
                string oldDesk = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Vaktmester.lnk");

                bool any = Directory.Exists(oldDir) || File.Exists(oldStart) || File.Exists(oldDesk);
                if (!any) return;

                log("Fjerner den gamle Vaktmester-installasjonen …");
                foreach (Process p in Process.GetProcessesByName("Vaktmester"))
                {
                    try { p.Kill(); p.WaitForExit(3000); }
                    catch { }
                }
                Thread.Sleep(200);
                Del(oldStart);
                Del(oldDesk);
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(
                        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Vaktmester", false);
                }
                catch { }
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo("schtasks",
                        "/Delete /F /TN \"Vaktmester ukentlig rydding\"");
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    Process pr = Process.Start(psi);
                    if (pr != null) pr.WaitForExit(5000);
                }
                catch { }
                for (int i = 0; i < 5 && Directory.Exists(oldDir); i++)
                {
                    try { Directory.Delete(oldDir, true); }
                    catch { Thread.Sleep(400); }
                }
            }
            catch (Exception ex) { log("  (kunne ikke rydde helt: " + ex.Message + ")"); }
        }

        public static void Install(bool desktopShortcut, Action<string> log)
        {
            log("Stopper Brisk hvis den kjører …");
            KillRunning();
            Thread.Sleep(300);
            RemoveOldName(log);

            log("Lager mappe: " + InstallDir);
            Directory.CreateDirectory(InstallDir);

            log("Pakker ut programmet …");
            using (Stream src = Assembly.GetExecutingAssembly()
                       .GetManifestResourceStream("Brisk.payload"))
            {
                if (src == null) throw new Exception(
                    "Installasjonsfilen mangler programmet. Bygg med -resource:Brisk.exe,Brisk.payload.");
                using (FileStream dst = new FileStream(ExePath, FileMode.Create, FileAccess.Write))
                    src.CopyTo(dst);
            }

            // Symbolfila maa ligge ved siden av programmet for at krasjlogger
            // skal faa linjenumre. Uten den staar det bare metodenavn, og en
            // feilrapport fra en fremmed blir nesten ubrukelig. Mangler den i
            // byggingen, gaar installasjonen videre - den er ikke kritisk.
            log("Legger inn symbolfil …");
            try
            {
                using (Stream pdb = Assembly.GetExecutingAssembly()
                           .GetManifestResourceStream("Brisk.symbols"))
                {
                    if (pdb == null) log("  (ikke med i denne byggingen)");
                    else
                        using (FileStream dst = new FileStream(
                                   Path.Combine(InstallDir, "Brisk.pdb"), FileMode.Create, FileAccess.Write))
                            pdb.CopyTo(dst);
                }
            }
            catch (Exception ex) { log("  (klarte ikke: " + ex.Message + ")"); }

            log("Legger inn avinstalleringsprogram …");
            try { File.Copy(Application.ExecutablePath, UninstPath, true); }
            catch (Exception ex) { log("  (klarte ikke: " + ex.Message + ")"); }

            log("Lager snarvei i Start-menyen …");
            MakeShortcut(StartMenuLink, ExePath, "PC-vedlikehold uten tull");
            if (desktopShortcut)
            {
                log("Lager snarvei på skrivebordet …");
                MakeShortcut(DesktopLink, ExePath, "PC-vedlikehold uten tull");
            }

            log("Registrerer i «Apper og funksjoner» …");
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RegKey))
                {
                    long size = 0;
                    try { size = new FileInfo(ExePath).Length / 1024; }
                    catch { }
                    k.SetValue("DisplayName", AppName);
                    k.SetValue("DisplayVersion", Version);
                    k.SetValue("Publisher", "Mathias Arne Andresen");
                    k.SetValue("DisplayIcon", ExePath + ",0");
                    k.SetValue("InstallLocation", InstallDir);
                    k.SetValue("UninstallString", "\"" + UninstPath + "\" /uninstall");
                    k.SetValue("EstimatedSize", (int)size, RegistryValueKind.DWord);
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                }
            }
            catch (Exception ex) { log("  (klarte ikke: " + ex.Message + ")"); }

            log("Ferdig.");
        }

        public static void Uninstall(Action<string> log)
        {
            log("Stopper Brisk …");
            KillRunning();
            Thread.Sleep(300);

            log("Fjerner snarveier …");
            Del(StartMenuLink);
            Del(DesktopLink);

            log("Fjerner registeroppføring …");
            try { Registry.CurrentUser.DeleteSubKeyTree(RegKey, false); }
            catch { }

            log("Fjerner programfiler …");
            // Prøver flere ganger — Windows kan holde exe-en et lite øyeblikk.
            for (int i = 0; i < 6; i++)
            {
                try
                {
                    if (Directory.Exists(InstallDir)) Directory.Delete(InstallDir, true);
                    break;
                }
                catch { Thread.Sleep(500); }
            }
            if (Directory.Exists(InstallDir))
                log("  Noen filer var låst. Mappa kan slettes manuelt: " + InstallDir);

            log("Loggen din er beholdt i %LOCALAPPDATA%\\Brisk.");
            log("Ferdig.");
        }

        static void Del(string p)
        {
            try { if (File.Exists(p)) File.Delete(p); }
            catch { }
        }

        static void MakeShortcut(string linkPath, string target, string desc)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return;
                dynamic shell = Activator.CreateInstance(t);
                dynamic sc = shell.CreateShortcut(linkPath);
                sc.TargetPath = target;
                sc.WorkingDirectory = Path.GetDirectoryName(target);
                sc.IconLocation = target + ",0";
                sc.Description = desc;
                sc.Save();
            }
            catch { }
        }
    }
}
