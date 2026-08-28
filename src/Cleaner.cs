using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Brisk
{
    public enum Risk { Trygg, Merk }

    // Ett ryddemaal: en samling mapper/filmoenstre som kan slettes.
    public class CleanTarget
    {
        public string Name;
        public string Info;
        public Risk Risk = Risk.Trygg;
        public bool DefaultChecked = true;
        public bool Special;                 // haandteres av egen kode (papirkurv)
        public string SpecialKey;
        public List<CleanRule> Rules = new List<CleanRule>();

        public long FoundBytes;
        public int FoundFiles;
        public bool Scanned;

        public CleanTarget(string name, string info) { Name = name; Info = info; }

        public CleanTarget Dir(string path) { Rules.Add(new CleanRule(path, "*", true, false)); return this; }
        public CleanTarget Files(string path, string pattern)
        {
            Rules.Add(new CleanRule(path, pattern, false, false)); return this;
        }
        public CleanTarget DirAndSelf(string path) { Rules.Add(new CleanRule(path, "*", true, true)); return this; }
    }

    public class CleanRule
    {
        public string Path;      // kan inneholde miljoevariabler og stjerne-ledd
        public string Pattern;
        public bool Recursive;
        public bool RemoveRoot;
        public CleanRule(string p, string pat, bool rec, bool removeRoot)
        { Path = p; Pattern = pat; Recursive = rec; RemoveRoot = removeRoot; }
    }

    public static class Cleaner
    {
        // Mapper som ALDRI skal roeres, uansett hva en regel sier.
        static readonly string[] Forbidden = BuildForbidden();

        static string[] BuildForbidden()
        {
            List<string> l = new List<string>();
            l.Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            l.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            l.Add(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            l.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
            l.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
            l.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
            l.Add(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            l.Add(Environment.GetFolderPath(Environment.SpecialFolder.System));
            l.Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            l.Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
            l.Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
            l.Add(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            l.Add(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            l.Add(Util.Expand("%SystemDrive%\\"));
            List<string> res = new List<string>();
            foreach (string s in l)
                if (!string.IsNullOrEmpty(s)) res.Add(Norm(s));
            return res.ToArray();
        }

        static string Norm(string p)
        {
            try { p = Path.GetFullPath(p); } catch { }
            return p.TrimEnd('\\', '/').ToLowerInvariant();
        }

        static bool IsForbidden(string path)
        {
            string n = Norm(path);
            if (n.Length < 4) return true;               // f.eks. "c:"
            foreach (string f in Forbidden) if (n == f) return true;
            return false;
        }

        // ---------------------------------------------------------------
        public static List<CleanTarget> BuildTargets()
        {
            List<CleanTarget> t = new List<CleanTarget>();

            t.Add(new CleanTarget("Midlertidige filer (bruker)",
                "%TEMP%. Rester etter installasjoner og programmer.")
                .Dir("%LOCALAPPDATA%\\Temp"));

            t.Add(new CleanTarget("Midlertidige filer (Windows)",
                "C:\\Windows\\Temp.")
                .Dir("%SystemRoot%\\Temp"));

            CleanTarget rb = new CleanTarget("Papirkurv", "Alle disker.");
            rb.Special = true; rb.SpecialKey = "recyclebin";
            t.Add(rb);

            t.Add(new CleanTarget("Windows Update-nedlastinger",
                "Ferdig installerte pakker. Lastes ned igjen hvis det trengs.")
                .Dir("%SystemRoot%\\SoftwareDistribution\\Download"));

            t.Add(new CleanTarget("Delivery Optimization",
                "Oppdateringer mellomlagret for deling på nettverket.")
                .Dir("%SystemRoot%\\ServiceProfiles\\NetworkService\\AppData\\Local\\Microsoft\\Windows\\DeliveryOptimization"));

            // Dumpene er grunnlaget for blåskjermanalysen under Helse, så de
            // er merket og tas aldri av den automatiske ryddingen.
            CleanTarget dumps = new CleanTarget("Krasjdumper",
                "Dumpfiler fra kræsj og blåskjerm. Sletter du dem, mister Helse grunnlaget for å analysere blåskjermene dine.");
            dumps.Risk = Risk.Merk;
            dumps.DefaultChecked = false;
            dumps.Dir("%LOCALAPPDATA%\\CrashDumps");
            dumps.Dir(DumpTools.MinidumpFolder());
            dumps.Files(System.IO.Path.GetDirectoryName(DumpTools.FullDumpFile()),
                        System.IO.Path.GetFileName(DumpTools.FullDumpFile()));
            t.Add(dumps);

            t.Add(new CleanTarget("Feilrapportering",
                "Rapporter som lå i kø til Microsoft.")
                .Dir("%LOCALAPPDATA%\\Microsoft\\Windows\\WER")
                .Dir("%ProgramData%\\Microsoft\\Windows\\WER\\ReportQueue")
                .Dir("%ProgramData%\\Microsoft\\Windows\\WER\\ReportArchive"));

            t.Add(new CleanTarget("Systemlogger",
                "CBS, Windows Update og DISM. Blir fort hundrevis av MB.")
                .Files("%SystemRoot%\\Logs\\CBS", "*.log")
                .Files("%SystemRoot%\\Logs\\CBS", "*.cab")
                .Dir("%SystemRoot%\\Logs\\WindowsUpdate")
                .Files("%SystemRoot%\\Logs\\DISM", "*.log")
                .Files("%SystemRoot%", "*.log"));

            t.Add(new CleanTarget("Miniatyrbilde- og ikon-cache",
                "Bygges opp igjen. Fikser gale miniatyrbilder.")
                .Files("%LOCALAPPDATA%\\Microsoft\\Windows\\Explorer", "thumbcache_*.db")
                .Files("%LOCALAPPDATA%\\Microsoft\\Windows\\Explorer", "iconcache_*.db"));

            t.Add(new CleanTarget("Grafikk-cache",
                "Kompilerte shadere fra DirectX, NVIDIA og AMD. Bygges opp igjen.")
                .Dir("%LOCALAPPDATA%\\D3DSCache")
                .Dir("%LOCALAPPDATA%\\NVIDIA\\DXCache")
                .Dir("%LOCALAPPDATA%\\NVIDIA\\GLCache")
                .Dir("%APPDATA%\\NVIDIA\\ComputeCache")
                .Dir("%LOCALAPPDATA%\\AMD\\DxCache")
                .Dir("%LOCALAPPDATA%\\AMD\\DxcCache"));

            t.Add(new CleanTarget("Nettleser-cache",
                "Bare cache. Passord, bokmerker og innlogginger røres ikke.")
                .Dir("%LOCALAPPDATA%\\Google\\Chrome\\User Data\\*\\Cache")
                .Dir("%LOCALAPPDATA%\\Google\\Chrome\\User Data\\*\\Code Cache")
                .Dir("%LOCALAPPDATA%\\Google\\Chrome\\User Data\\*\\GPUCache")
                .Dir("%LOCALAPPDATA%\\Microsoft\\Edge\\User Data\\*\\Cache")
                .Dir("%LOCALAPPDATA%\\Microsoft\\Edge\\User Data\\*\\Code Cache")
                .Dir("%LOCALAPPDATA%\\Microsoft\\Edge\\User Data\\*\\GPUCache")
                .Dir("%LOCALAPPDATA%\\BraveSoftware\\Brave-Browser\\User Data\\*\\Cache")
                .Dir("%LOCALAPPDATA%\\Vivaldi\\User Data\\*\\Cache")
                .Dir("%APPDATA%\\Opera Software\\Opera Stable\\Cache")
                .Dir("%LOCALAPPDATA%\\Mozilla\\Firefox\\Profiles\\*\\cache2")
                .Dir("%LOCALAPPDATA%\\Microsoft\\Windows\\INetCache\\IE"));

            t.Add(new CleanTarget("App-cache",
                "Discord, Spotify, Teams, Slack og Office.")
                .Dir("%APPDATA%\\discord\\Cache")
                .Dir("%APPDATA%\\discord\\Code Cache")
                .Dir("%APPDATA%\\discord\\GPUCache")
                .Dir("%LOCALAPPDATA%\\Spotify\\Data")
                .Dir("%APPDATA%\\Spotify\\Data")
                .Dir("%APPDATA%\\Microsoft\\Teams\\Cache")
                .Dir("%LOCALAPPDATA%\\Slack\\Cache")
                .Dir("%LOCALAPPDATA%\\Microsoft\\Office\\16.0\\OfficeFileCache"));

            CleanTarget wold = new CleanTarget("Windows.old",
                "Rester etter oppgradering, ofte 10–30 GB. Sletting fjerner muligheten til å rulle tilbake.");
            wold.Risk = Risk.Merk;
            wold.DefaultChecked = false;
            wold.DirAndSelf("%SystemDrive%\\Windows.old");
            wold.DirAndSelf("%SystemDrive%\\$Windows.~BT");
            wold.DirAndSelf("%SystemDrive%\\$Windows.~WS");
            t.Add(wold);

            t.Add(new CleanTarget("Oppsettsrester",
                "Panther-logger etter store Windows-oppdateringer.")
                .Dir("%SystemRoot%\\Panther")
                .Dir("%SystemRoot%\\SoftwareDistribution\\DataStore\\Logs"));

            return t;
        }

        // ---------------------------------------------------------------
        // Loeser opp stjerne-ledd i en sti til konkrete mapper.
        static IEnumerable<string> ResolvePaths(string raw)
        {
            string p = Util.Expand(raw);
            int star = p.IndexOf('*');
            if (star < 0) { yield return p; yield break; }

            int slashBefore = p.LastIndexOf('\\', star);
            int slashAfter = p.IndexOf('\\', star);
            if (slashBefore < 0) yield break;
            string baseDir = p.Substring(0, slashBefore);
            string wildSeg = slashAfter < 0
                ? p.Substring(slashBefore + 1)
                : p.Substring(slashBefore + 1, slashAfter - slashBefore - 1);
            string rest = slashAfter < 0 ? "" : p.Substring(slashAfter + 1);

            if (!Directory.Exists(baseDir)) yield break;
            string[] subs;
            try { subs = Directory.GetDirectories(baseDir, wildSeg); }
            catch { yield break; }
            foreach (string s in subs)
            {
                string full = rest.Length == 0 ? s : Path.Combine(s, rest);
                if (full.IndexOf('*') >= 0)
                {
                    foreach (string inner in ResolvePaths(full)) yield return inner;
                }
                else yield return full;
            }
        }

        public delegate void Progress(string what);

        // ---------------------------------------------------------------
        public static void Scan(CleanTarget t, CancellationToken ct, Progress prog)
        {
            long bytes = 0; int files = 0;

            if (t.Special)
            {
                if (t.SpecialKey == "recyclebin")
                {
                    long items;
                    bytes = Native.RecycleBinSize(out items);
                    files = (int)items;
                }
            }
            else
            {
                foreach (CleanRule r in t.Rules)
                {
                    foreach (string dir in ResolvePaths(r.Path))
                    {
                        ct.ThrowIfCancellationRequested();
                        if (!Directory.Exists(dir)) continue;
                        if (r.RemoveRoot && IsForbidden(dir)) continue;
                        if (prog != null) prog(dir);
                        MeasureDir(dir, r.Pattern, r.Recursive, ct, ref bytes, ref files);
                    }
                }
            }
            t.FoundBytes = bytes;
            t.FoundFiles = files;
            t.Scanned = true;
        }

        static void MeasureDir(string dir, string pattern, bool recursive, CancellationToken ct,
            ref long bytes, ref int files)
        {
            string[] fs;
            try { fs = Directory.GetFiles(dir, pattern); }
            catch { return; }
            foreach (string f in fs)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    FileInfo fi = new FileInfo(f);
                    if ((fi.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    bytes += fi.Length; files++;
                }
                catch { }
            }
            if (!recursive) return;
            string[] ds;
            try { ds = Directory.GetDirectories(dir); }
            catch { return; }
            foreach (string d in ds)
            {
                try
                {
                    DirectoryInfo di = new DirectoryInfo(d);
                    if ((di.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                }
                catch { continue; }
                MeasureDir(d, pattern, true, ct, ref bytes, ref files);
            }
        }

        // ---------------------------------------------------------------
        public class CleanResult
        {
            public long Freed;
            public int Deleted;
            public int Skipped;
        }

        public static CleanResult Clean(CleanTarget t, CancellationToken ct, Progress prog)
        {
            CleanResult res = new CleanResult();

            if (t.Special)
            {
                if (t.SpecialKey == "recyclebin")
                {
                    long items;
                    long before = Native.RecycleBinSize(out items);
                    if (Native.EmptyRecycleBin()) { res.Freed = before; res.Deleted = (int)items; }
                    else res.Skipped = (int)items;
                }
                return res;
            }

            foreach (CleanRule r in t.Rules)
            {
                foreach (string dir in ResolvePaths(r.Path))
                {
                    ct.ThrowIfCancellationRequested();
                    if (!Directory.Exists(dir)) continue;
                    if (r.RemoveRoot && IsForbidden(dir)) continue;
                    if (prog != null) prog(dir);
                    DeleteDir(dir, r.Pattern, r.Recursive, ct, res);
                    if (r.RemoveRoot) TryRemoveDir(dir, res);
                    else PruneEmpty(dir, ct);
                }
            }
            return res;
        }

        static void DeleteDir(string dir, string pattern, bool recursive, CancellationToken ct, CleanResult res)
        {
            string[] fs;
            try { fs = Directory.GetFiles(dir, pattern); }
            catch { return; }
            foreach (string f in fs)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    FileInfo fi = new FileInfo(f);
                    if ((fi.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    long len = fi.Length;
                    if ((fi.Attributes & (FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.Hidden)) != 0)
                        fi.Attributes = FileAttributes.Normal;
                    fi.Delete();
                    res.Freed += len; res.Deleted++;
                }
                catch { res.Skipped++; }   // fil i bruk — hoppes over, helt normalt
            }
            if (!recursive) return;
            string[] ds;
            try { ds = Directory.GetDirectories(dir); }
            catch { return; }
            foreach (string d in ds)
            {
                try
                {
                    DirectoryInfo di = new DirectoryInfo(d);
                    if ((di.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                }
                catch { continue; }
                DeleteDir(d, pattern, true, ct, res);
            }
        }

        static void PruneEmpty(string dir, CancellationToken ct)
        {
            string[] ds;
            try { ds = Directory.GetDirectories(dir); }
            catch { return; }
            foreach (string d in ds)
            {
                ct.ThrowIfCancellationRequested();
                PruneEmpty(d, ct);
                try
                {
                    if (Directory.GetFiles(d).Length == 0 && Directory.GetDirectories(d).Length == 0)
                        Directory.Delete(d, false);
                }
                catch { }
            }
        }

        static void TryRemoveDir(string dir, CleanResult res)
        {
            if (IsForbidden(dir)) return;
            try { Directory.Delete(dir, true); }
            catch { res.Skipped++; }
        }
    }
}
