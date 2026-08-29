using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace Brisk
{
    // ==================================================================
    //  WINDOWS-OPPDATERINGER (ikke drivere)
    // ==================================================================
    public class WinUpdate
    {
        public string Title;
        public string Severity = "";
        public long Size;
        public bool Mandatory;
        public object Update;
    }

    public static class UpdateTools
    {
        public static List<WinUpdate> Search(out string note)
        {
            note = "";
            List<WinUpdate> list = new List<WinUpdate>();
            try
            {
                Type t = Type.GetTypeFromProgID("Microsoft.Update.Session");
                dynamic session = Activator.CreateInstance(t);
                session.ClientApplicationID = "Brisk";
                dynamic searcher = session.CreateUpdateSearcher();
                searcher.Online = true;
                dynamic res = searcher.Search("IsInstalled=0 and Type='Software' and IsHidden=0");

                foreach (dynamic u in res.Updates)
                {
                    try
                    {
                        WinUpdate w = new WinUpdate();
                        w.Title = Convert.ToString(u.Title);
                        w.Update = u;
                        try { w.Size = Convert.ToInt64(u.MaxDownloadSize); }
                        catch { }
                        try { w.Severity = Convert.ToString(u.MsrcSeverity); }
                        catch { }
                        try { w.Mandatory = Convert.ToBoolean(u.IsMandatory); }
                        catch { }
                        list.Add(w);
                    }
                    catch { }
                }
                if (list.Count == 0)
                    note = L.T("Windows er oppdatert.");
            }
            catch (Exception ex)
            {
                note = L.T("Oppdateringssøket feilet: ") + ex.Message;
                Util.Log(note);
            }
            return list;
        }

        public static int Install(List<WinUpdate> chosen, out bool reboot, Action<string> progress)
        {
            reboot = false;
            if (chosen == null || chosen.Count == 0) return 0;
            try
            {
                dynamic session = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.Session"));
                session.ClientApplicationID = "Brisk";
                dynamic coll = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.UpdateColl"));
                foreach (WinUpdate w in chosen)
                {
                    dynamic u = w.Update;
                    try { if (!(bool)u.EulaAccepted) u.AcceptEula(); }
                    catch { }
                    coll.Add(u);
                }

                if (progress != null) progress(L.F("Laster ned {0} oppdatering(er).", coll.Count));
                dynamic dl = session.CreateUpdateDownloader();
                dl.Updates = coll;
                dl.Download();

                dynamic ready = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.UpdateColl"));
                foreach (dynamic u in coll)
                {
                    try { if ((bool)u.IsDownloaded) ready.Add(u); }
                    catch { }
                }
                if (ready.Count == 0) { if (progress != null) progress(L.T("Ingenting ble lastet ned.")); return 0; }

                if (progress != null) progress(L.T("Installerer."));
                dynamic inst = session.CreateUpdateInstaller();
                inst.Updates = ready;
                dynamic ires = inst.Install();
                reboot = (bool)ires.RebootRequired;
                int ok = 0;
                for (int i = 0; i < ready.Count; i++)
                {
                    try
                    {
                        int rc = Convert.ToInt32(ires.GetUpdateResult(i).ResultCode);
                        if (rc == 2 || rc == 3) ok++;
                    }
                    catch { }
                }
                Util.Log("Installerte " + ok + " Windows-oppdatering(er).");
                return ok;
            }
            catch (Exception ex)
            {
                if (progress != null) progress(L.T("Installasjon feilet: ") + ex.Message);
                return 0;
            }
        }
    }

    // ==================================================================
    //  DISKPLASS
    // ==================================================================
    public class SizeEntry
    {
        public string Path;
        public string Name;
        public long Size;
        public bool IsFolder;
        public int Files;
    }

    public static class DiskTools
    {
        // Går gjennom treet én gang og samler både mappe- og filstørrelser.
        public static void Scan(string root, CancellationToken ct, Action<string> progress,
            out List<SizeEntry> folders, out List<SizeEntry> files)
        {
            List<SizeEntry> fo = new List<SizeEntry>();
            List<SizeEntry> fi = new List<SizeEntry>();
            Walk(root, root, 0, ct, progress, fo, fi);

            fo.Sort(delegate(SizeEntry a, SizeEntry b) { return b.Size.CompareTo(a.Size); });
            fi.Sort(delegate(SizeEntry a, SizeEntry b) { return b.Size.CompareTo(a.Size); });
            if (fo.Count > 60) fo.RemoveRange(60, fo.Count - 60);
            if (fi.Count > 60) fi.RemoveRange(60, fi.Count - 60);
            folders = fo;
            files = fi;
        }

        const long BigFile = 100L * 1024 * 1024;     // filer over 100 MB er interessante

        static string Relative(string root, string dir)
        {
            if (dir.Length > root.Length && dir.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return dir.Substring(root.Length).TrimStart('\\', '/');
            return dir;
        }

        static long Walk(string root, string dir, int depth, CancellationToken ct, Action<string> progress,
            List<SizeEntry> folders, List<SizeEntry> files)
        {
            ct.ThrowIfCancellationRequested();
            long total = 0;
            int count = 0;

            try
            {
                DirectoryInfo di = new DirectoryInfo(dir);
                if ((di.Attributes & FileAttributes.ReparsePoint) != 0) return 0;
            }
            catch { return 0; }

            if (depth <= 1 && progress != null) progress(dir);

            try
            {
                foreach (string f in Directory.GetFiles(dir))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(f);
                        if ((fi.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                        total += fi.Length;
                        count++;
                        if (fi.Length >= BigFile)
                        {
                            SizeEntry e = new SizeEntry();
                            e.Path = f; e.Name = fi.Name; e.Size = fi.Length; e.IsFolder = false;
                            files.Add(e);
                            if (files.Count > 4000)
                            {
                                files.Sort(delegate(SizeEntry a, SizeEntry b) { return b.Size.CompareTo(a.Size); });
                                files.RemoveRange(2000, files.Count - 2000);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            try
            {
                foreach (string d in Directory.GetDirectories(dir))
                    total += Walk(root, d, depth + 1, ct, progress, folders, files);
            }
            catch { }

            // Bare mapper nær toppen er nyttige å vise — ellers drukner lista.
            // Rota selv hoppes over; den sier bare hvor stort det du skannet er.
            if (depth >= 1 && depth <= 3 && total > 50L * 1024 * 1024)
            {
                SizeEntry e = new SizeEntry();
                e.Path = dir;
                e.Name = Relative(root, dir);
                e.Size = total;
                e.IsFolder = true;
                e.Files = count;
                folders.Add(e);
            }
            return total;
        }
    }

    // ==================================================================
    //  DUPLIKATER OG GLEMTE FILER
    // ==================================================================
    public class DupGroup
    {
        public long Size;
        public List<string> Files = new List<string>();
        public long Wasted { get { return Size * Math.Max(0, Files.Count - 1); } }
    }

    public static class DupTools
    {
        // Filer under denne grensen er ikke verdt tiden det tar å lese dem.
        const long MinSize = 2L * 1024 * 1024;

        public static List<DupGroup> Find(string root, CancellationToken ct, Action<string> progress)
        {
            // Steg 1: grupper på nøyaktig størrelse. To filer med ulik lengde kan
            // aldri være like, så dette luker bort det meste helt gratis.
            Dictionary<long, List<string>> bySize = new Dictionary<long, List<string>>();
            Collect(root, 0, ct, progress, bySize);

            List<DupGroup> result = new List<DupGroup>();
            foreach (KeyValuePair<long, List<string>> kv in bySize)
            {
                ct.ThrowIfCancellationRequested();
                if (kv.Value.Count < 2) continue;

                // Steg 2: hash de første 64 kB. Skiller nesten alltid.
                Dictionary<string, List<string>> byHead = Group(kv.Value, ct, 65536);
                foreach (KeyValuePair<string, List<string>> hk in byHead)
                {
                    if (hk.Value.Count < 2) continue;

                    // Steg 3: full hash på det som fortsatt ser likt ut.
                    Dictionary<string, List<string>> full = Group(hk.Value, ct, 0);
                    foreach (KeyValuePair<string, List<string>> fk in full)
                    {
                        if (fk.Value.Count < 2) continue;
                        DupGroup g = new DupGroup();
                        g.Size = kv.Key;
                        g.Files = fk.Value;
                        result.Add(g);
                    }
                }
            }

            result.Sort(delegate(DupGroup a, DupGroup b) { return b.Wasted.CompareTo(a.Wasted); });
            if (result.Count > 300) result.RemoveRange(300, result.Count - 300);
            return result;
        }

        static void Collect(string dir, int depth, CancellationToken ct, Action<string> progress,
            Dictionary<long, List<string>> bySize)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                DirectoryInfo di = new DirectoryInfo(dir);
                if ((di.Attributes & FileAttributes.ReparsePoint) != 0) return;
            }
            catch { return; }

            if (depth <= 1 && progress != null) progress(dir);

            try
            {
                foreach (string f in Directory.GetFiles(dir))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(f);
                        if (fi.Length < MinSize) continue;
                        if ((fi.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                        List<string> l;
                        if (!bySize.TryGetValue(fi.Length, out l))
                        {
                            l = new List<string>();
                            bySize[fi.Length] = l;
                        }
                        l.Add(f);
                    }
                    catch { }
                }
            }
            catch { }

            try
            {
                foreach (string d in Directory.GetDirectories(dir))
                    Collect(d, depth + 1, ct, progress, bySize);
            }
            catch { }
        }

        static Dictionary<string, List<string>> Group(List<string> files, CancellationToken ct, int bytes)
        {
            Dictionary<string, List<string>> d = new Dictionary<string, List<string>>();
            foreach (string f in files)
            {
                ct.ThrowIfCancellationRequested();
                string h = Hash(f, bytes);
                if (h == null) continue;
                List<string> l;
                if (!d.TryGetValue(h, out l)) { l = new List<string>(); d[h] = l; }
                l.Add(f);
            }
            return d;
        }

        // bytes = 0 gir full hash.
        static string Hash(string path, int bytes)
        {
            try
            {
                using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
                using (FileStream fs = File.OpenRead(path))
                {
                    byte[] hash;
                    if (bytes <= 0) hash = md5.ComputeHash(fs);
                    else
                    {
                        byte[] buf = new byte[bytes];
                        int n = fs.Read(buf, 0, bytes);
                        hash = md5.ComputeHash(buf, 0, n);
                    }
                    return BitConverter.ToString(hash);
                }
            }
            catch { return null; }
        }

        // Filer som ikke er rørt på lenge. Standard er nedlastingsmappa.
        public static List<SizeEntry> Forgotten(string dir, int days, CancellationToken ct)
        {
            List<SizeEntry> list = new List<SizeEntry>();
            DateTime limit = DateTime.Now.AddDays(-days);
            Sweep(dir, 0, limit, ct, list);
            list.Sort(delegate(SizeEntry a, SizeEntry b) { return b.Size.CompareTo(a.Size); });
            if (list.Count > 200) list.RemoveRange(200, list.Count - 200);
            return list;
        }

        static void Sweep(string dir, int depth, DateTime limit, CancellationToken ct, List<SizeEntry> list)
        {
            if (depth > 4) return;
            ct.ThrowIfCancellationRequested();
            try
            {
                foreach (string f in Directory.GetFiles(dir))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(f);
                        if (fi.Length < 10L * 1024 * 1024) continue;
                        DateTime touched = fi.LastAccessTime > fi.LastWriteTime
                            ? fi.LastAccessTime : fi.LastWriteTime;
                        if (touched > limit) continue;
                        SizeEntry e = new SizeEntry();
                        e.Path = f;
                        e.Name = fi.Name;
                        e.Size = fi.Length;
                        e.Files = (int)(DateTime.Now - touched).TotalDays;
                        list.Add(e);
                    }
                    catch { }
                }
                foreach (string d in Directory.GetDirectories(dir))
                    Sweep(d, depth + 1, limit, ct, list);
            }
            catch { }
        }
    }

    // ==================================================================
    //  INSTALLERTE PROGRAMMER
    // ==================================================================
    public class InstalledApp
    {
        public string Name;
        public string Version = "";
        public string Publisher = "";
        public long EstimatedSize;      // bytes
        public string UninstallCmd = "";
        public string QuietUninstallCmd = "";
        public DateTime Installed;
        public string Location = "";
    }

    public static class AppInventory
    {
        public static List<InstalledApp> List()
        {
            Dictionary<string, InstalledApp> map = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);
            Read(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall", map);
            Read(Registry.LocalMachine, @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall", map);
            Read(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall", map);

            List<InstalledApp> l = new List<InstalledApp>(map.Values);
            l.Sort(delegate(InstalledApp a, InstalledApp b) { return b.EstimatedSize.CompareTo(a.EstimatedSize); });
            return l;
        }

        static void Read(RegistryKey root, string path, Dictionary<string, InstalledApp> map)
        {
            try
            {
                using (RegistryKey k = root.OpenSubKey(path))
                {
                    if (k == null) return;
                    foreach (string sub in k.GetSubKeyNames())
                    {
                        try
                        {
                            using (RegistryKey s = k.OpenSubKey(sub))
                            {
                                if (s == null) continue;
                                string name = Convert.ToString(s.GetValue("DisplayName"));
                                if (string.IsNullOrEmpty(name)) continue;
                                if (Convert.ToString(s.GetValue("SystemComponent")) == "1") continue;
                                if (s.GetValue("ParentKeyName") != null) continue;
                                string un = Convert.ToString(s.GetValue("UninstallString"));
                                string qun = Convert.ToString(s.GetValue("QuietUninstallString"));
                                if (string.IsNullOrEmpty(un) && string.IsNullOrEmpty(qun)) continue;

                                InstalledApp a = new InstalledApp();
                                a.Name = name.Trim();
                                a.Version = Convert.ToString(s.GetValue("DisplayVersion"));
                                a.Publisher = Convert.ToString(s.GetValue("Publisher"));
                                a.UninstallCmd = un;
                                a.QuietUninstallCmd = qun;
                                a.Location = Convert.ToString(s.GetValue("InstallLocation"));
                                try
                                {
                                    object es = s.GetValue("EstimatedSize");
                                    if (es != null) a.EstimatedSize = Convert.ToInt64(es) * 1024L;
                                }
                                catch { }
                                try
                                {
                                    string d = Convert.ToString(s.GetValue("InstallDate"));
                                    if (d != null && d.Length == 8)
                                        a.Installed = new DateTime(int.Parse(d.Substring(0, 4)),
                                            int.Parse(d.Substring(4, 2)), int.Parse(d.Substring(6, 2)));
                                }
                                catch { }
                                map[a.Name + "|" + a.Version] = a;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        // Starter programmets egen avinstallering. Vi kjører den synlig med vilje:
        // stille avinstallering av vilkårlige pakker er ikke trygt å gjette seg til.
        public static bool StartUninstall(InstalledApp a)
        {
            string cmd = !string.IsNullOrEmpty(a.QuietUninstallCmd) ? a.QuietUninstallCmd : a.UninstallCmd;
            if (string.IsNullOrEmpty(cmd)) return false;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c " + cmd);
                psi.UseShellExecute = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
                Util.Log("Startet avinstallering: " + a.Name);
                return true;
            }
            catch (Exception ex)
            {
                Util.Log("Avinstallering feilet for " + a.Name + ": " + ex.Message);
                return false;
            }
        }
    }

    // ==================================================================
    //  PLANLAGT AUTOMATISK RYDDING
    // ==================================================================
    public static class ScheduleTools
    {
        public const string TaskName = "Brisk ukentlig rydding";

        public static bool Exists()
        {
            int code;
            Util.RunCapture("schtasks", "/Query /TN \"" + TaskName + "\"", out code);
            return code == 0;
        }

        public static bool Create(string dayCode, string time, Action<string> log)
        {
            string exe = Util.ExePath();
            string args = "/Create /F /TN \"" + TaskName + "\" /TR \"\\\"" + exe + "\\\" /auto\" " +
                          "/SC WEEKLY /D " + dayCode + " /ST " + time;
            int code = Util.Run("schtasks", args, log);
            Util.Log("Opprettet planlagt rydding (" + dayCode + " " + time + "): kode " + code);
            return code == 0;
        }

        public static bool Remove(Action<string> log)
        {
            int code = Util.Run("schtasks", "/Delete /F /TN \"" + TaskName + "\"", log);
            Util.Log("Fjernet planlagt rydding: kode " + code);
            return code == 0;
        }
    }

    // ==================================================================
    //  SYSTEMRAPPORT
    // ==================================================================
    public static class Report
    {
        public static string Build()
        {
            StringBuilder b = new StringBuilder();
            b.AppendLine("VAKTMESTER — SYSTEMRAPPORT");
            b.AppendLine("Laget " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            b.AppendLine(new string('=', 62));
            b.AppendLine();

            b.AppendLine("MASKIN");
            b.AppendLine("  Navn        : " + Environment.MachineName);
            b.AppendLine("  Windows     : " + Wmi("Win32_OperatingSystem", "Caption") +
                         "  (build " + Environment.OSVersion.Version.Build + ")");
            b.AppendLine("  Prosessor   : " + Wmi("Win32_Processor", "Name"));
            b.AppendLine("  Hovedkort   : " + Wmi("Win32_BaseBoard", "Manufacturer") + " " +
                         Wmi("Win32_BaseBoard", "Product"));
            b.AppendLine("  Grafikk     : " + Wmi("Win32_VideoController", "Name"));
            MemSnapshot m = MemoryTools.Snapshot();
            b.AppendLine("  Minne       : " + Util.Bytes(m.TotalPhys) + " totalt, " +
                         Util.Bytes(m.AvailPhys) + " ledig (" + m.LoadPercent + " % i bruk)");
            b.AppendLine();

            b.AppendLine("DISKER");
            foreach (DiskInfo d in MaintenanceTools.PhysicalDisks())
                b.AppendLine("  " + d.Name + " — " + d.Media + ", helse: " + d.Health + ", " + Util.Bytes(d.Size));
            foreach (VolumeInfo v in MaintenanceTools.Volumes())
                b.AppendLine("  " + v.Letter + " " + Util.Bytes(v.Free) + " ledig av " + Util.Bytes(v.Total));
            b.AppendLine();

            b.AppendLine("OPPSTARTSPROGRAMMER");
            try
            {
                foreach (StartupItem it in StartupTools.Enumerate(false))
                    b.AppendLine("  [" + (it.Enabled ? "PÅ " : "av ") + "] " + it.Name +
                                 (it.Publisher.Length > 0 ? "  (" + it.Publisher + ")" : ""));
            }
            catch { }
            b.AppendLine();

            b.AppendLine("ENHETER MED PROBLEM");
            List<ProblemDevice> devs = DriverTools.FindProblemDevices();
            if (devs.Count == 0) b.AppendLine("  Ingen.");
            foreach (ProblemDevice d in devs)
                b.AppendLine("  " + d.Name + " — " + d.ErrorText);
            b.AppendLine();

            b.AppendLine("STØRSTE INSTALLERTE PROGRAMMER");
            int n = 0;
            foreach (InstalledApp a in AppInventory.List())
            {
                if (n++ >= 20) break;
                b.AppendLine("  " + Util.Bytes(a.EstimatedSize).PadLeft(9) + "  " + a.Name +
                             (a.Version.Length > 0 ? " " + a.Version : ""));
            }
            b.AppendLine();
            b.AppendLine("Rapporten inneholder ingen personlige filer, passord eller nettadresser.");
            return b.ToString();
        }

        static string Wmi(string cls, string prop)
        {
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT " + prop + " FROM " + cls))
                    foreach (ManagementObject mo in s.Get())
                    {
                        string v = Convert.ToString(mo[prop]);
                        if (!string.IsNullOrEmpty(v)) return v.Trim();
                    }
            }
            catch { }
            return "(ukjent)";
        }
    }

    // ==================================================================
    //  STILLE RYDDING (/auto)
    // ==================================================================
    public static class AutoClean
    {
        public static int Run()
        {
            Util.Log("=== Automatisk rydding startet ===");
            long freed = 0;
            int deleted = 0;
            CancellationTokenSource cts = new CancellationTokenSource();
            foreach (CleanTarget t in Cleaner.BuildTargets())
            {
                if (t.Risk != Risk.Trygg) continue;          // aldri Windows.old uten samtykke
                if (!t.Auto) continue;                       // nettleser-cache o.l. tas ikke automatisk
                try
                {
                    Cleaner.CleanResult r = Cleaner.Clean(t, cts.Token, null);
                    freed += r.Freed;
                    deleted += r.Deleted;
                }
                catch (Exception ex) { Util.Log("Auto: " + t.Name + " feilet: " + ex.Message); }
            }
            Util.Log("=== Automatisk rydding ferdig: " + Util.Bytes(freed) + " frigjort, " +
                     deleted + " filer ===");
            return 0;
        }
    }
}
