using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text;

namespace Brisk
{
    // ==================================================================
    //  MINNE
    // ==================================================================
    public class MemSnapshot
    {
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong UsedPhys;
        public uint LoadPercent;
        public ulong Standby;      // cache som kan gjenbrukes umiddelbart
        public ulong Modified;     // må skrives til disk først
        public ulong CommitTotal;
        public ulong CommitLimit;
    }

    public class ProcMem
    {
        public string Name;
        public long Bytes;
        public int Count;
        public int Pid;
    }

    public static class MemoryTools
    {
        public static MemSnapshot Snapshot()
        {
            MemSnapshot s = new MemSnapshot();
            Native.MEMORYSTATUSEX m = Native.GetMemory();
            s.TotalPhys = m.ullTotalPhys;
            s.AvailPhys = m.ullAvailPhys;
            s.UsedPhys = m.ullTotalPhys - m.ullAvailPhys;
            s.LoadPercent = m.dwMemoryLoad;
            s.CommitLimit = m.ullTotalPageFile;
            s.CommitTotal = m.ullTotalPageFile - m.ullAvailPageFile;
            try
            {
                using (ManagementObjectSearcher q = new ManagementObjectSearcher(
                    "SELECT StandbyCacheCoreBytes, StandbyCacheNormalPriorityBytes, StandbyCacheReserveBytes, ModifiedPageListBytes FROM Win32_PerfRawData_PerfOS_Memory"))
                {
                    foreach (ManagementObject mo in q.Get())
                    {
                        s.Standby = Convert.ToUInt64(mo["StandbyCacheCoreBytes"])
                                  + Convert.ToUInt64(mo["StandbyCacheNormalPriorityBytes"])
                                  + Convert.ToUInt64(mo["StandbyCacheReserveBytes"]);
                        s.Modified = Convert.ToUInt64(mo["ModifiedPageListBytes"]);
                        break;
                    }
                }
            }
            catch { }
            return s;
        }

        public static List<ProcMem> TopProcesses(int take)
        {
            Dictionary<string, ProcMem> map = new Dictionary<string, ProcMem>(StringComparer.OrdinalIgnoreCase);
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    string n = p.ProcessName;
                    ProcMem pm;
                    if (!map.TryGetValue(n, out pm))
                    {
                        pm = new ProcMem();
                        pm.Name = n;
                        pm.Pid = p.Id;
                        map[n] = pm;
                    }
                    pm.Bytes += p.WorkingSet64;
                    pm.Count++;
                }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }
            List<ProcMem> list = new List<ProcMem>(map.Values);
            list.Sort(delegate(ProcMem a, ProcMem b) { return b.Bytes.CompareTo(a.Bytes); });
            if (list.Count > take) list.RemoveRange(take, list.Count - take);
            return list;
        }

        // Skyver ut arbeidssett for alle prosesser vi har tilgang til.
        public static int TrimAll()
        {
            int n = 0;
            foreach (Process p in Process.GetProcesses())
            {
                try { if (Native.TrimProcess(p)) n++; }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }
            Util.Log("Frigjorde arbeidssett for " + n + " prosesser.");
            return n;
        }

        public static bool PurgeStandby()
        {
            bool ok = Native.PurgeStandbyList();
            Util.Log("Tømming av standby-liste: " + (ok ? "OK" : "feilet (krever administrator)"));
            return ok;
        }
    }

    // ==================================================================
    //  WINGET — programoppdateringer
    // ==================================================================
    public class AppUpgrade
    {
        public string Name;
        public string Id;
        public string Current;
        public string Available;
        public string Source;
        public bool Selected = true;
    }

    public static class WingetTools
    {
        public static bool IsAvailable()
        {
            try
            {
                int code;
                Util.RunCapture("winget", "--version", out code);
                return code == 0;
            }
            catch { return false; }
        }

        public static List<AppUpgrade> ListUpgrades(out string note)
        {
            note = "";
            List<AppUpgrade> list = new List<AppUpgrade>();
            int code;
            string raw = Util.RunCapture("winget",
                "upgrade --include-unknown --accept-source-agreements --disable-interactivity", out code);

            string[] lines = raw.Replace("\r", "\n").Split('\n');
            int hdr = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                if (l.IndexOf("Name", StringComparison.Ordinal) >= 0 &&
                    l.IndexOf("Id", StringComparison.Ordinal) >= 0 &&
                    l.IndexOf("Version", StringComparison.Ordinal) >= 0 &&
                    (l.IndexOf("Available", StringComparison.Ordinal) >= 0 ||
                     l.IndexOf("Tilgjengelig", StringComparison.Ordinal) >= 0))
                { hdr = i; break; }
            }
            if (hdr < 0)
            {
                note = L.T("Fant ingen liste fra winget.");
                return list;
            }

            string header = lines[hdr];
            int cId = header.IndexOf("Id", StringComparison.Ordinal);
            int cVer = header.IndexOf("Version", StringComparison.Ordinal);
            int cAvail = header.IndexOf("Available", StringComparison.Ordinal);
            if (cAvail < 0) cAvail = header.IndexOf("Tilgjengelig", StringComparison.Ordinal);
            int cSrc = header.IndexOf("Source", StringComparison.Ordinal);
            if (cSrc < 0) cSrc = header.IndexOf("Kilde", StringComparison.Ordinal);
            if (cId < 0 || cVer < 0 || cAvail < 0) { note = L.T("Klarte ikke tolke winget-utdata."); return list; }

            for (int i = hdr + 1; i < lines.Length; i++)
            {
                string l = lines[i].TrimEnd();
                if (l.Length == 0) continue;
                if (l.TrimStart().StartsWith("-")) continue;
                if (l.Length <= cVer) continue;
                // Sluttlinjer som "12 upgrades available."
                if (l.IndexOf("upgrade", StringComparison.OrdinalIgnoreCase) >= 0 && l.Length < cId) continue;

                try
                {
                    AppUpgrade a = new AppUpgrade();
                    a.Name = l.Substring(0, Math.Min(cId, l.Length)).Trim();
                    a.Id = Sub(l, cId, cVer).Trim();
                    a.Current = Sub(l, cVer, cAvail).Trim();
                    a.Available = cSrc > cAvail ? Sub(l, cAvail, cSrc).Trim() : l.Substring(Math.Min(cAvail, l.Length)).Trim();
                    a.Source = cSrc > 0 && l.Length > cSrc ? l.Substring(cSrc).Trim() : "";
                    if (a.Id.Length == 0 || a.Name.Length == 0) continue;
                    if (a.Id.IndexOf(' ') >= 0) continue;                 // sannsynligvis en tekstlinje
                    if (a.Available.Length == 0) continue;
                    list.Add(a);
                }
                catch { }
            }
            if (list.Count == 0 && note.Length == 0)
                note = L.T("Alt winget kjenner til er oppdatert.");
            return list;
        }

        static string Sub(string s, int start, int end)
        {
            if (start >= s.Length) return "";
            if (end > s.Length) end = s.Length;
            if (end <= start) return "";
            return s.Substring(start, end - start);
        }

        public static bool Upgrade(AppUpgrade a, Action<string> onLine)
        {
            string args = "upgrade --id \"" + a.Id + "\" --silent --accept-package-agreements " +
                          "--accept-source-agreements --disable-interactivity";
            if (!string.IsNullOrEmpty(a.Source)) args += " --source " + a.Source;
            int code = Util.Run("winget", args, onLine);
            Util.Log("winget upgrade " + a.Id + " -> kode " + code);
            return code == 0;
        }
    }

    // ==================================================================
    //  VEDLIKEHOLD OG HELSE
    // ==================================================================
    public class DiskInfo
    {
        public string Name;
        public string Media;
        public string Health;
        public long Size;
    }

    public class VolumeInfo
    {
        public string Letter;
        public string Label;
        public long Total;
        public long Free;
    }

    public static class MaintenanceTools
    {
        public static List<VolumeInfo> Volumes()
        {
            List<VolumeInfo> l = new List<VolumeInfo>();
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady || d.DriveType != DriveType.Fixed) continue;
                    VolumeInfo v = new VolumeInfo();
                    v.Letter = d.Name;
                    v.Label = d.VolumeLabel;
                    v.Total = d.TotalSize;
                    v.Free = d.AvailableFreeSpace;
                    l.Add(v);
                }
                catch { }
            }
            return l;
        }

        public static List<DiskInfo> PhysicalDisks()
        {
            List<DiskInfo> l = new List<DiskInfo>();
            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                scope.Connect();
                ObjectQuery q = new ObjectQuery("SELECT FriendlyName, MediaType, HealthStatus, Size FROM MSFT_PhysicalDisk");
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(scope, q))
                {
                    foreach (ManagementObject mo in s.Get())
                    {
                        DiskInfo d = new DiskInfo();
                        d.Name = Convert.ToString(mo["FriendlyName"]);
                        int mt = 0;
                        try { mt = Convert.ToInt32(mo["MediaType"]); }
                        catch { }
                        d.Media = mt == 3 ? "HDD" : mt == 4 ? "SSD" : mt == 5 ? "SCM" : "Ukjent";
                        int hs = 0;
                        try { hs = Convert.ToInt32(mo["HealthStatus"]); }
                        catch { }
                        d.Health = hs == 0 ? "Frisk" : hs == 1 ? "Advarsel" : hs == 2 ? "Usunn" : "Ukjent";
                        try { d.Size = Convert.ToInt64(mo["Size"]); }
                        catch { }
                        l.Add(d);
                    }
                }
            }
            catch (Exception ex) { Util.Log("Kunne ikke lese diskhelse: " + ex.Message); }
            return l;
        }

        public static void CreateRestorePoint(Action<string> onLine)
        {
            onLine(L.T("Oppretter gjenopprettingspunkt."));
            string ps = "try { Enable-ComputerRestore -Drive \"$env:SystemDrive\\\" -ErrorAction SilentlyContinue; " +
                        "Checkpoint-Computer -Description 'Brisk' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop; " +
                        "Write-Output 'OK: gjenopprettingspunkt opprettet.' } catch { Write-Output ('Kunne ikke opprette punkt: ' + $_.Exception.Message) }";
            Util.Run("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"" + ps.Replace("\"", "\\\"") + "\"", onLine);
        }

        public static void RunSfc(Action<string> onLine)
        {
            onLine(L.T("Kjører sfc /scannow. Tar 5–15 minutter."));
            Util.Run("sfc", "/scannow", onLine, Encoding.Unicode);
        }

        public static void RunDismRestore(Action<string> onLine)
        {
            onLine(L.T("Kjører DISM /RestoreHealth. Kan ta lang tid."));
            Util.Run("dism.exe", "/Online /Cleanup-Image /RestoreHealth", onLine);
        }

        public static void RunComponentCleanup(Action<string> onLine)
        {
            onLine(L.T("Rydder komponentlageret (WinSxS)."));
            Util.Run("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup", onLine);
        }

        public static void OptimizeDrives(Action<string> onLine)
        {
            foreach (VolumeInfo v in Volumes())
            {
                string letter = v.Letter.TrimEnd('\\');
                onLine(L.F("Optimaliserer {0}", letter));
                Util.Run("defrag.exe", letter + " /O", onLine);
            }
        }

        public static void FlushDns(Action<string> onLine)
        {
            onLine(L.T("Tømmer DNS-cache."));
            Util.Run("ipconfig", "/flushdns", onLine);
        }
    }
}
