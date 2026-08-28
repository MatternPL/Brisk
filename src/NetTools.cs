using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using Microsoft.Win32;

namespace Brisk
{
    public class NetCheck
    {
        public string What;
        public string Result;
        public int Level;        // 0 ok, 1 merk, 2 feil
    }

    public static class NetTools
    {
        public static List<NetCheck> RunAll(Action<string> progress)
        {
            List<NetCheck> r = new List<NetCheck>();
            Step(progress, "Nettverkskort");
            Adapter(r);
            Step(progress, "Gateway");
            Gateway(r);
            Step(progress, "Internett");
            Internet(r);
            Step(progress, "DNS");
            Dns(r);
            Step(progress, "Wi-Fi");
            WiFi(r);
            Step(progress, "hosts-fil");
            Hosts(r);
            Step(progress, "Proxy");
            Proxy(r);
            return r;
        }

        static void Step(Action<string> p, string s) { if (p != null) p(L.T(s)); }

        static void Add(List<NetCheck> r, string what, string result, int level)
        {
            NetCheck c = new NetCheck();
            c.What = L.T(what);
            c.Result = result;
            c.Level = level;
            r.Add(c);
        }

        // ---------------------------------------------------------------
        static NetworkInterface Active()
        {
            NetworkInterface best = null;
            long bestSpeed = -1;
            foreach (NetworkInterface n in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (n.OperationalStatus != OperationalStatus.Up) continue;
                if (n.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (n.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                IPInterfaceProperties p = n.GetIPProperties();
                if (p.GatewayAddresses.Count == 0) continue;
                if (n.Speed > bestSpeed) { bestSpeed = n.Speed; best = n; }
            }
            return best;
        }

        static void Adapter(List<NetCheck> r)
        {
            NetworkInterface n = Active();
            if (n == null) { Add(r, "Nettverkskort", L.T("Ingen aktiv tilkobling"), 2); return; }
            string speed = n.Speed > 0 ? " · " + (n.Speed / 1000000) + " Mbit/s" : "";
            Add(r, "Nettverkskort", n.Name + " (" + n.NetworkInterfaceType + ")" + speed, 0);
        }

        static void Gateway(List<NetCheck> r)
        {
            NetworkInterface n = Active();
            if (n == null) { Add(r, "Gateway", L.T("Ukjent"), 2); return; }
            IPAddress gw = null;
            foreach (GatewayIPAddressInformation g in n.GetIPProperties().GatewayAddresses)
                if (g.Address != null && g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                { gw = g.Address; break; }
            if (gw == null) { Add(r, "Gateway", L.T("Ingen"), 2); return; }

            long ms = PingMs(gw.ToString());
            if (ms < 0) Add(r, "Gateway", gw + " — " + L.T("svarer ikke"), 2);
            else Add(r, "Gateway", gw + " — " + ms + " ms", ms > 20 ? 1 : 0);
        }

        static void Internet(List<NetCheck> r)
        {
            long ms = PingMs("1.1.1.1");
            if (ms < 0) ms = PingMs("8.8.8.8");
            if (ms < 0) Add(r, "Internett", L.T("Ingen svar"), 2);
            else Add(r, "Internett", ms + " ms", ms > 120 ? 1 : 0);
        }

        static void Dns(List<NetCheck> r)
        {
            NetworkInterface n = Active();
            List<string> servers = new List<string>();
            if (n != null)
                foreach (IPAddress a in n.GetIPProperties().DnsAddresses)
                    if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        servers.Add(a.ToString());

            DateTime t0 = DateTime.Now;
            bool ok;
            try { ok = System.Net.Dns.GetHostEntry("www.microsoft.com").AddressList.Length > 0; }
            catch { ok = false; }
            int ms = (int)(DateTime.Now - t0).TotalMilliseconds;

            string who = servers.Count > 0 ? string.Join(", ", servers.ToArray()) : L.T("ukjent");
            if (!ok) Add(r, "DNS", who + " — " + L.T("klarte ikke slå opp navn"), 2);
            else Add(r, "DNS", who + " — " + ms + " ms", ms > 300 ? 1 : 0);
        }

        static void WiFi(List<NetCheck> r)
        {
            int code;
            string outp = Util.RunCapture("netsh", "wlan show interfaces", out code);
            if (code != 0 || outp.IndexOf("SSID", StringComparison.OrdinalIgnoreCase) < 0)
            {
                Add(r, "Wi-Fi", L.T("Ikke i bruk (kablet)"), 0);
                return;
            }

            string ssid = Field(outp, "SSID");
            string signal = Field(outp, "Signal");
            string rate = Field(outp, "Receive rate (Mbps)");
            if (string.IsNullOrEmpty(rate)) rate = Field(outp, "Mottakshastighet (Mbps)");

            int pct = 0;
            if (!string.IsNullOrEmpty(signal))
                int.TryParse(signal.Replace("%", "").Trim(), out pct);

            string txt = (string.IsNullOrEmpty(ssid) ? "" : ssid + " · ") +
                         (pct > 0 ? pct + " %" : signal) +
                         (string.IsNullOrEmpty(rate) ? "" : " · " + rate + " Mbit/s");
            Add(r, "Wi-Fi", txt, pct > 0 && pct < 50 ? 1 : 0);
        }

        // netsh skriver "  Navn  : verdi" - vi tar første treff etter kolon.
        static string Field(string text, string label)
        {
            foreach (string line in text.Split('\n'))
            {
                int c = line.IndexOf(':');
                if (c <= 0) continue;
                string k = line.Substring(0, c).Trim();
                if (string.Equals(k, label, StringComparison.OrdinalIgnoreCase))
                    return line.Substring(c + 1).Trim();
            }
            return "";
        }

        // ---------------------------------------------------------------
        // Skadevare og «snarveier» omdirigerer ofte trafikk her.
        static void Hosts(List<NetCheck> r)
        {
            try
            {
                string path = Util.Expand("%SystemRoot%\\System32\\drivers\\etc\\hosts");
                if (!File.Exists(path)) { Add(r, "hosts-fil", L.T("Finnes ikke"), 1); return; }

                List<string> entries = new List<string>();
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    if (line.StartsWith("127.0.0.1") && line.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) > 0) continue;
                    if (line.StartsWith("::1") && line.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) > 0) continue;
                    entries.Add(line);
                }

                if (entries.Count == 0) Add(r, "hosts-fil", L.T("Ren"), 0);
                else
                {
                    string sample = entries[0];
                    if (entries.Count > 1) sample += "  (+" + (entries.Count - 1) + ")";
                    Add(r, "hosts-fil", L.F("{0} omdirigeringer: ", entries.Count) + sample, 1);
                }
            }
            catch (Exception ex) { Add(r, "hosts-fil", ex.Message, 1); }
        }

        static void Proxy(List<NetCheck> r)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    if (k == null) { Add(r, "Proxy", L.T("Ingen"), 0); return; }
                    object en = k.GetValue("ProxyEnable");
                    bool on = en != null && Convert.ToInt32(en) == 1;
                    string srv = Convert.ToString(k.GetValue("ProxyServer"));
                    if (!on) Add(r, "Proxy", L.T("Ingen"), 0);
                    else Add(r, "Proxy", srv + " — " + L.T("all trafikk går via denne"), 1);
                }
            }
            catch { Add(r, "Proxy", L.T("Ukjent"), 0); }
        }

        // ---------------------------------------------------------------
        static long PingMs(string host)
        {
            try
            {
                using (Ping p = new Ping())
                {
                    PingReply rep = p.Send(host, 2000);
                    if (rep != null && rep.Status == IPStatus.Success) return rep.RoundtripTime;
                }
            }
            catch { }
            return -1;
        }

        // Nullstiller nettverksstakken. Krever administrator og omstart.
        public static void Reset(Action<string> log)
        {
            log(L.T("Tømmer DNS-cache."));
            Util.Run("ipconfig", "/flushdns", log);
            log(L.T("Fornyer IP-adresse."));
            Util.Run("ipconfig", "/renew", log);
            log(L.T("Nullstiller Winsock."));
            Util.Run("netsh", "winsock reset", log);
            log(L.T("Nullstiller TCP/IP."));
            Util.Run("netsh", "int ip reset", log);
            log(L.T("Ferdig. Start maskinen på nytt for at det skal tre i kraft."));
            Util.Log("Nettverksstakken ble nullstilt.");
        }
    }
}
