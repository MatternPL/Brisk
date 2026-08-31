using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Management;
using System.Xml;

namespace Brisk
{
    public class CrashEvent
    {
        public DateTime Time;
        public string Code = "";        // f.eks. 0x0000133
        public string Meaning = "";
    }

    public class DriveWear
    {
        public string Name = "";
        public int Wear = -1;           // prosent av forventet levetid brukt
        public int Temperature = -1;    // grader
        public long PowerOnHours = -1;
        public long ReadErrors = -1;
        public long WriteErrors = -1;
        public int SpareLeft = -1;      // ledig reservekapasitet, prosent
        public long UnsafeShutdowns = -1;
        public long MediaErrors = -1;
        public bool FromDrive;          // lest fra disken selv, ikke fra WMI
    }

    public class BatteryHealth
    {
        public string Name = "";
        public long DesignCapacity;
        public long FullCapacity;
        public int HealthPercent = -1;
    }

    public static class HealthTools
    {
        // ---------------------------------------------------------------
        // Hendelsene i systemloggen kan ikke slettes uten aa toemme hele
        // System-loggen, og det skal ikke et vedlikeholdsprogram gjore - det
        // ville tatt med all annen historikk ogsaa. Dumpfilene kan ryddes,
        // men hendelsen staar igjen. Derfor kan brukeren i stedet kvittere
        // ut kraesjene han har sett paa, saa forsida slutter aa mase om dem.
        const string SeenKey = "BlaaskjermSett";

        public static DateTime CrashesSeenUntil
        {
            get
            {
                string s = Util.Setting(SeenKey);
                long t;
                if (s != null && long.TryParse(s, out t) && t > 0 && t <= DateTime.MaxValue.Ticks)
                    return new DateTime(t);
                return DateTime.MinValue;
            }
            set { Util.SetSetting(SeenKey, value.Ticks.ToString()); }
        }

        // Antall kraesj siste 30 dager som brukeren ikke har kvittert ut.
        public static int UnseenCrashes(List<CrashEvent> alle)
        {
            if (alle == null) return 0;
            DateTime sett = CrashesSeenUntil;
            DateTime grense = DateTime.Now.AddDays(-30);
            int n = 0;
            foreach (CrashEvent c in alle)
                if (c.Time >= grense && c.Time > sett) n++;
            return n;
        }

        // ---------------------------------------------------------------
        // Blåskjermer. Windows skriver én hendelse per kræsj.
        public static List<CrashEvent> Crashes(int max)
        {
            List<CrashEvent> list = new List<CrashEvent>();
            try
            {
                EventLogQuery q = new EventLogQuery("System", PathType.LogName,
                    "*[System[Provider[@Name='Microsoft-Windows-WER-SystemErrorReporting'] and (EventID=1001)]]");
                q.ReverseDirection = true;
                using (EventLogReader r = new EventLogReader(q))
                {
                    EventRecord rec;
                    while (list.Count < max && (rec = r.ReadEvent()) != null)
                    {
                        using (rec)
                        {
                            CrashEvent c = new CrashEvent();
                            c.Time = rec.TimeCreated.HasValue ? rec.TimeCreated.Value : DateTime.MinValue;
                            c.Code = FirstData(rec);
                            c.Meaning = Explain(c.Code);
                            list.Add(c);
                        }
                    }
                }
            }
            catch (Exception ex) { Util.Log("Kunne ikke lese blåskjermlogg: " + ex.Message); }
            return list;
        }

        static string FirstData(EventRecord rec)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(rec.ToXml());
                XmlNodeList nodes = doc.GetElementsByTagName("Data");
                if (nodes.Count > 0)
                {
                    // Formatet er "0x00000133 (0x...,...)" - vi vil ha stoppkoden
                    string s = nodes[0].InnerText.Trim();
                    int sp = s.IndexOf(' ');
                    return sp > 0 ? s.Substring(0, sp) : s;
                }
            }
            catch { }
            return "";
        }

        // De vanligste stoppkodene. Resten står som kode.
        static string Explain(string code)
        {
            string c = (code ?? "").ToLowerInvariant().TrimStart('0', 'x');
            switch (c)
            {
                case "a": return L.T("Driver rørte minne den ikke skulle");
                case "1a": return L.T("Feil i minnehåndteringen — test RAM-en");
                case "3b": return L.T("Feil i en systemtjeneste");
                case "50": return L.T("Lesing fra ugyldig minne — ofte RAM eller driver");
                case "7e": return L.T("Driver kræsjet");
                case "9f": return L.T("Driver hang under strømsparing");
                case "d1": return L.T("Driver rørte minne den ikke skulle");
                case "ef": return L.T("En kritisk systemprosess døde");
                case "116": return L.T("Grafikkortet svarte ikke — ofte driver eller varme");
                case "119": return L.T("Feil i grafikkdriveren");
                case "124": return L.T("Maskinvarefeil — CPU, minne eller hovedkort");
                case "133": return L.T("En driver holdt prosessoren for lenge");
                case "139": return L.T("Windows oppdaget minneødeleggelse");
                case "1e": return L.T("Uhåndtert feil i kjernen");
                case "c2": return L.T("Driver frigjorde minne feil");
                default: return "";
            }
        }

        // ---------------------------------------------------------------
        // Slitasje og temperatur. Hva som faktisk rapporteres varierer mye
        // mellom disker — NVMe gir ofte bare temperatur og slitasje.
        public static List<DriveWear> Drives()
        {
            List<DriveWear> list = new List<DriveWear>();
            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                scope.Connect();

                // MSFT_StorageReliabilityCounter kan ikke spørres direkte — den
                // finnes bare via assosiasjonen fra hver fysiske disk.
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(scope,
                    new ObjectQuery("SELECT * FROM MSFT_PhysicalDisk")))
                {
                    foreach (ManagementObject disk in s.Get())
                    {
                        DriveWear d = new DriveWear();
                        try { d.Name = Convert.ToString(disk["FriendlyName"]); }
                        catch { }

                        try
                        {
                            foreach (ManagementObject rc in disk.GetRelated("MSFT_StorageReliabilityCounter"))
                            {
                                d.Wear = (int)Opt(rc, "Wear", -1);
                                d.Temperature = (int)Opt(rc, "Temperature", -1);
                                d.PowerOnHours = Opt(rc, "PowerOnHours", -1);
                                d.ReadErrors = Opt(rc, "ReadErrorsTotal", -1);
                                d.WriteErrors = Opt(rc, "WriteErrorsTotal", -1);
                                break;
                            }
                        }
                        catch { }

                        // Windows melder Wear = 0 og PowerOnHours = null for mange
                        // NVMe-disker selv om disken selv vet bedre. Spor derfor
                        // disken direkte, og la det svaret gaa foran.
                        try
                        {
                            int nr;
                            if (int.TryParse(Convert.ToString(disk["DeviceId"]), out nr))
                            {
                                NvmeTools.Health n = NvmeTools.Read(nr);
                                if (n != null)
                                {
                                    d.Wear = n.PercentUsed;
                                    d.SpareLeft = n.SpareLeft;
                                    d.UnsafeShutdowns = n.UnsafeShutdowns;
                                    d.MediaErrors = n.MediaErrors;
                                    if (n.Temperature > 0) d.Temperature = n.Temperature;
                                    if (n.PowerOnHours > 0) d.PowerOnHours = n.PowerOnHours;
                                    d.FromDrive = true;
                                }
                            }
                        }
                        catch (Exception ex) { Util.Log("NVMe-logg feilet: " + ex.Message); }

                        list.Add(d);
                    }
                }
            }
            catch (Exception ex) { Util.Log("Kunne ikke lese diskslitasje: " + ex.Message); }
            return list;
        }

        static long Opt(ManagementObject mo, string prop, long fallback)
        {
            try
            {
                object v = mo[prop];
                if (v == null) return fallback;
                return Convert.ToInt64(v);
            }
            catch { return fallback; }
        }

        // ---------------------------------------------------------------
        // Batterikapasitet. root\WMI har både designkapasitet og hva batteriet
        // faktisk klarer nå — forholdet mellom dem er helsa.
        public static BatteryHealth Battery()
        {
            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\root\WMI");
                scope.Connect();

                long design = 0, full = 0;
                string name = "";

                using (ManagementObjectSearcher s = new ManagementObjectSearcher(scope,
                    new ObjectQuery("SELECT * FROM BatteryStaticData")))
                    foreach (ManagementObject mo in s.Get())
                    {
                        design = Opt(mo, "DesignedCapacity", 0);
                        try { name = Convert.ToString(mo["DeviceName"]); }
                        catch { }
                        break;
                    }

                using (ManagementObjectSearcher s = new ManagementObjectSearcher(scope,
                    new ObjectQuery("SELECT * FROM BatteryFullChargedCapacity")))
                    foreach (ManagementObject mo in s.Get())
                    {
                        full = Opt(mo, "FullChargedCapacity", 0);
                        break;
                    }

                if (design <= 0 || full <= 0) return null;

                BatteryHealth b = new BatteryHealth();
                b.Name = string.IsNullOrEmpty(name) ? L.T("Batteri") : name;
                b.DesignCapacity = design;
                b.FullCapacity = full;
                b.HealthPercent = (int)Math.Round(100.0 * full / design);
                return b;
            }
            catch { return null; }
        }
    }
}
