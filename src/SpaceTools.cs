using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using Microsoft.Win32;

namespace Brisk
{
    // Plass Windows selv legger beslag på. Dette dukker ikke opp i vanlig
    // rydding, og er ofte den største enkeltposten på maskinen.
    public class SpaceItem
    {
        public string Key = "";          // hiberfil, restore, pagefile, swapfile, winsxs
        public string Name = "";
        public string What = "";         // hva det er, på vanlig språk
        public long Size;
        public bool CanFree;             // finnes det en trygg handling?
        public string Action = "";       // knappetekst
        public string Consequence = "";  // hva du mister
        public bool NeedsAdmin = true;
    }

    public static class SpaceTools
    {
        public static List<SpaceItem> Scan()
        {
            List<SpaceItem> list = new List<SpaceItem>();
            string sys = Util.Expand("%SystemDrive%\\");

            // --- dvalefil ---
            long hib = FileSize(sys + "hiberfil.sys");
            if (hib > 0)
            {
                SpaceItem s = new SpaceItem();
                s.Key = "hiberfil";
                s.Name = L.T("Dvalefil");
                s.What = L.T("Windows reserverer plass lik RAM-en for å kunne dvale. Den brukes også av hurtigoppstart.");
                s.Size = hib;
                s.CanFree = true;
                s.Action = L.T("Slå av dvale");
                s.Consequence = L.T("Du mister dvalemodus og hurtigoppstart. Maskinen starter fra bunnen hver gang — på en SSD merkes det knapt. Kan slås på igjen når som helst.");
                list.Add(s);
            }

            // --- gjenopprettingspunkter ---
            long used = ShadowUsed();
            if (used > 0)
            {
                SpaceItem s = new SpaceItem();
                s.Key = "restore";
                s.Name = L.T("Gjenopprettingspunkter");
                s.What = L.T("Eldre kopier av systemfiler og innstillinger, brukt av Systemgjenoppretting.");
                s.Size = used;
                s.CanFree = true;
                s.Action = L.T("Behold bare det nyeste");
                s.Consequence = L.T("Du beholder det nyeste punktet og mister de eldre. Du kan fortsatt rulle tilbake, men ikke like langt.");
                list.Add(s);
            }

            // --- vekslingsfil ---
            long page = FileSize(sys + "pagefile.sys");
            if (page > 0)
            {
                SpaceItem s = new SpaceItem();
                s.Key = "pagefile";
                s.Name = L.T("Vekslingsfil");
                s.What = L.T("Windows sitt reservelager når RAM-en tar slutt. Bør stå i fred.");
                s.Size = page;
                s.CanFree = false;
                list.Add(s);
            }

            long swap = FileSize(sys + "swapfile.sys");
            if (swap > 1024 * 1024)
            {
                SpaceItem s = new SpaceItem();
                s.Key = "swapfile";
                s.Name = L.T("Swapfil for apper");
                s.What = L.T("Brukes av Store-apper som settes på pause. Styres av Windows.");
                s.Size = swap;
                s.CanFree = false;
                list.Add(s);
            }

            return list;
        }

        static long FileSize(string path)
        {
            try
            {
                FileInfo fi = new FileInfo(path);
                return fi.Exists ? fi.Length : 0;
            }
            catch { return 0; }
        }

        public static bool HibernationOn()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Power"))
                {
                    if (k == null) return false;
                    object v = k.GetValue("HibernateEnabled");
                    return v != null && Convert.ToInt32(v) != 0;
                }
            }
            catch { return false; }
        }

        static long ShadowUsed()
        {
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(
                    "SELECT UsedSpace FROM Win32_ShadowStorage"))
                {
                    long sum = 0;
                    foreach (ManagementObject mo in s.Get())
                    {
                        try { sum += Convert.ToInt64(mo["UsedSpace"]); }
                        catch { }
                    }
                    return sum;
                }
            }
            catch { return 0; }
        }

        // ---------------------------------------------------------------
        public static bool Free(SpaceItem item, Action<string> log)
        {
            if (item == null) return false;
            switch (item.Key)
            {
                case "hiberfil":
                    log(L.T("Slår av dvale …"));
                    Util.Run("powercfg.exe", "/hibernate off", log);
                    System.Threading.Thread.Sleep(700);
                    bool gone = FileSize(Util.Expand("%SystemDrive%\\hiberfil.sys")) == 0;
                    log(gone ? L.T("Dvalefila er borte.")
                             : L.T("Fila ligger fortsatt der. Krever administrator."));
                    Util.Log("Dvale slått av: " + gone);
                    return gone;

                case "restore":
                    log(L.T("Fjerner eldre gjenopprettingspunkter …"));
                    // /all beholder ikke noe; /oldest tar ett om gangen. Vi
                    // kjører til bare det nyeste står igjen.
                    for (int i = 0; i < 40; i++)
                    {
                        int code = Util.Run("vssadmin.exe",
                            "delete shadows /for=" + Util.Expand("%SystemDrive%") + " /oldest /quiet", log);
                        if (code != 0) break;
                    }
                    log(L.T("Ferdig."));
                    Util.Log("Eldre gjenopprettingspunkter fjernet.");
                    return true;
            }
            return false;
        }

        // Slår dvale på igjen.
        public static void HibernationOn(Action<string> log)
        {
            log(L.T("Slår på dvale igjen …"));
            Util.Run("powercfg.exe", "/hibernate on", log);
            Util.Log("Dvale slått på.");
        }

        // ---------------------------------------------------------------
        // Komponentlageret. DISM bruker et halvt minutt, så dette kalles bare
        // når brukeren ber om det.
        public static string AnalyseComponentStore(Action<string> log, out long reclaimable)
        {
            reclaimable = 0;
            string result = "";
            long found = 0;
            Util.Run("dism.exe", "/Online /Cleanup-Image /AnalyzeComponentStore",
                delegate(string line)
                {
                    log(line);
                    string t = line.Trim();
                    int c = t.IndexOf(':');
                    if (c <= 0) return;
                    string key = t.Substring(0, c).Trim().ToLowerInvariant();
                    string val = t.Substring(c + 1).Trim();
                    if (key.IndexOf("reclaimable", StringComparison.Ordinal) >= 0 &&
                        key.IndexOf("packages", StringComparison.Ordinal) < 0)
                        found = ParseSize(val);
                    if (key.IndexOf("recommended", StringComparison.Ordinal) >= 0)
                        result = val;
                });
            reclaimable = found;
            return result;
        }

        static long ParseSize(string s)
        {
            try
            {
                s = s.Trim().Replace(",", ".");
                string[] p = s.Split(' ');
                if (p.Length < 2) return 0;
                double v = double.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture);
                string unit = p[1].ToUpperInvariant();
                if (unit.StartsWith("KB")) return (long)(v * 1024);
                if (unit.StartsWith("MB")) return (long)(v * 1024 * 1024);
                if (unit.StartsWith("GB")) return (long)(v * 1024 * 1024 * 1024);
                return (long)v;
            }
            catch { return 0; }
        }
    }
}
