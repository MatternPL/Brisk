using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

namespace Brisk
{
    // Henter Chris Titus Tech sin WinUtil. Det er et selvstendig verktoy under
    // MIT-lisens, laget av andre, og Brisk gjor ingenting annet enn aa hente det
    // og starte det.
    //
    // Den vanlige maaten aa starte WinUtil paa er "irm christitus.com/win | iex",
    // altsaa aa kjore fjernkode som administrator uten aa se paa den forst. Det
    // gjor vi ikke. Vi henter nyeste utgivelse fra GitHub sitt API, leser sha256
    // derfra, laster ned og verifiserer for noe kjores - samme regel som Brisk
    // bruker paa sine egne oppdateringer.
    public static class WinUtil
    {
        public const string Project = "https://github.com/ChrisTitusTech/winutil";
        const string LatestApi = "https://api.github.com/repos/ChrisTitusTech/winutil/releases/latest";
        const string AssetName = "winutil.ps1";

        public class Release
        {
            public string Version = "";
            public string Url = "";
            public string Sha256 = "";
            public long Size;
        }

        // Leser nyeste utgivelse. Returnerer null og setter error ved problemer.
        public static Release Latest(out string error)
        {
            error = null;
            string json;
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Brisk/" + Updater.CurrentVersion);
                    wc.Headers.Add("Accept", "application/vnd.github+json");
                    wc.Encoding = Encoding.UTF8;
                    json = wc.DownloadString(LatestApi);
                }
            }
            catch (Exception ex)
            {
                error = L.T("Fikk ikke kontakt med GitHub: ") + ex.Message;
                return null;
            }

            Release r = new Release();
            r.Version = Updater.Field(json, "tag_name");

            // Finn blokken for winutil.ps1 og les feltene som hoerer til den.
            int i = json.IndexOf("\"name\": \"" + AssetName + "\"", StringComparison.Ordinal);
            if (i < 0) i = json.IndexOf("\"name\":\"" + AssetName + "\"", StringComparison.Ordinal);
            if (i < 0)
            {
                error = L.F("Fant ikke {0} i nyeste utgivelse.", AssetName);
                return null;
            }
            string tail = json.Substring(i);

            r.Url = Updater.Field(tail, "browser_download_url");
            string digest = Updater.Field(tail, "digest");
            long size;
            if (long.TryParse(Updater.Field(tail, "size"), out size)) r.Size = size;

            if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                r.Sha256 = digest.Substring(7);

            if (!r.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                error = L.T("Nedlastingsadressen må være https.");
                return null;
            }
            if (r.Sha256.Length != 64)
            {
                error = L.T("Utgivelsen oppgir ingen gyldig sha256. Avbryter.");
                return null;
            }
            return r;
        }

        // Laster ned og verifiserer. Returnerer stien, eller null med error satt.
        public static string Download(Release r, out string error)
        {
            error = null;
            string path = Path.Combine(Path.GetTempPath(),
                "winutil-" + r.Version + ".ps1");
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Brisk/" + Updater.CurrentVersion);
                    wc.DownloadFile(r.Url, path);
                }
            }
            catch (Exception ex)
            {
                error = L.T("Nedlastingen feilet: ") + ex.Message;
                try { File.Delete(path); }
                catch (Exception) { }
                return null;
            }

            string actual = Updater.Sha256Of(path);
            if (!string.Equals(actual, r.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                error = L.T("Sjekksummen stemte ikke. Filen ble slettet — ingenting er kjørt.");
                Util.Log("WinUtil avvist. Forventet " + r.Sha256 + ", fikk " + actual);
                try { File.Delete(path); }
                catch (Exception) { }
                return null;
            }

            Util.Log("WinUtil " + r.Version + " lastet ned og verifisert: " + path);
            return path;
        }

        // Starter skriptet i PowerShell som administrator. Brisk venter ikke paa
        // det; WinUtil har sitt eget vindu.
        public static bool Run(string scriptPath, out string error)
        {
            error = null;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe");
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\"";
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                Util.Log("WinUtil startet.");
                return true;
            }
            catch (Exception ex)
            {
                error = L.T("Klarte ikke å starte WinUtil: ") + ex.Message;
                return false;
            }
        }
    }
}
