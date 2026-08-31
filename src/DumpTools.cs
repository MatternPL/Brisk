using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace Brisk
{
    public class DumpModule
    {
        public string Name = "";
        public ulong Base;
        public uint Size;
        public string Company = "";
        public string Version = "";
        public string Path = "";
        public bool IsMicrosoft;
        public bool Known;          // fant vi fila på disk?

        public string Origin
        {
            get
            {
                if (Company.Length > 0) return Company;
                return IsMicrosoft ? "Microsoft" : L.T("ukjent opphav");
            }
        }
    }

    public class DumpAnalysis
    {
        public string File = "";
        public DateTime Time;
        public uint Code;
        public ulong[] Args = new ulong[4];
        public string CodeName = "";
        public string Meaning = "";

        public DumpModule Culprit;          // modulen feiladressen ligger i
        public DumpModule LikelyCause;      // første ikke-Microsoft-modul i stakken
        public ulong CulpritOffset;
        public List<DumpModule> Stack = new List<DumpModule>();      // moduler i kallstakken
        public List<DumpModule> ThirdParty = new List<DumpModule>(); // lastede drivere som ikke er fra Microsoft
        public string Advice = "";
        public string Error = "";

        public string CodeText
        {
            get { return "0x" + Code.ToString("X8") + (CodeName.Length > 0 ? "  " + CodeName : ""); }
        }
    }

    // Leser Windows' egne kjerne-minidumper (PAGEDU64).
    //
    // Formatet er ikke dokumentert av Microsoft, men det er stabilt og lett å
    // verifisere: stoppkoden og de fire parameterne i filhodet skal stemme med
    // hendelsesloggen, og modul 0 skal være ntoskrnl.exe. Begge deler sjekkes.
    public static class DumpTools
    {
        // --- filhodet, DUMP_HEADER64 ---
        const int OffMachineType = 48;
        const int OffBugCheck = 56;
        const int OffParam1 = 64;
        const int TriageStart = 0x2000;

        // --- TRIAGE_DUMP64, felt som ULONG fra TriageStart. Alle offsets i
        //     denne strukturen er absolutte fra filstart. ---
        const int FldCallStack = 10;
        const int FldCallStackSize = 11;
        const int FldDriverList = 12;
        const int FldDriverCount = 13;
        const int FldStringPool = 14;

        // --- én driveroppføring (KLDR_DATA_TABLE_ENTRY i triage-form) ---
        const int EntNameOffset = 0x00;     // peker inn i strengbassenget
        const int EntBase = 0x38;
        const int EntSize = 0x48;

        public static string MinidumpFolder()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\CrashControl"))
                {
                    if (k != null)
                    {
                        string d = Convert.ToString(k.GetValue("MinidumpDir"));
                        if (!string.IsNullOrEmpty(d)) return Util.Expand(d);
                    }
                }
            }
            catch { }
            return Util.Expand("%SystemRoot%\\Minidump");
        }

        public static string FullDumpFile()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\CrashControl"))
                {
                    if (k != null)
                    {
                        string d = Convert.ToString(k.GetValue("DumpFile"));
                        if (!string.IsNullOrEmpty(d)) return Util.Expand(d);
                    }
                }
            }
            catch { }
            return Util.Expand("%SystemRoot%\\MEMORY.DMP");
        }

        // Alle dumpfiler, nyeste først.
        public static List<string> Find()
        {
            List<string> files = new List<string>();
            try
            {
                string dir = MinidumpFolder();
                if (Directory.Exists(dir))
                {
                    string[] f = Directory.GetFiles(dir, "*.dmp");
                    Array.Sort(f, delegate(string a, string b)
                    {
                        return File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a));
                    });
                    files.AddRange(f);
                }
            }
            catch { }
            try
            {
                string full = FullDumpFile();
                if (File.Exists(full)) files.Add(full);
            }
            catch { }
            return files;
        }

        // Hvor mange kraesj vi faktisk kan si noe om. Forsida teller dette og
        // ikke hendelsene i systemloggen: er dumpfila ryddet bort, har Brisk
        // ingenting aa vise brukeren, og da skal den heller ikke maase om det.
        // Aa aapne fila er unodig her - tidspunktet holder.
        public static int RecentCount(int dager)
        {
            int n = 0;
            DateTime grense = DateTime.Now.AddDays(-dager);
            try
            {
                foreach (string f in Find())
                    if (File.GetLastWriteTime(f) >= grense) n++;
            }
            catch (Exception) { }
            return n;
        }

        // Nyeste dumpfil, eller DateTime.MinValue om det ikke finnes noen.
        public static DateTime Newest()
        {
            DateTime t = DateTime.MinValue;
            try
            {
                foreach (string f in Find())
                {
                    DateTime w = File.GetLastWriteTime(f);
                    if (w > t) t = w;
                }
            }
            catch (Exception) { }
            return t;
        }

        // ---------------------------------------------------------------
        public static DumpAnalysis Analyse(string path)
        {
            DumpAnalysis a = new DumpAnalysis();
            a.File = path;
            try { a.Time = File.GetLastWriteTime(path); }
            catch { }

            byte[] b;
            try { b = File.ReadAllBytes(path); }
            catch (Exception ex) { a.Error = ex.Message; return a; }

            if (b.Length < TriageStart + 0x80)
            {
                a.Error = L.T("Dumpfilen er for liten til å tolkes.");
                return a;
            }

            string sig = Encoding.ASCII.GetString(b, 0, 8);
            if (sig != "PAGEDU64")
            {
                a.Error = L.F("Ukjent dumpformat ({0}).", sig.Trim());
                return a;
            }

            a.Code = BitConverter.ToUInt32(b, OffBugCheck);
            for (int i = 0; i < 4; i++)
                a.Args[i] = BitConverter.ToUInt64(b, OffParam1 + i * 8);
            a.CodeName = Name(a.Code);
            a.Meaning = Meaning(a.Code);

            try { ReadModules(b, a); }
            catch (Exception ex) { a.Error = ex.Message; }

            a.Advice = BuildAdvice(a);
            return a;
        }

        static void ReadModules(byte[] b, DumpAnalysis a)
        {
            int driverList = (int)BitConverter.ToUInt32(b, TriageStart + FldDriverList * 4);
            int count = (int)BitConverter.ToUInt32(b, TriageStart + FldDriverCount * 4);
            int pool = (int)BitConverter.ToUInt32(b, TriageStart + FldStringPool * 4);
            int stack = (int)BitConverter.ToUInt32(b, TriageStart + FldCallStack * 4);
            int stackSize = (int)BitConverter.ToUInt32(b, TriageStart + FldCallStackSize * 4);

            if (count <= 0 || count > 4000) return;
            if (driverList <= 0 || pool <= driverList || pool >= b.Length) return;

            int stride = (pool - driverList) / count;
            if (stride < 0x60 || stride > 0x200) return;

            List<DumpModule> mods = new List<DumpModule>();
            for (int i = 0; i < count; i++)
            {
                int o = driverList + i * stride;
                if (o + stride > b.Length) break;

                DumpModule m = new DumpModule();
                m.Base = BitConverter.ToUInt64(b, o + EntBase);
                m.Size = BitConverter.ToUInt32(b, o + EntSize);
                int nameOff = (int)BitConverter.ToUInt32(b, o + EntNameOffset);
                m.Name = PoolString(b, nameOff);
                if (m.Name.Length == 0 || m.Size == 0) continue;
                Describe(m);
                mods.Add(m);
            }

            // Kontroll: første modul skal være kjernen. Er den ikke det, har vi
            // tolket strukturen feil, og da er det bedre å si ingenting.
            if (mods.Count == 0 ||
                mods[0].Name.IndexOf("ntoskrnl", StringComparison.OrdinalIgnoreCase) < 0)
            {
                a.Error = L.T("Klarte ikke lese modullista i dumpen.");
                return;
            }

            // Kjente leverandører først — de er mest til hjelp.
            foreach (DumpModule m in mods)
                if (m.Known && !m.IsMicrosoft) a.ThirdParty.Add(m);
            foreach (DumpModule m in mods)
                if (!m.Known && !m.IsMicrosoft) a.ThirdParty.Add(m);

            // Feiladressen: prøv parameterne, den som treffer en modul vinner.
            // For de fleste stoppkodene er det parameter 2 eller 4.
            int[] order = { 1, 3, 0, 2 };
            foreach (int idx in order)
            {
                DumpModule m = Owner(mods, a.Args[idx]);
                if (m != null)
                {
                    a.Culprit = m;
                    a.CulpritOffset = a.Args[idx] - m.Base;
                    break;
                }
            }

            // Kallstakken: hvilke drivere var involvert, i rekkefølge.
            if (stack > 0 && stackSize > 0 && stack + stackSize <= b.Length)
            {
                Dictionary<string, bool> seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                for (int o = stack; o + 8 <= stack + stackSize; o += 8)
                {
                    DumpModule m = Owner(mods, BitConverter.ToUInt64(b, o));
                    if (m == null || seen.ContainsKey(m.Name)) continue;
                    seen[m.Name] = true;
                    a.Stack.Add(m);
                    if (a.Stack.Count >= 12) break;
                }
            }

            // Ligger det en driver som ikke er fra Microsoft i stakken, er det
            // nesten alltid den som er årsaken — selv om selve feilen slo ut
            // inne i en Windows-komponent.
            foreach (DumpModule m in a.Stack)
                if (!m.IsMicrosoft) { a.LikelyCause = m; break; }
        }

        static DumpModule Owner(List<DumpModule> mods, ulong addr)
        {
            if (addr < 0xFFFF000000000000UL) return null;
            foreach (DumpModule m in mods)
                if (addr >= m.Base && addr < m.Base + m.Size) return m;
            return null;
        }

        // Bassenget lagrer: ULONG antall tegn, så teksten i UTF-16.
        static string PoolString(byte[] b, int off)
        {
            try
            {
                if (off <= 0 || off + 4 >= b.Length) return "";
                int chars = (int)BitConverter.ToUInt32(b, off);
                if (chars <= 0 || chars > 200) return "";
                int bytes = chars * 2;
                if (off + 4 + bytes > b.Length) return "";
                return Encoding.Unicode.GetString(b, off + 4, bytes).Trim('\0');
            }
            catch { return ""; }
        }

        // Indeks over driverfiler. Mange drivere ligger ikke i drivers-mappa,
        // men i DriverStore, så vi bygger et navneoppslag én gang.
        static Dictionary<string, string> index;

        static Dictionary<string, string> Index()
        {
            if (index != null) return index;
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] roots =
            {
                Util.Expand("%SystemRoot%\\System32\\drivers"),
                Util.Expand("%SystemRoot%\\System32"),
                Util.Expand("%SystemRoot%\\System32\\DriverStore\\FileRepository"),
            };
            for (int i = 0; i < roots.Length; i++)
            {
                try
                {
                    if (!Directory.Exists(roots[i])) continue;
                    SearchOption so = i == 2 ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    foreach (string pat in new string[] { "*.sys", "*.dll", "*.exe" })
                    {
                        if (i == 2 && pat != "*.sys") continue;   // DriverStore er stor nok som den er
                        foreach (string f in Directory.GetFiles(roots[i], pat, so))
                        {
                            string n = Path.GetFileName(f);
                            if (!d.ContainsKey(n)) d[n] = f;
                        }
                    }
                }
                catch { }
            }
            index = d;
            return index;
        }

        // Slår opp driverfila på disk for å finne hvem som har laget den.
        static void Describe(DumpModule m)
        {
            try
            {
                string p;
                if (Index().TryGetValue(m.Name, out p) && File.Exists(p))
                {
                    FileVersionInfo fv = FileVersionInfo.GetVersionInfo(p);
                    m.Path = p;
                    m.Company = (fv.CompanyName ?? "").Trim();
                    m.Version = (fv.FileVersion ?? "").Trim();
                    m.Known = true;
                }
            }
            catch { }

            m.IsMicrosoft = m.Company.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0;

            // Fant vi ikke fila — for eksempel fordi driveren er avinstallert
            // etter kræsjen — går vi på navnet. Lista dekker kjernemodulene
            // Windows selv laster; alt annet regnes som fremmed.
            if (!m.Known && !m.IsMicrosoft)
                m.IsMicrosoft = LooksLikeWindows(m.Name);
        }

        static readonly string[] WindowsModules =
        {
            "ntoskrnl", "ntfs", "nt", "hal", "kdcom", "kdnet", "kdstub", "clfs", "cng",
            "ksecdd", "ksecpkg", "msrpc", "ndis", "netio", "tcpip", "fltmgr", "fileinfo",
            "volsnap", "volmgr", "partmgr", "disk", "classpnp", "storport", "storahci",
            "storufs", "stornvme", "acpi", "pci", "pcw", "msisadrv", "intelpep", "wdf01000",
            "wmilib", "wpprecorder", "cimfs", "fs_rec", "null", "beep", "ksthunk", "umbus",
            "usbxhci", "usbhub3", "ucx01000", "hidclass", "hidparse", "kbdclass", "mouclass",
            "i8042prt", "monitor", "dxgkrnl", "dxgmms", "watchdog", "basicdisplay",
            "basicrender", "cdrom", "cdd", "win32k", "afd", "mup", "srv", "rdbss", "bowser",
            "mrxsmb", "tdx", "http", "vwififlt", "wanarp", "ndiswan", "ndistapi", "ndproxy",
            "pacer", "netbt", "nsiproxy", "ipnat", "wfplwfs", "mssmbios", "luafv", "dam",
            "pdc", "prm", "spaceport", "iorate", "vhf", "hdaudio", "portcls", "drmk",
            "condrv", "mssecflt", "wcifs", "cldflt", "bindflt", "wof", "exfat", "fastfat",
            "refs", "ubpm", "tm", "pshed", "bootvid", "cpu", "werkernel", "ci", "msfs",
            "npfs", "tbs", "usbd", "usbport", "usbehci", "sdbus", "rdyboost", "mountmgr",
            "fvevol", "iorate", "filecrypt", "tcpipreg", "netbios", "smb", "mslldp",
            "ahcache", "peauth", "vdrvroot", "swenum", "umpass", "compositebus", "kdhwsupport",
        };

        static bool LooksLikeWindows(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            int dot = n.LastIndexOf('.');
            if (dot > 0) n = n.Substring(0, dot);
            foreach (string w in WindowsModules)
                if (n == w || n.StartsWith(w)) return true;
            return false;
        }

        // ---------------------------------------------------------------
        static string Name(uint code)
        {
            switch (code)
            {
                case 0x0A: return "IRQL_NOT_LESS_OR_EQUAL";
                case 0x18: return "REFERENCE_BY_POINTER";
                case 0x1A: return "MEMORY_MANAGEMENT";
                case 0x1E: return "KMODE_EXCEPTION_NOT_HANDLED";
                case 0x3B: return "SYSTEM_SERVICE_EXCEPTION";
                case 0x4E: return "PFN_LIST_CORRUPT";
                case 0x50: return "PAGE_FAULT_IN_NONPAGED_AREA";
                case 0x7E: return "SYSTEM_THREAD_EXCEPTION_NOT_HANDLED";
                case 0x9C: return "MACHINE_CHECK_EXCEPTION";
                case 0x9F: return "DRIVER_POWER_STATE_FAILURE";
                case 0xA0: return "INTERNAL_POWER_ERROR";
                case 0xC2: return "BAD_POOL_CALLER";
                case 0xC4: return "DRIVER_VERIFIER_DETECTED_VIOLATION";
                case 0xC5: return "DRIVER_CORRUPTED_EXPOOL";
                case 0xD1: return "DRIVER_IRQL_NOT_LESS_OR_EQUAL";
                case 0xEF: return "CRITICAL_PROCESS_DIED";
                case 0xF7: return "DRIVER_OVERRAN_STACK_BUFFER";
                case 0x101: return "CLOCK_WATCHDOG_TIMEOUT";
                case 0x109: return "CRITICAL_STRUCTURE_CORRUPTION";
                case 0x116: return "VIDEO_TDR_ERROR";
                case 0x119: return "VIDEO_SCHEDULER_INTERNAL_ERROR";
                case 0x124: return "WHEA_UNCORRECTABLE_ERROR";
                case 0x133: return "DPC_WATCHDOG_VIOLATION";
                case 0x139: return "KERNEL_SECURITY_CHECK_FAILURE";
                case 0x1CA: return "SYNTHETIC_WATCHDOG_TIMEOUT";
                default: return "";
            }
        }

        static string Meaning(uint code)
        {
            switch (code)
            {
                case 0x0A:
                case 0xD1: return L.T("En driver leste minne den ikke hadde lov til å røre.");
                case 0x1A: return L.T("Windows fant en feil i minnehåndteringen.");
                case 0x1E:
                case 0x7E: return L.T("En driver kastet en feil ingen tok imot.");
                case 0x3B: return L.T("En systemtjeneste feilet mens den kjørte på vegne av en driver.");
                case 0x4E:
                case 0xC5:
                case 0x109: return L.T("Noe skrev i minne som tilhørte kjernen.");
                case 0x50: return L.T("Noe leste fra en minneadresse som ikke fantes.");
                case 0x9C:
                case 0x124: return L.T("Maskinvaren meldte en feil den ikke kunne rette.");
                case 0x9F:
                case 0xA0: return L.T("En driver hang da maskinen skulle sove eller våkne.");
                case 0xC2: return L.T("En driver ba om minne på feil måte.");
                case 0xC4: return L.T("Driververifisering fanget en driver som gjorde noe ulovlig.");
                case 0xEF: return L.T("En prosess Windows må ha, døde.");
                case 0xF7: return L.T("En driver skrev utenfor sitt eget område.");
                case 0x101: return L.T("En prosessorkjerne sluttet å svare.");
                case 0x116:
                case 0x119: return L.T("Grafikkortet sluttet å svare og lot seg ikke nullstille.");
                case 0x133: return L.T("En driver holdt prosessoren for lenge uten å gi slipp.");
                case 0x139: return L.T("Windows oppdaget at data i kjernen var ødelagt.");
                case 0x1CA: return L.T("En vakthund utløste fordi noe hang.");
                default: return "";
            }
        }

        // Konkret råd, basert på stoppkoden og hvem som faktisk feilet.
        static string BuildAdvice(DumpAnalysis a)
        {
            List<string> r = new List<string>();

            if (a.Code == 0x124 || a.Code == 0x9C)
                r.Add(L.T("Dette peker på maskinvare, ikke programvare. Sjekk temperaturer, slå av overklokking, og test minnet."));
            else if (a.Code == 0x1A || a.Code == 0x50 || a.Code == 0x4E)
                r.Add(L.T("Kjør Windows Minnediagnostikk. Feil i RAM gir ofte akkurat denne."));
            else if (a.Code == 0x116 || a.Code == 0x119)
                r.Add(L.T("Installer grafikkdriveren på nytt. Er den nettopp oppdatert, prøv forrige versjon."));
            else if (a.Code == 0xEF)
                r.Add(L.T("Kjør sfc og DISM under Vedlikehold — systemfiler er sannsynligvis ødelagt."));

            if (a.LikelyCause != null)
            {
                if (a.LikelyCause.Known)
                    r.Add(L.F("Oppdater eller avinstaller programvaren {0} hører til. Er den nettopp oppdatert, gå tilbake til forrige versjon.", a.LikelyCause.Name));
                else
                    r.Add(L.T("Den fila finnes ikke lenger på maskinen, så driveren er trolig avinstallert eller byttet ut siden kræsjen."));
            }
            else if (a.Culprit != null)
            {
                if (!a.Culprit.IsMicrosoft)
                    r.Add(L.F("Feilen skjedde i {0}. Oppdater eller fjern programvaren den hører til.", a.Culprit.Name));
                else
                {
                    r.Add(L.F("Feilen slo ut i {0}, som er en del av Windows. Ingen fremmed driver lå i kallstakken.", a.Culprit.Name));
                    r.Add(L.T("Kjør sfc og DISM under Vedlikehold, og se om noen av driverne under er nylig oppdatert."));
                }
            }
            else if (a.Error.Length == 0)
                r.Add(L.T("Fant ingen navngitt modul på feiladressen. Se hvilke drivere som var involvert nedenfor."));

            if (r.Count == 0)
                r.Add(L.T("Oppdater drivere og kjør sfc under Vedlikehold."));

            return string.Join("\n", r.ToArray());
        }

        // Kort tekstoppsummering som kan limes inn i en e-post eller et forum.
        public static string Summary(DumpAnalysis a)
        {
            StringBuilder b = new StringBuilder();
            b.AppendLine("Brisk — " + L.T("Blåskjermanalyse"));
            b.AppendLine(new string('-', 52));
            b.AppendLine(L.T("Fil") + ": " + a.File);
            b.AppendLine(L.T("Når") + ": " + a.Time.ToString("yyyy-MM-dd HH:mm"));
            b.AppendLine(L.T("Stoppkode") + ": " + a.CodeText);
            if (a.Meaning.Length > 0) b.AppendLine(a.Meaning);
            b.AppendLine(L.T("Parametere") + ": " +
                "0x" + a.Args[0].ToString("X") + ", 0x" + a.Args[1].ToString("X") + ", " +
                "0x" + a.Args[2].ToString("X") + ", 0x" + a.Args[3].ToString("X"));
            b.AppendLine();
            if (a.Culprit != null)
                b.AppendLine(L.T("Modul") + ": " + a.Culprit.Name +
                    "  (" + a.Culprit.Origin + ")  +0x" + a.CulpritOffset.ToString("X"));
            if (a.LikelyCause != null)
                b.AppendLine(L.T("Sannsynlig årsak") + ": " + a.LikelyCause.Name +
                    "  (" + a.LikelyCause.Origin + ")");
            if (a.Stack.Count > 0)
            {
                b.AppendLine();
                b.AppendLine(L.T("Involvert i kallstakken") + ":");
                foreach (DumpModule m in a.Stack)
                    b.AppendLine("  " + m.Name.PadRight(28) + m.Origin);
            }
            if (a.ThirdParty.Count > 0)
            {
                b.AppendLine();
                b.AppendLine(L.T("Drivere som ikke er fra Microsoft") + ":");
                int n = 0;
                foreach (DumpModule m in a.ThirdParty)
                {
                    if (n++ >= 25) break;
                    b.AppendLine("  " + m.Name.PadRight(28) + m.Company +
                        (m.Version.Length > 0 ? "  " + m.Version : ""));
                }
            }
            b.AppendLine();
            b.AppendLine(a.Advice);
            return b.ToString();
        }
    }
}
