using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Xml;

namespace Brisk
{
    public class BootEvent
    {
        public DateTime Time;
        public long TotalMs;
        public long MainPathMs;
        public long PostBootMs;
    }

    // Ett program, én tjeneste eller én driver som Windows mener forsinket oppstarten.
    public class BootDelay
    {
        public string Name;
        public string Kind;          // Program, Tjeneste, Driver
        public long WorstMs;
        public long TotalMs;         // sum over alle oppstarter
        public int Count;
        public DateTime Last;

        public long AverageMs { get { return Count > 0 ? TotalMs / Count : 0; } }
    }

    // Windows måler selv hvor lang tid oppstarten tok og hvem som sinket den.
    // Alt ligger i Diagnostics-Performance-loggen; vi leser den bare.
    public static class BootTools
    {
        const string LogName = "Microsoft-Windows-Diagnostics-Performance/Operational";

        public static bool Available(out string why)
        {
            why = "";
            try
            {
                EventLogQuery q = new EventLogQuery(LogName, PathType.LogName, "*[System[(EventID=100)]]");
                using (EventLogReader r = new EventLogReader(q))
                {
                    EventRecord rec = r.ReadEvent();
                    if (rec != null) { rec.Dispose(); return true; }
                }
                why = L.T("Windows har ikke logget noen oppstart ennå.");
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                why = L.T("Krever administrator.");
                return false;
            }
            catch (Exception ex)
            {
                why = ex.Message;
                return false;
            }
        }

        // De siste oppstartene, nyeste først.
        public static List<BootEvent> RecentBoots(int max)
        {
            List<BootEvent> list = new List<BootEvent>();
            try
            {
                EventLogQuery q = new EventLogQuery(LogName, PathType.LogName, "*[System[(EventID=100)]]");
                q.ReverseDirection = true;
                using (EventLogReader r = new EventLogReader(q))
                {
                    EventRecord rec;
                    while (list.Count < max && (rec = r.ReadEvent()) != null)
                    {
                        using (rec)
                        {
                            Dictionary<string, string> d = Fields(rec);
                            BootEvent b = new BootEvent();
                            b.Time = rec.TimeCreated.HasValue ? rec.TimeCreated.Value : DateTime.MinValue;
                            b.TotalMs = Num(d, "BootTime");
                            b.MainPathMs = Num(d, "MainPathBootTime");
                            b.PostBootMs = Num(d, "BootPostBootTime");
                            if (b.TotalMs > 0) list.Add(b);
                        }
                    }
                }
            }
            catch (Exception ex) { Util.Log("Kunne ikke lese oppstartstider: " + ex.Message); }
            return list;
        }

        // Hva som sinket oppstarten, slått sammen per navn.
        public static List<BootDelay> Delays(int maxEvents)
        {
            Dictionary<string, BootDelay> map =
                new Dictionary<string, BootDelay>(StringComparer.OrdinalIgnoreCase);
            try
            {
                EventLogQuery q = new EventLogQuery(LogName, PathType.LogName,
                    "*[System[(EventID=101 or EventID=102 or EventID=103)]]");
                q.ReverseDirection = true;
                using (EventLogReader r = new EventLogReader(q))
                {
                    int seen = 0;
                    EventRecord rec;
                    while (seen < maxEvents && (rec = r.ReadEvent()) != null)
                    {
                        using (rec)
                        {
                            seen++;
                            Dictionary<string, string> d = Fields(rec);
                            string name = Get(d, "Name");
                            if (string.IsNullOrEmpty(name)) continue;

                            long ms = Num(d, "DegradationTime");
                            if (ms <= 0) ms = Num(d, "TotalTime");
                            if (ms <= 0) continue;

                            BootDelay bd;
                            if (!map.TryGetValue(name, out bd))
                            {
                                bd = new BootDelay();
                                bd.Name = name;
                                bd.Kind = rec.Id == 103 ? L.T("Tjeneste")
                                        : rec.Id == 102 ? L.T("Driver")
                                        : L.T("Program");
                                map[name] = bd;
                            }
                            bd.Count++;
                            bd.TotalMs += ms;
                            if (ms > bd.WorstMs) bd.WorstMs = ms;
                            if (rec.TimeCreated.HasValue && rec.TimeCreated.Value > bd.Last)
                                bd.Last = rec.TimeCreated.Value;
                        }
                    }
                }
            }
            catch (Exception ex) { Util.Log("Kunne ikke lese oppstartsforsinkelser: " + ex.Message); }

            List<BootDelay> l = new List<BootDelay>(map.Values);
            l.Sort(delegate(BootDelay a, BootDelay b) { return b.AverageMs.CompareTo(a.AverageMs); });
            return l;
        }

        // Slår opp gjennomsnittlig forsinkelse for en oppstartsoppføring.
        // Matcher på exe-navnet, som er det Windows logger.
        public static BootDelay MatchFor(List<BootDelay> delays, StartupItem it)
        {
            if (delays == null || it == null) return null;
            string exe = StartupTools.ExtractExe(it.Command);
            string file = null;
            try { if (exe != null) file = System.IO.Path.GetFileName(exe); }
            catch { }

            foreach (BootDelay d in delays)
            {
                if (file != null && string.Equals(d.Name, file, StringComparison.OrdinalIgnoreCase))
                    return d;
                // Fallback: navnet uten .exe mot oppforingsnavnet
                string bare = d.Name;
                int dot = bare.LastIndexOf('.');
                if (dot > 0) bare = bare.Substring(0, dot);
                if (!string.IsNullOrEmpty(it.Name) &&
                    string.Equals(bare, it.Name, StringComparison.OrdinalIgnoreCase))
                    return d;
            }
            return null;
        }

        // ---------------------------------------------------------------
        static Dictionary<string, string> Fields(EventRecord rec)
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(rec.ToXml());
                XmlNodeList nodes = doc.GetElementsByTagName("Data");
                foreach (XmlNode n in nodes)
                {
                    XmlAttribute a = n.Attributes == null ? null : n.Attributes["Name"];
                    if (a != null) d[a.Value] = n.InnerText;
                }
            }
            catch { }
            return d;
        }

        static string Get(Dictionary<string, string> d, string key)
        {
            string v;
            return d.TryGetValue(key, out v) ? v : null;
        }

        static long Num(Dictionary<string, string> d, string key)
        {
            long v;
            string s = Get(d, key);
            return (s != null && long.TryParse(s, out v)) ? v : 0;
        }

        public static string Seconds(long ms)
        {
            if (ms <= 0) return "—";
            if (ms < 1000) return ms + " ms";
            return (ms / 1000.0).ToString(ms < 10000 ? "0.0" : "0") + " s";
        }
    }
}
