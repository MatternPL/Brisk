using System;
using System.IO;
using System.Management;
using System.Net;
using System.Text;
using Microsoft.Win32;

namespace Brisk
{
    public class GpuInfo
    {
        public string Name = "";          // "NVIDIA GeForce RTX 5090"
        public string Vendor = "";        // "NVIDIA", "AMD" eller tom
        public string Installed = "";     // "616.56" / "25.9.1" - slik leverandoren skriver den
        public string RawVersion = "";    // "32.0.16.1656" - slik Windows skriver den
        public DateTime DriverDate;
        public string AppPath = "";       // NVIDIA app / AMD Software, om den finnes
        public string AppName = "";

        public bool Known { get { return Vendor.Length > 0; } }
    }

    public class GpuDriver
    {
        public string Version = "";
        public string Url = "";
        public string Released = "";
        public bool Newer;                // nyere enn den som er installert
    }

    // ------------------------------------------------------------------
    //  Skjermkort og driver
    //
    //  Windows oppgir driverversjonen i sitt eget format (32.0.16.1656).
    //  NVIDIA kaller den samme driveren 616.56 - tallet er de fem siste
    //  sifrene med punktum foran de to siste. AMD skriver sin egen versjon
    //  ("25.9.1") i registeret under skjermkortet, saa der leser vi den rett
    //  av i stedet for aa regne.
    //
    //  For aa vite om det finnes noe nyere:
    //    NVIDIA - deres eget nedlastingssok, det samme nettsidene bruker.
    //             Serie- og produkt-ID hentes ved hvert oppslag i stedet for
    //             aa ligge som tabell i koden, saa det virker ogsaa for kort
    //             som kommer etter denne versjonen av Brisk.
    //    AMD    - nedlastingssida deres har én lenke til nettinstallatoren,
    //             og versjonen staar i filnavnet. Den installatoren finner
    //             selv ut hvilket kort maskina har, saa den passer alle.
    //
    //  Begge er nettsider som kan endre seg. Gaar oppslaget i staa, skal
    //  brukeren faa vite at vi ikke fikk sjekket - ikke at alt er i orden.
    // ------------------------------------------------------------------
    public static class GpuTools
    {
        const string NvLookup = "https://www.nvidia.com/Download/API/lookupValueSearch.aspx";
        const string NvSearch = "https://gfwsl.geforce.com/services_toolkit/services/" +
                                "com/nvidia/services/AjaxDriverService.php";
        const string AmdSide = "https://www.amd.com/en/support/download/drivers.html";

        const string Skjermkort = @"SYSTEM\CurrentControlSet\Control\Class\" +
                                  "{4d36e968-e325-11ce-bfc1-08002be10318}";

        // ---------------------------------------------------------------
        public static GpuInfo Read()
        {
            GpuInfo best = null;
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(
                    "SELECT Name, DriverVersion, DriverDate FROM Win32_VideoController"))
                foreach (ManagementObject o in s.Get())
                {
                    GpuInfo g = new GpuInfo();
                    g.Name = Str(o["Name"]);
                    g.RawVersion = Str(o["DriverVersion"]);
                    g.Vendor = Merke(g.Name);
                    g.DriverDate = Dato(Str(o["DriverDate"]));

                    if (g.Vendor == "NVIDIA") g.Installed = NvidiaVersion(g.RawVersion);
                    else if (g.Vendor == "AMD") g.Installed = AmdVersion(g.Name, g.RawVersion);
                    else g.Installed = g.RawVersion;

                    // Et kort vi kan sjekke driver for vinner over integrert
                    // grafikk: det er det som faktisk driver spillene.
                    if (best == null || (g.Known && !best.Known)) best = g;
                }
            }
            catch (Exception ex) { Util.Log("Kunne ikke lese skjermkort: " + ex.Message); }

            if (best == null) best = new GpuInfo();
            FinnApp(best);
            return best;
        }

        static string Merke(string navn)
        {
            if (string.IsNullOrEmpty(navn)) return "";
            string n = navn.ToUpperInvariant();
            if (n.Contains("NVIDIA") || n.Contains("GEFORCE")) return "NVIDIA";
            if (n.Contains("AMD") || n.Contains("RADEON")) return "AMD";
            return "";
        }

        static DateTime Dato(string d)
        {
            try
            {
                if (d != null && d.Length >= 8)
                    return new DateTime(int.Parse(d.Substring(0, 4)),
                                        int.Parse(d.Substring(4, 2)),
                                        int.Parse(d.Substring(6, 2)));
            }
            catch (Exception) { }
            return DateTime.MinValue;
        }

        // Windows: 32.0.16.1656 -> NVIDIA: 616.56
        public static string NvidiaVersion(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in raw) if (c >= '0' && c <= '9') sb.Append(c);
            string t = sb.ToString();
            if (t.Length < 5) return raw;
            t = t.Substring(t.Length - 5);
            return t.Substring(0, 3) + "." + t.Substring(3);
        }

        // AMD skriver Adrenalin-versjonen sin ved siden av driveren i
        // registeret. Finnes den ikke, faar brukeren Windows-versjonen -
        // den er riktig, bare ikke tallet AMD selv snakker om.
        public static string AmdVersion(string navn, string raw)
        {
            try
            {
                using (RegistryKey rot = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                           RegistryView.Registry64).OpenSubKey(Skjermkort))
                {
                    if (rot == null) return raw;
                    string reserve = null;
                    foreach (string navnPaaUndernokkel in rot.GetSubKeyNames())
                    {
                        using (RegistryKey k = rot.OpenSubKey(navnPaaUndernokkel))
                        {
                            if (k == null) continue;
                            string v = Str(k.GetValue("RadeonSoftwareVersion")).Trim();
                            if (v.Length == 0) continue;
                            string desc = Str(k.GetValue("DriverDesc"));
                            if (string.Equals(desc, navn, StringComparison.OrdinalIgnoreCase))
                                return v;
                            if (reserve == null) reserve = v;
                        }
                    }
                    if (reserve != null) return reserve;
                }
            }
            catch (Exception ex) { Util.Log("Kunne ikke lese AMD-driverversjon: " + ex.Message); }
            return raw;
        }

        static void FinnApp(GpuInfo g)
        {
            string[] steder =
            {
                @"NVIDIA Corporation\NVIDIA app\CEF\NVIDIA app.exe",
                @"NVIDIA Corporation\NVIDIA GeForce Experience\NVIDIA GeForce Experience.exe",
                @"AMD\CNext\CNext\RadeonSoftware.exe",
                @"AMD\CIM\BIN64\InstallManagerApp.exe"
            };
            string[] navn = { "NVIDIA App", "GeForce Experience", "AMD Software", "AMD Software" };
            string[] merke = { "NVIDIA", "NVIDIA", "AMD", "AMD" };
            string[] rot =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            for (int i = 0; i < steder.Length; i++)
            {
                if (g.Vendor.Length > 0 && merke[i] != g.Vendor) continue;
                foreach (string r in rot)
                {
                    if (string.IsNullOrEmpty(r)) continue;
                    try
                    {
                        string p = Path.Combine(r, steder[i]);
                        if (File.Exists(p)) { g.AppPath = p; g.AppName = navn[i]; return; }
                    }
                    catch (Exception) { }
                }
            }
        }

        // ---------------------------------------------------------------
        public static GpuDriver Latest(GpuInfo g, out string error)
        {
            error = null;
            if (g == null || !g.Known)
            {
                error = L.T("Brisk kan bare sjekke drivere for NVIDIA- og AMD-skjermkort.");
                return null;
            }
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
                return g.Vendor == "NVIDIA" ? Nvidia(g, out error) : Amd(g, out error);
            }
            catch (Exception ex)
            {
                error = L.F("Fikk ikke kontakt med {0}: ", g.Vendor) + ex.Message;
                return null;
            }
        }

        // ---------------------------------------------------------------
        static GpuDriver Nvidia(GpuInfo g, out string error)
        {
            error = null;
            string serie = SerieNavn(g.Name);
            if (serie == null)
            {
                error = L.F("Fant ikke {0} i listene hos NVIDIA.", g.Name);
                return null;
            }

            string psid = FinnVerdi(Hent(NvLookup + "?TypeID=2"), serie, true);
            if (psid == null)
            {
                error = L.F("Fant ikke {0} i listene hos NVIDIA.", serie);
                return null;
            }

            string produkter = Hent(NvLookup + "?TypeID=3&ParentID=" + psid);
            string pfid = FinnVerdi(produkter, g.Name, true);
            if (pfid == null) pfid = FinnVerdi(produkter, g.Name, false);
            if (pfid == null)
            {
                error = L.F("Fant ikke {0} i listene hos NVIDIA.", g.Name);
                return null;
            }

            string osNavn = Environment.OSVersion.Version.Build >= 22000
                ? "Windows 11" : "Windows 10 64-bit";
            string osId = FinnVerdi(Hent(NvLookup + "?TypeID=4&ParentID=" + psid), osNavn, true);
            if (osId == null) osId = Environment.OSVersion.Version.Build >= 22000 ? "135" : "57";

            string json = Hent(NvSearch + "?func=DriverManualLookup" +
                "&psid=" + psid + "&pfid=" + pfid + "&osID=" + osId +
                "&languageCode=1033&beta=0&isWHQL=1&dltype=-1&dch=1" +
                "&upCRD=0&qnf=0&sort1=0&numberOfResults=1");

            GpuDriver d = new GpuDriver();
            d.Version = Felt(json, "Version");
            d.Url = Felt(json, "DownloadURL");
            d.Released = Felt(json, "ReleaseDateTime");
            if (d.Version.Length == 0)
            {
                error = L.T("NVIDIA svarte, men uten en driverversjon.");
                return null;
            }
            d.Newer = Nyere(d.Version, g.Installed);
            return d;
        }

        // AMD har ingen oppslagstjeneste, men nedlastingssida har én lenke til
        // nettinstallatoren, og versjonen staar i filnavnet:
        //   .../amd-software-adrenalin-edition-26.8.1-minimalsetup-260818_web.exe
        static GpuDriver Amd(GpuInfo g, out string error)
        {
            error = null;
            string html = Hent(AmdSide);

            string beste = null, besteVer = null;
            int p = 0;
            while (true)
            {
                int a = html.IndexOf("https://drivers.amd.com/", p, StringComparison.OrdinalIgnoreCase);
                if (a < 0) break;
                int b = a;
                while (b < html.Length && html[b] != '"' && html[b] != '\'' && html[b] != ' ' &&
                       html[b] != '<' && html[b] != '\n' && html[b] != '\r') b++;
                string url = html.Substring(a, b - a);
                p = b;
                if (!url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

                string ver = VersjonIFilnavn(url);
                if (ver == null) continue;
                if (besteVer == null || Nyere(ver, besteVer)) { besteVer = ver; beste = url; }
            }

            if (besteVer == null)
            {
                error = L.T("Fant ingen driverversjon på AMD sine sider. De kan ha lagt om.");
                return null;
            }

            GpuDriver d = new GpuDriver();
            d.Version = besteVer;
            d.Url = beste;
            d.Newer = Nyere(d.Version, g.Installed);
            return d;
        }

        // "amd-software-adrenalin-edition-26.8.1-minimalsetup-260818_web.exe" -> "26.8.1"
        static string VersjonIFilnavn(string url)
        {
            string fil = url;
            int skrå = fil.LastIndexOf('/');
            if (skrå >= 0) fil = fil.Substring(skrå + 1);

            const string merke = "adrenalin-edition-";
            int i = fil.IndexOf(merke, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;
            i += merke.Length;

            int j = i;
            while (j < fil.Length && (fil[j] == '.' || (fil[j] >= '0' && fil[j] <= '9'))) j++;
            string v = fil.Substring(i, j - i).Trim('.');
            return v.Length >= 3 && v.IndexOf('.') > 0 ? v : null;
        }

        // "NVIDIA GeForce RTX 5090"        -> "GeForce RTX 50 Series"
        // "NVIDIA GeForce RTX 4060 Laptop" -> "GeForce RTX 40 Series (Notebooks)"
        static string SerieNavn(string navn)
        {
            if (string.IsNullOrEmpty(navn)) return null;
            string n = navn.ToUpperInvariant();
            bool baerbar = n.Contains("LAPTOP") || n.Contains("MAX-Q") || n.Contains("MOBILE");

            int i = n.IndexOf("RTX ", StringComparison.Ordinal);
            if (i >= 0 && Siffer(n, i + 4, 4))
                return "GeForce RTX " + n[i + 4] + "0 Series" + (baerbar ? " (Notebooks)" : "");

            i = n.IndexOf("GTX ", StringComparison.Ordinal);
            if (i >= 0)
            {
                if (Siffer(n, i + 4, 4) && n.Substring(i + 4, 2) == "16")
                    return baerbar ? "GeForce GTX 16 Series (Notebooks)" : "GeForce 16 Series";
                if (Siffer(n, i + 4, 4) && n.Substring(i + 4, 2) == "10")
                    return "GeForce 10 Series" + (baerbar ? " (Notebooks)" : "");
                if (Siffer(n, i + 4, 3) && n[i + 4] == '9')
                    return baerbar ? "GeForce 900M Series (Notebooks)" : "GeForce 900 Series";
            }
            return null;
        }

        static bool Siffer(string s, int fra, int antall)
        {
            if (fra < 0 || fra + antall > s.Length) return false;
            for (int i = 0; i < antall; i++)
                if (s[fra + i] < '0' || s[fra + i] > '9') return false;
            return true;
        }

        // Leter etter <Name>…</Name><Value>…</Value> i svaret fra NVIDIA.
        // eksakt = navnet maa stemme helt; ellers holder det at det ene
        // inneholder det andre (kortnavn har av og til et tillegg).
        static string FinnVerdi(string xml, string navn, bool eksakt)
        {
            if (xml == null || navn == null) return null;
            int p = 0;
            while (true)
            {
                int a = xml.IndexOf("<Name>", p, StringComparison.Ordinal);
                if (a < 0) return null;
                int b = xml.IndexOf("</Name>", a, StringComparison.Ordinal);
                if (b < 0) return null;
                int c = xml.IndexOf("<Value>", b, StringComparison.Ordinal);
                int d = c < 0 ? -1 : xml.IndexOf("</Value>", c, StringComparison.Ordinal);
                if (c < 0 || d < 0) return null;

                string n = xml.Substring(a + 6, b - a - 6).Trim();
                string v = xml.Substring(c + 7, d - c - 7).Trim();
                bool treff = eksakt
                    ? string.Equals(n, navn, StringComparison.OrdinalIgnoreCase)
                    : n.IndexOf(navn, StringComparison.OrdinalIgnoreCase) >= 0
                      || navn.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0;
                if (treff) return v;
                p = d;
            }
        }

        // Svaret er JSON med mellomrom rundt kolonet: "Version" : "616.56"
        static string Felt(string json, string navn)
        {
            if (json == null) return "";
            int i = json.IndexOf("\"" + navn + "\"", StringComparison.Ordinal);
            if (i < 0) return "";
            int k = json.IndexOf(':', i);
            if (k < 0) return "";
            int a = json.IndexOf('"', k);
            if (a < 0) return "";
            int b = json.IndexOf('"', a + 1);
            if (b < 0) return "";
            return json.Substring(a + 1, b - a - 1);
        }

        // Sammenligner ledd for ledd: 616.56 mot 616.9, eller 26.8.1 mot 25.9.1.
        // Et rent tallsammenligning ville sagt at 616.9 er stoerre enn 616.56.
        public static bool Nyere(string ny, string gammel)
        {
            if (string.IsNullOrEmpty(ny)) return false;
            if (string.IsNullOrEmpty(gammel)) return true;
            string[] a = ny.Split('.'), b = gammel.Split('.');
            int n = Math.Max(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                int x = i < a.Length ? Tall(a[i]) : 0;
                int y = i < b.Length ? Tall(b[i]) : 0;
                if (x != y) return x > y;
            }
            return false;
        }

        static int Tall(string s)
        {
            int v;
            return int.TryParse(s.Trim(), out v) ? v : 0;
        }

        static string Hent(string url)
        {
            using (WebClient wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "Brisk/" + Updater.CurrentVersion);
                wc.Encoding = Encoding.UTF8;
                return wc.DownloadString(url);
            }
        }

        // ---------------------------------------------------------------
        //  Laster ned driveren til Nedlastinger. Brisk kjorer den ikke selv -
        //  en driverinstallasjon slaar av skjermen underveis og skal startes
        //  naar brukeren er klar for det.
        public static string Download(GpuDriver d, string merke,
                                      Action<long, long> progress, out string error)
        {
            error = null;
            if (d == null || d.Url.Length == 0)
            {
                error = L.T("Ingen nedlastingsadresse.");
                return null;
            }

            // Adressen kommer fra nettet. Er den ikke fra leverandoren over
            // https, lastes ingenting ned.
            bool trygg = d.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                (Vert(d.Url, "nvidia.com") || Vert(d.Url, "geforce.com") || Vert(d.Url, "amd.com"));
            if (!trygg)
            {
                error = L.T("Nedlastingsadressen er ikke fra NVIDIA eller AMD. Avbrutt.");
                Util.Log("Avviste driveradresse: " + d.Url);
                return null;
            }

            string fil = Path.Combine(Nedlastinger(),
                (merke.Length > 0 ? merke : "GPU") + "-" + d.Version + "-driver.exe");
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(d.Url);
                req.UserAgent = "Brisk/" + Updater.CurrentVersion;
                req.Timeout = 30000;

                // AMD sender den direkte lenka videre til en HTML-side som
                // heter «Download Not Complete» hvis forespoerselen ikke kommer
                // fra deres egen nedlastingsside. Da lastet Brisk ned den sida
                // og lagret den som .exe - en fil som ikke lar seg kjore.
                // Maalt: uten Referer kommer text/html, med Referer kommer
                // application/octet-stream paa 47,8 MB som begynner med MZ.
                // Det er ikke User-Agent-avhengig; en nettleser-UA fikk samme
                // omdirigering.
                if (Vert(d.Url, "amd.com")) req.Referer = AmdSide;
                using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
                using (Stream src = res.GetResponseStream())
                using (FileStream dst = new FileStream(fil, FileMode.Create, FileAccess.Write))
                {
                    long total = res.ContentLength;
                    byte[] buf = new byte[65536];
                    long got = 0;
                    int r;
                    while ((r = src.Read(buf, 0, buf.Length)) > 0)
                    {
                        dst.Write(buf, 0, r);
                        got += r;
                        if (progress != null) progress(got, total);
                    }
                }
            }
            catch (Exception ex)
            {
                error = L.T("Nedlastingen feilet: ") + ex.Message;
                try { if (File.Exists(fil)) File.Delete(fil); }
                catch (Exception) { }
                return null;
            }

            // Er det i det hele tatt et program? Alle Windows-programmer
            // begynner med bokstavene MZ. Uten denne sjekken sa Brisk at
            // driveren var lastet ned, mens fila i virkeligheten var en
            // HTML-side lagret med .exe paa slutten - og brukeren fikk en fil
            // som ikke gjorde noe naar han dobbeltklikket den.
            //
            // Sjekken staar her og ikke bare i AMD-delen: gaar en leverandor
            // om paa samme maate i morgen, skal Brisk si fra i stedet for aa
            // levere soppel med et fornoyd ansikt.
            if (!ErProgram(fil))
            {
                error = L.T("Det som ble lastet ned var ikke et program. Leverandøren svarte med noe annet — prøv å laste ned driveren fra nettsiden deres.");
                Util.Log("Driverfila var ikke et program, slettet: " + fil);
                try { if (File.Exists(fil)) File.Delete(fil); }
                catch (Exception) { }
                return null;
            }

            Util.Log(merke + "-driver " + d.Version + " lastet ned til " + fil);
            return fil;
        }

        // Alle Windows-programmer begynner med MZ. Er de to foerste bytene noe
        // annet, er fila ikke et program uansett hva den heter.
        static bool ErProgram(string fil)
        {
            try
            {
                using (FileStream fs = File.OpenRead(fil))
                    return fs.ReadByte() == 'M' && fs.ReadByte() == 'Z';
            }
            catch (Exception) { return false; }
        }

        // Verten maa vaere domenet selv eller et underdomene - ikke bare
        // inneholde teksten et sted i adressen.
        static bool Vert(string url, string domene)
        {
            try
            {
                string h = new Uri(url).Host;
                return h.Equals(domene, StringComparison.OrdinalIgnoreCase) ||
                       h.EndsWith("." + domene, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) { return false; }
        }

        static string Nedlastinger()
        {
            try
            {
                string p = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (Directory.Exists(p)) return p;
            }
            catch (Exception) { }
            return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

        static string Str(object o) { return o == null ? "" : o.ToString(); }
    }
}
