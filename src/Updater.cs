using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace Brisk
{
    public class UpdateInfo
    {
        public string Version;
        public string Url;
        public string Sha256;
        public string Notes = "";
        public long Size;
    }

    // Selvoppdatering: henter en liten versjonsfil, sammenligner, laster ned
    // installasjonsfilen og kjører den stille. Alt bekreftes av brukeren først.
    public static class Updater
    {
        // Standardadresse. Kan overstyres uten å bygge på nytt ved å sette
        // HKCU\Software\Brisk\OppdateringsUrl til en annen https-adresse.
        public const string DefaultManifestUrl =
            "https://raw.githubusercontent.com/MatternPL/Brisk/main/oppdatering.json";

        const string SettingsKey = @"Software\Brisk";

        // ---------------------------------------------------------------
        public static string CurrentVersion
        {
            get
            {
                try
                {
                    Version v = Assembly.GetExecutingAssembly().GetName().Version;
                    return v.Major + "." + v.Minor + "." + v.Build;
                }
                catch { return "1.0.0"; }
            }
        }

        public static string ManifestUrl
        {
            get
            {
                string v = ReadSetting("OppdateringsUrl");
                return string.IsNullOrEmpty(v) ? DefaultManifestUrl : v;
            }
            set { WriteSetting("OppdateringsUrl", value); }
        }

        public static bool AutoCheck
        {
            get { return ReadSetting("SjekkAutomatisk") != "0"; }
            set { WriteSetting("SjekkAutomatisk", value ? "1" : "0"); }
        }

        public static DateTime LastCheck
        {
            get
            {
                long t;
                if (long.TryParse(ReadSetting("SistSjekket"), out t))
                {
                    try { return new DateTime(t); }
                    catch { }
                }
                return DateTime.MinValue;
            }
            set { WriteSetting("SistSjekket", value.Ticks.ToString()); }
        }

        static string ReadSetting(string name)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(SettingsKey))
                    return k == null ? null : Convert.ToString(k.GetValue(name));
            }
            catch { return null; }
        }

        static void WriteSetting(string name, string value)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(SettingsKey))
                    if (k != null) k.SetValue(name, value);
            }
            catch { }
        }

        // ---------------------------------------------------------------
        // Returnerer info om en nyere versjon, eller null. feil settes ved problemer.
        public static UpdateInfo Check(out string error)
        {
            error = null;
            string url = ManifestUrl;
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                error = L.T("Oppdateringsadressen må være https.");
                return null;
            }

            string json;
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Brisk/" + CurrentVersion);
                    // Versjonsfila er liten og skal alltid vaere fersk. Et
                    // engangsledd i adressen er ikke nok alene: bade .NET sin
                    // egen mellomlagring og eventuelle mellomtjenere maa faa
                    // beskjed. En ny utgivelse ble en gang meldt som «du har
                    // nyeste» i flere minutter etter at den var publisert.
                    wc.Headers.Add("Cache-Control", "no-cache");
                    wc.Headers.Add("Pragma", "no-cache");
                    wc.CachePolicy = new System.Net.Cache.RequestCachePolicy(
                        System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
                    wc.Encoding = Encoding.UTF8;
                    json = wc.DownloadString(url + (url.IndexOf('?') >= 0 ? "&" : "?") +
                                             "t=" + DateTime.UtcNow.Ticks);
                }
            }
            catch (Exception ex)
            {
                error = L.T("Fikk ikke kontakt med oppdateringskilden: ") + ex.Message;
                return null;
            }

            UpdateInfo u = new UpdateInfo();
            u.Version = Field(json, "versjon");
            u.Url = Field(json, "url");
            u.Sha256 = Field(json, "sha256");
            u.Notes = Field(json, "notat");
            long size;
            if (long.TryParse(Field(json, "storrelse"), out size)) u.Size = size;

            if (string.IsNullOrEmpty(u.Version) || string.IsNullOrEmpty(u.Url))
            {
                error = L.T("Versjonsfilen kunne ikke tolkes.");
                return null;
            }
            if (!u.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                error = L.T("Nedlastingsadressen i versjonsfilen må være https.");
                return null;
            }
            if (string.IsNullOrEmpty(u.Sha256) || u.Sha256.Length != 64)
            {
                error = L.T("Versjonsfilen mangler en gyldig sha256-sjekksum. Avbryter.");
                return null;
            }

            LastCheck = DateTime.Now;
            if (Compare(u.Version, CurrentVersion) <= 0) return null;   // ikke nyere
            return u;
        }

        // Enkel felt-uthenting. Versjonsfilen er vår egen og har flat struktur.
        public static string Field(string json, string name)
        {
            if (json == null) return "";
            string key = "\"" + name + "\"";
            int i = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return "";
            i = json.IndexOf(':', i + key.Length);
            if (i < 0) return "";
            i++;
            while (i < json.Length && (json[i] == ' ' || json[i] == '\t')) i++;
            if (i >= json.Length) return "";

            if (json[i] == '"')
            {
                i++;
                StringBuilder sb = new StringBuilder();
                while (i < json.Length && json[i] != '"')
                {
                    if (json[i] == '\\' && i + 1 < json.Length)
                    {
                        i++;
                        if (json[i] == 'n') sb.Append('\n');
                        else if (json[i] == 't') sb.Append('\t');
                        else sb.Append(json[i]);
                    }
                    else sb.Append(json[i]);
                    i++;
                }
                return sb.ToString().Trim();
            }

            int start = i;
            while (i < json.Length && json[i] != ',' && json[i] != '}' && json[i] != '\n') i++;
            return json.Substring(start, i - start).Trim();
        }

        // 1 hvis a er nyere enn b, -1 hvis eldre, 0 hvis lik.
        public static int Compare(string a, string b)
        {
            string[] pa = (a ?? "").Split('.');
            string[] pb = (b ?? "").Split('.');
            int n = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                int x = 0, y = 0;
                if (i < pa.Length) int.TryParse(pa[i], out x);
                if (i < pb.Length) int.TryParse(pb[i], out y);
                if (x != y) return x > y ? 1 : -1;
            }
            return 0;
        }

        // ---------------------------------------------------------------
        public delegate void DownloadProgress(long got, long total);

        // Laster ned og sjekker summen. Returnerer filsti, eller null ved feil.
        public static string Download(UpdateInfo u, DownloadProgress progress, out string error)
        {
            error = null;
            string path = Path.Combine(Path.GetTempPath(),
                "Brisk-" + u.Version + "-installer.exe");
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(u.Url);
                req.UserAgent = "Brisk/" + CurrentVersion;
                req.Timeout = 30000;
                using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
                using (Stream src = res.GetResponseStream())
                using (FileStream dst = new FileStream(path, FileMode.Create, FileAccess.Write))
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
                Try(delegate { File.Delete(path); });
                return null;
            }

            // Naar sjekksummen ikke stemmer er det aldri fila hos oss som er
            // gal - den er verifisert for utgivelse. Noe mellom GitHub og
            // maskinen har endret den underveis. Gjett paa hva, i stedet for
            // aa la brukeren staa igjen med «stemte ikke» og ingen vei videre.
            string actual = Sha256Of(path);
            if (!string.Equals(actual, u.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                error = Forklar(path, u.Size) + " " + L.T("Last den ned selv fra utgivelsessida.");
                Util.Log("Oppdatering avvist. Forventet " + u.Sha256 + " (" + u.Size +
                         " bytes), fikk " + actual + ". " + Forklar(path, u.Size));
                Try(delegate { File.Delete(path); });
                return null;
            }

            // Sjekksummen sier bare at fila stemmer med manifestet - og
            // sjekksummen kom fra manifestet. Signaturen er den uavhengige
            // kontrollen: den binder fila til sertifikatet vaart, og det kan
            // ikke byttes ut ved aa servere en annen JSON-fil.
            //
            // Alt fra og med 1.7.0 er signert, saa dette skal aldri slaa inn
            // paa en ekte utgivelse. Gjor det likevel det, er det riktig aa
            // stoppe - da er det noe annet enn oss som ligger der.
            string signaturfeil = Signatur.Sjekk(path);
            if (signaturfeil != null)
            {
                error = signaturfeil + " " + L.T("Last den ned selv fra utgivelsessida.");
                Util.Log("Oppdatering avvist paa signatur: " + signaturfeil);
                Try(delegate { File.Delete(path); });
                return null;
            }

            Util.Log("Oppdatering " + u.Version + " lastet ned og verifisert.");
            return path;
        }

        // Hvorfor stemte ikke sjekksummen. Fila paa utgivelsen er verifisert
        // for den ble publisert, saa avviket har alltid oppstaatt paa veien.
        // Egen metode fordi de fire tilfellene maa kunne testes hver for seg.
        public static string Forklar(string path, long forventet)
        {
            if (!File.Exists(path))
                return L.T("Filen forsvant rett etter nedlastingen. Det er nesten alltid et antivirus som har tatt den.");

            long lengde = -1;
            bool erProgram = false;
            try
            {
                lengde = new FileInfo(path).Length;
                using (FileStream fs = File.OpenRead(path))
                    erProgram = fs.ReadByte() == 'M' && fs.ReadByte() == 'Z';
            }
            catch (Exception)
            {
                return L.T("Filen kunne ikke leses etter nedlastingen. Et antivirus har trolig låst den.");
            }

            if (!erProgram)
                return L.T("Det som ble lastet ned var ikke et program. Noe mellom deg og GitHub byttet ut fila — ofte en antivirus eller et nettverksfilter.");
            if (forventet > 0 && lengde != forventet)
                return L.T("Nedlastingen ble avbrutt underveis. Filen ble slettet — ingenting er kjørt.");
            return L.T("Sjekksummen stemte ikke. Filen ble slettet — ingenting er kjørt.");
        }

        public static string Sha256Of(string path)
        {
            try
            {
                using (SHA256 sha = SHA256.Create())
                using (FileStream fs = File.OpenRead(path))
                {
                    byte[] h = sha.ComputeHash(fs);
                    StringBuilder sb = new StringBuilder(64);
                    foreach (byte b in h) sb.Append(b.ToString("x2"));
                    return sb.ToString();
                }
            }
            catch { return ""; }
        }

        // Starter installasjonsfilen. Programmet avsluttes rett etterpå av installeren.
        public static bool Apply(string installerPath, bool installedNormally, out string error)
        {
            error = null;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(installerPath);
                psi.Arguments = installedNormally ? "/S /start" : "";
                psi.UseShellExecute = true;
                Process.Start(psi);
                Util.Log("Startet oppdatering fra " + installerPath);
                return true;
            }
            catch (Exception ex)
            {
                error = L.T("Klarte ikke starte installasjonen: ") + ex.Message;
                return false;
            }
        }

        // Ligger vi i den vanlige installasjonsmappa? Da kan vi bytte oss selv stille ut.
        public static bool InstalledNormally()
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "Brisk");
                string me = Path.GetFullPath(Util.ExePath());
                return me.StartsWith(Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        static void Try(Action a) { try { a(); } catch { } }
    }
}
