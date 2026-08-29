using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Xml;

namespace Brisk
{
    public class AppCrash
    {
        public string App = "";
        public string Version = "";
        public string Module = "";       // modulen som feilet
        public string ModuleVersion = "";
        public string Code = "";         // unntakskode
        public string Meaning = "";
        public int Count = 1;
        public DateTime Last;
        public bool Hang;                // sluttet å svare, i stedet for å kræsje
    }

    // Windows logger hvert programkræsj med hvilken modul som feilet.
    // Det står i Hendelsesliste, men vises aldri for brukeren.
    public static class AppCrashTools
    {
        public static List<AppCrash> Recent(int days, int maxEvents)
        {
            Dictionary<string, AppCrash> map = new Dictionary<string, AppCrash>(StringComparer.OrdinalIgnoreCase);
            DateTime since = DateTime.Now.AddDays(-days);

            Read("Application Error", 1000, since, maxEvents, map, false);
            Read("Application Hang", 1002, since, maxEvents, map, true);

            List<AppCrash> l = new List<AppCrash>(map.Values);
            l.Sort(delegate(AppCrash a, AppCrash b)
            {
                int c = b.Count.CompareTo(a.Count);
                return c != 0 ? c : b.Last.CompareTo(a.Last);
            });
            return l;
        }

        static void Read(string provider, int id, DateTime since, int max,
            Dictionary<string, AppCrash> map, bool hang)
        {
            try
            {
                EventLogQuery q = new EventLogQuery("Application", PathType.LogName,
                    "*[System[Provider[@Name='" + provider + "'] and (EventID=" + id + ")]]");
                q.ReverseDirection = true;
                using (EventLogReader r = new EventLogReader(q))
                {
                    int seen = 0;
                    EventRecord rec;
                    while (seen < max && (rec = r.ReadEvent()) != null)
                    {
                        using (rec)
                        {
                            seen++;
                            if (!rec.TimeCreated.HasValue) continue;
                            if (rec.TimeCreated.Value < since) break;   // vi leser nyeste først

                            List<string> d = Data(rec);
                            if (d.Count == 0) continue;

                            AppCrash c = new AppCrash();
                            c.App = d[0];
                            c.Hang = hang;
                            c.Last = rec.TimeCreated.Value;
                            if (!hang && d.Count >= 7)
                            {
                                c.Version = d[1];
                                c.Module = d[3];
                                c.ModuleVersion = d[4];
                                c.Code = d[6];
                                c.Meaning = Explain(c.Code);
                            }
                            else if (hang && d.Count >= 2) c.Version = d[1];

                            if (string.IsNullOrEmpty(c.App)) continue;

                            string key = c.App + "|" + c.Module + "|" + c.Code;
                            AppCrash e;
                            if (map.TryGetValue(key, out e))
                            {
                                e.Count++;
                                if (c.Last > e.Last) e.Last = c.Last;
                            }
                            else map[key] = c;
                        }
                    }
                }
            }
            catch (Exception ex) { Util.Log("Kunne ikke lese programkræsj: " + ex.Message); }
        }

        static List<string> Data(EventRecord rec)
        {
            List<string> l = new List<string>();
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(rec.ToXml());
                foreach (XmlNode n in doc.GetElementsByTagName("Data"))
                    l.Add(n.InnerText);
            }
            catch { }
            return l;
        }

        // De vanligste unntakskodene.
        static string Explain(string code)
        {
            string c = (code ?? "").ToLowerInvariant().Replace("0x", "").TrimStart('0');
            switch (c)
            {
                case "c0000005": return L.T("Leste eller skrev minne den ikke eide");
                case "c0000374": return L.T("Ødela sitt eget minne");
                case "c0000409": return L.T("Skrev utenfor en buffer");
                case "c000041d": return L.T("Feil under håndtering av en annen feil");
                case "c0000006": return L.T("Mistet tilgang til en fil den kjørte fra");
                case "c00000fd": return L.T("Gikk tom for stakkplass");
                case "c0000017": return L.T("Fikk ikke mer minne");
                case "e0434352": return L.T("Ubehandlet .NET-feil");
                case "e06d7363": return L.T("Ubehandlet C++-feil");
                case "80000003": return L.T("Traff et innebygd stoppunkt");
                case "c0000094": return L.T("Delte på null");
                case "c0000096": return L.T("Prøvde en instruksjon den ikke hadde lov til");
                default: return "";
            }
        }

        // Kort råd basert på hva som feilet.
        public static string Advice(AppCrash c)
        {
            if (c == null) return "";
            if (c.Hang)
                return L.T("Programmet sluttet å svare. Skjer det ofte, se om det finnes en nyere versjon.");

            if (string.IsNullOrEmpty(c.Module) ||
                string.Equals(c.Module, "unknown", StringComparison.OrdinalIgnoreCase))
                return L.T("Windows fikk ikke tak i hvilken modul som feilet. Oppdater programmet, eller installer det på nytt.");

            // Kræsjer det i en av Windows sine kjøretider, er det programmets
            // egen kode som feilet — Windows er bare der feilen ble fanget.
            if (IsRuntime(c.Module))
                return L.F("Programmet feilet i sin egen kode. {0} er bare der Windows fanget feilen. Oppdater eller installer {1} på nytt.",
                    c.Module, c.App);

            if (c.Module.StartsWith(StripExt(c.App), StringComparison.OrdinalIgnoreCase))
                return L.F("Feilen ligger i programmet selv ({0}). Oppdater det, eller installer det på nytt.", c.Module);

            return L.F("Feilen ligger i {0}, ikke i {1} selv. Oppdater det som eier {0} — ofte en driver eller et tillegg.",
                c.Module, c.App);
        }

        static readonly string[] Runtimes =
        {
            "kernelbase.dll", "ntdll.dll", "kernel32.dll", "clr.dll", "coreclr.dll",
            "mscoreei.dll", "mscorlib.ni.dll", "ucrtbase.dll", "msvcrt.dll",
            "combase.dll", "user32.dll", "gdi32.dll", "shell32.dll", "ole32.dll",
        };

        static bool IsRuntime(string module)
        {
            string m = (module ?? "").ToLowerInvariant();
            foreach (string r in Runtimes) if (m == r) return true;
            if (m.StartsWith("msvcr") || m.StartsWith("msvcp") || m.StartsWith("vcruntime")) return true;
            return false;
        }

        static string StripExt(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int d = s.LastIndexOf('.');
            return d > 0 ? s.Substring(0, d) : s;
        }
    }
}
