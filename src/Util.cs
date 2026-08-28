using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Vaktmester
{
    // Felles hjelpefunksjoner: formatering, logging og kjøring av eksterne prosesser.
    static class Util
    {
        public static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Vaktmester", "vaktmester.log");

        public static event Action<string> LogWritten;

        public static void Log(string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message;
            try
            {
                string dir = Path.GetDirectoryName(LogPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
            Action<string> h = LogWritten;
            if (h != null) h(line);
        }

        public static string Bytes(long b)
        {
            if (b < 0) return "–";
            if (b == 0) return "0 B";
            if (b < 1024) return b + " B";
            double v = b;
            string[] units = { "KB", "MB", "GB", "TB" };
            int i = -1;
            do { v /= 1024.0; i++; } while (v >= 1024 && i < units.Length - 1);
            return v.ToString(v >= 100 ? "0" : "0.0") + " " + units[i];
        }

        public static string Bytes(ulong b) { return Bytes((long)b); }

        public static bool IsAdmin()
        {
            try
            {
                System.Security.Principal.WindowsIdentity id =
                    System.Security.Principal.WindowsIdentity.GetCurrent();
                return new System.Security.Principal.WindowsPrincipal(id)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        public static bool RelaunchAsAdmin()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = ExePath();
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                return true;
            }
            catch { return false; }
        }

        public static string ExePath()
        {
            return Process.GetCurrentProcess().MainModule.FileName;
        }

        public static string Expand(string p)
        {
            return Environment.ExpandEnvironmentVariables(p);
        }

        public static int Run(string exe, string args, Action<string> onLine)
        {
            return Run(exe, args, onLine, Encoding.UTF8);
        }

        // Kjører et program og sender stdout/stderr linje for linje til callback.
        public static int Run(string exe, string args, Action<string> onLine, Encoding enc)
        {
            ProcessStartInfo psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            psi.StandardOutputEncoding = enc;
            psi.StandardErrorEncoding = enc;
            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null && onLine != null) onLine(Clean(e.Data));
                    };
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null && onLine != null) onLine(Clean(e.Data));
                    };
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    p.WaitForExit();
                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                if (onLine != null) onLine("Kunne ikke kjøre " + exe + ": " + ex.Message);
                return -1;
            }
        }

        // Fjerner framdriftstegn og nulltegn som enkelte verktøy spyr ut.
        static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c == '\0' || c == '\b' || c == '\r') continue;
                if (c == '█' || c == '░' || c == '▒' || c == '▓') continue;
                sb.Append(c);
            }
            return sb.ToString().TrimEnd();
        }

        public static string RunCapture(string exe, string args, out int exitCode)
        {
            StringBuilder sb = new StringBuilder();
            exitCode = Run(exe, args, delegate(string l) { sb.AppendLine(l); });
            return sb.ToString();
        }

        public static void OpenPath(string path)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(path);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch { }
        }
    }
}
