using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace Vaktmester
{
    public enum StartupKind { RegistryHKCU, RegistryHKLM, RegistryHKLM32, Folder, Task }

    public class StartupItem
    {
        public string Name;          // visningsnavn / verdinavn
        public string Command;       // hele kommandolinjen
        public string Publisher = "";
        public StartupKind Kind;
        public bool Enabled;
        public string TaskPath;      // kun for oppgaver
        public string FolderFile;    // kun for oppstartsmappe
        public string Note = "";     // advarsel hvis oppforingen er systemnaer
        public bool Critical;

        public string KindText
        {
            get
            {
                switch (Kind)
                {
                    case StartupKind.RegistryHKCU: return L.T("Register (denne brukeren)");
                    case StartupKind.RegistryHKLM: return L.T("Register (alle brukere)");
                    case StartupKind.RegistryHKLM32: return L.T("Register (32-bit)");
                    case StartupKind.Folder: return L.T("Oppstartsmappe");
                    default: return L.T("Planlagt oppgave");
                }
            }
        }
    }

    public static class StartupTools
    {
        const string ApprovedRun = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        const string ApprovedRun32 = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
        const string ApprovedFolder = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunKey32 = @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run";

        public static List<StartupItem> Enumerate(bool includeTasks)
        {
            List<StartupItem> list = new List<StartupItem>();

            ReadRun(Registry.CurrentUser, RunKey, StartupKind.RegistryHKCU, Registry.CurrentUser, ApprovedRun, list);
            ReadRun(Registry.LocalMachine, RunKey, StartupKind.RegistryHKLM, Registry.LocalMachine, ApprovedRun, list);
            ReadRun(Registry.LocalMachine, RunKey32, StartupKind.RegistryHKLM32, Registry.LocalMachine, ApprovedRun32, list);

            ReadFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), Registry.CurrentUser, list);
            ReadFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), Registry.LocalMachine, list);

            if (includeTasks) ReadTasks(list);

            foreach (StartupItem i in list)
            {
                i.Publisher = FileInfoOf(i.Command);
                i.Note = NoteFor(i);
                i.Critical = i.Note.Length > 0;
            }
            return list;
        }

        // Oppforinger som gjor noe brukeren faktisk trenger. Ikke sperret,
        // men markert - blindt avslag pa disse skaper problemer.
        static readonly string[][] Sensitive =
        {
            new string[] { "securityhealth",  "Windows Sikkerhet-ikonet" },
            new string[] { "rtkaud",          "Lydbehandling (Realtek)" },
            new string[] { "realtek",         "Lydbehandling (Realtek)" },
            new string[] { "wavessvc",        "Lydbehandling (Waves)" },
            new string[] { "nahimic",         "Lydbehandling (Nahimic)" },
            new string[] { "syntp",           "Styreplate" },
            new string[] { "etdctrl",         "Styreplate" },
            new string[] { "apoint",          "Styreplate" },
            new string[] { "elan",            "Styreplate" },
            new string[] { "igfxtray",        "Intel-grafikk" },
            new string[] { "hotkeyscmds",     "Hurtigtaster for grafikk" },
            new string[] { "nvdisplay",       "NVIDIA-grafikk" },
            new string[] { "nvbackend",       "NVIDIA-grafikk" },
            new string[] { "amdow",           "AMD-grafikk" },
            new string[] { "startupcheck",    "Antivirus" },
            new string[] { "defender",        "Antivirus" },
            new string[] { "avast",           "Antivirus" },
            new string[] { "bitdefender",     "Antivirus" },
            new string[] { "onedrive",        "Skysynkronisering" },
            new string[] { "1password",       "Passordbehandler" },
            new string[] { "bitwarden",       "Passordbehandler" },
            new string[] { "lastpass",        "Passordbehandler" },
        };

        static string NoteFor(StartupItem it)
        {
            string hay = ((it.Name ?? "") + " " + (it.Command ?? "")).ToLowerInvariant();
            foreach (string[] row in Sensitive)
                if (hay.IndexOf(row[0], StringComparison.Ordinal) >= 0) return L.T(row[1]);
            return "";
        }

        static void ReadRun(RegistryKey root, string path, StartupKind kind, RegistryKey approvedRoot,
            string approvedPath, List<StartupItem> list)
        {
            try
            {
                using (RegistryKey k = root.OpenSubKey(path))
                {
                    if (k == null) return;
                    RegistryKey ap = approvedRoot.OpenSubKey(approvedPath);
                    foreach (string name in k.GetValueNames())
                    {
                        object v = k.GetValue(name);
                        if (v == null) continue;
                        StartupItem it = new StartupItem();
                        it.Name = name;
                        it.Command = v.ToString();
                        it.Kind = kind;
                        it.Enabled = IsApproved(ap, name);
                        list.Add(it);
                    }
                    if (ap != null) ap.Close();
                }
            }
            catch { }
        }

        static void ReadFolder(string dir, RegistryKey approvedRoot, List<StartupItem> list)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            RegistryKey ap = null;
            try { ap = approvedRoot.OpenSubKey(ApprovedFolder); } catch { }
            try
            {
                foreach (string f in Directory.GetFiles(dir))
                {
                    string fn = Path.GetFileName(f);
                    if (fn.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    StartupItem it = new StartupItem();
                    it.Name = Path.GetFileNameWithoutExtension(f);
                    it.Command = f;
                    it.FolderFile = fn;
                    it.Kind = StartupKind.Folder;
                    it.Enabled = IsApproved(ap, fn);
                    list.Add(it);
                }
            }
            catch { }
            finally { if (ap != null) ap.Close(); }
        }

        static bool IsApproved(RegistryKey approved, string name)
        {
            if (approved == null) return true;
            try
            {
                byte[] b = approved.GetValue(name) as byte[];
                if (b == null || b.Length == 0) return true;
                return (b[0] & 0x01) == 0;      // oddetall i byte 0 = deaktivert
            }
            catch { return true; }
        }

        // ---------------------------------------------------------------
        public static bool SetEnabled(StartupItem it, bool enable)
        {
            try
            {
                if (it.Kind == StartupKind.Task) return SetTaskEnabled(it.TaskPath, enable);

                RegistryKey root;
                string approvedPath;
                string valueName;

                switch (it.Kind)
                {
                    case StartupKind.RegistryHKCU:
                        root = Registry.CurrentUser; approvedPath = ApprovedRun; valueName = it.Name; break;
                    case StartupKind.RegistryHKLM:
                        root = Registry.LocalMachine; approvedPath = ApprovedRun; valueName = it.Name; break;
                    case StartupKind.RegistryHKLM32:
                        root = Registry.LocalMachine; approvedPath = ApprovedRun32; valueName = it.Name; break;
                    default:
                        root = File.Exists(Path.Combine(
                                   Environment.GetFolderPath(Environment.SpecialFolder.Startup) ?? "", it.FolderFile ?? ""))
                               ? Registry.CurrentUser : Registry.LocalMachine;
                        approvedPath = ApprovedFolder; valueName = it.FolderFile; break;
                }

                using (RegistryKey k = root.CreateSubKey(approvedPath))
                {
                    if (k == null) return false;
                    byte[] val = new byte[12];
                    val[0] = enable ? (byte)0x02 : (byte)0x03;
                    if (!enable)
                    {
                        long ft = DateTime.Now.ToFileTime();
                        byte[] fb = BitConverter.GetBytes(ft);
                        Array.Copy(fb, 0, val, 4, 8);
                    }
                    k.SetValue(valueName, val, RegistryValueKind.Binary);
                }
                it.Enabled = enable;
                Util.Log((enable ? "Aktiverte" : "Deaktiverte") + " oppstart: " + it.Name);
                return true;
            }
            catch (Exception ex)
            {
                Util.Log("Klarte ikke endre oppstart for " + it.Name + ": " + ex.Message);
                return false;
            }
        }

        // ---------------------------------------------------------------
        static void ReadTasks(List<StartupItem> list)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("Schedule.Service");
                if (t == null) return;
                dynamic svc = Activator.CreateInstance(t);
                svc.Connect();
                WalkFolder(svc.GetFolder("\\"), list, 0);
            }
            catch (Exception ex) { Util.Log("Kunne ikke lese planlagte oppgaver: " + ex.Message); }
        }

        static void WalkFolder(dynamic folder, List<StartupItem> list, int depth)
        {
            if (depth > 4) return;
            string fpath = "";
            try { fpath = folder.Path; } catch { }
            // Hopp over Microsofts egne oppgaver - de hoerer til systemet.
            if (fpath.StartsWith("\\Microsoft", StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                foreach (dynamic task in folder.GetTasks(1))
                {
                    try
                    {
                        bool logon = false;
                        foreach (dynamic tr in task.Definition.Triggers)
                        {
                            int ty = (int)tr.Type;
                            if (ty == 9 || ty == 8) { logon = true; break; }   // LOGON eller BOOT
                        }
                        if (!logon) continue;

                        string cmd = "";
                        foreach (dynamic ac in task.Definition.Actions)
                        {
                            try { cmd = ac.Path + " " + ac.Arguments; } catch { }
                            break;
                        }

                        StartupItem it = new StartupItem();
                        it.Name = task.Name;
                        it.Command = (cmd ?? "").Trim();
                        it.Kind = StartupKind.Task;
                        it.TaskPath = task.Path;
                        it.Enabled = task.Enabled;
                        list.Add(it);
                    }
                    catch { }
                }
            }
            catch { }

            try
            {
                foreach (dynamic sub in folder.GetFolders(0)) WalkFolder(sub, list, depth + 1);
            }
            catch { }
        }

        static bool SetTaskEnabled(string taskPath, bool enable)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("Schedule.Service");
                dynamic svc = Activator.CreateInstance(t);
                svc.Connect();
                int slash = taskPath.LastIndexOf('\\');
                string folder = slash <= 0 ? "\\" : taskPath.Substring(0, slash);
                string name = taskPath.Substring(slash + 1);
                dynamic f = svc.GetFolder(folder);
                dynamic task = f.GetTask(name);
                task.Enabled = enable;
                Util.Log((enable ? "Aktiverte" : "Deaktiverte") + " oppgave: " + taskPath);
                return true;
            }
            catch (Exception ex)
            {
                Util.Log("Klarte ikke endre oppgave " + taskPath + ": " + ex.Message);
                return false;
            }
        }

        // ---------------------------------------------------------------
        // Trekker ut exe-sti fra en kommandolinje og henter selskapsnavn.
        public static string FileInfoOf(string command)
        {
            string exe = ExtractExe(command);
            if (exe == null) return "";
            try
            {
                System.Diagnostics.FileVersionInfo fv = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe);
                string c = fv.CompanyName;
                return string.IsNullOrEmpty(c) ? "" : c.Trim();
            }
            catch { return ""; }
        }

        public static string ExtractExe(string command)
        {
            if (string.IsNullOrEmpty(command)) return null;
            string c = command.Trim();
            try
            {
                if (c.StartsWith("\""))
                {
                    int end = c.IndexOf('"', 1);
                    if (end > 1) c = c.Substring(1, end - 1);
                }
                else
                {
                    int idx = c.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                    if (idx > 0) c = c.Substring(0, idx + 4);
                    else
                    {
                        int sp = c.IndexOf(' ');
                        if (sp > 0) c = c.Substring(0, sp);
                    }
                }
                c = Util.Expand(c);
                return File.Exists(c) ? c : null;
            }
            catch { return null; }
        }
    }
}
