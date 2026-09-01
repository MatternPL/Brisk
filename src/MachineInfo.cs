using System;
using System.Collections.Generic;
using System.Management;

namespace Brisk
{
    // Kort oppsummering av maskinen til forsida. Alt hentes i bakgrunnen,
    // for WMI-oppslagene tar noen hundre millisekunder hver og skal ikke
    // holde vinduet igjen mens det aapnes.
    public class MachineLine
    {
        public string Label = "";
        public string Value = "";
        public MachineLine(string l, string v) { Label = l; Value = v; }
    }

    public static class MachineInfo
    {
        public static List<MachineLine> Read()
        {
            List<MachineLine> l = new List<MachineLine>();

            // Minne og oppetid staar som egne kort paa forsida naa, og skal
            // ikke staa to steder. Her hoerer det som ikke endrer seg hjemme:
            // hva slags maskin dette er.
            l.Add(new MachineLine("Windows", Windows()));
            l.Add(new MachineLine("Prosessor", Trim(Wmi("Win32_Processor", "Name"))));
            l.Add(new MachineLine("Skjermkort", Trim(Wmi("Win32_VideoController", "Name"))));
            l.Add(new MachineLine("Hovedkort", Board()));
            l.Add(new MachineLine("Minne totalt", Total()));
            return l;
        }

        static string Windows()
        {
            string caption = Wmi("Win32_OperatingSystem", "Caption");
            caption = caption.Replace("Microsoft ", "").Trim();
            string build = Wmi("Win32_OperatingSystem", "BuildNumber");
            if (build.Length > 0 && build != "(ukjent)") caption += "  ·  build " + build;
            return caption;
        }

        // Bare totalen. Hvor mye som er i bruk staar paa minnekortet paa
        // forsida, og det tallet endrer seg mens man ser paa det.
        static string Total()
        {
            try { return Util.Bytes(MemoryTools.Snapshot().TotalPhys); }
            catch (Exception) { return "(ukjent)"; }
        }

        static string Board()
        {
            string m = Wmi("Win32_BaseBoard", "Manufacturer");
            string p = Wmi("Win32_BaseBoard", "Product");
            string s = (m + " " + p).Trim();
            return s.Length > 1 ? s : "(ukjent)";
        }

        // Windows lagrer oppstartstidspunktet som en WMI-dato. Vi regner ut
        // hvor lenge maskinen har vaert paa, siden det ofte forklarer hvorfor
        // minnebruken har krope oppover.
        // Naar maskinen sist ble startet. Brukes til aa avgjore om en endring
        // som krever omstart faktisk har faatt sin omstart.
        public static DateTime LastBoot()
        {
            try
            {
                string raw = Wmi("Win32_OperatingSystem", "LastBootUpTime");
                if (raw.Length >= 14) return ManagementDateTimeConverter.ToDateTime(raw);
            }
            catch (Exception) { }
            return DateTime.MinValue;
        }

        public static string Uptime()
        {
            try
            {
                string raw = Wmi("Win32_OperatingSystem", "LastBootUpTime");
                if (raw.Length < 14) return "(ukjent)";
                DateTime boot = ManagementDateTimeConverter.ToDateTime(raw);
                TimeSpan t = DateTime.Now - boot;
                if (t.TotalDays >= 1)
                    return L.F("{0} d", (int)t.TotalDays) + " " + L.F("{0} t", t.Hours);
                if (t.TotalHours >= 1)
                    return L.F("{0} t", (int)t.TotalHours) + " " + L.F("{0} min", t.Minutes);
                return L.F("{0} min", (int)t.TotalMinutes);
            }
            catch (Exception) { return "(ukjent)"; }
        }

        static string Trim(string s)
        {
            if (string.IsNullOrEmpty(s)) return "(ukjent)";
            s = s.Replace("(R)", "").Replace("(TM)", "").Replace("(tm)", "");
            while (s.IndexOf("  ") >= 0) s = s.Replace("  ", " ");
            return s.Trim();
        }

        static string Wmi(string cls, string prop)
        {
            try
            {
                using (ManagementObjectSearcher s =
                    new ManagementObjectSearcher("SELECT " + prop + " FROM " + cls))
                    foreach (ManagementObject mo in s.Get())
                    {
                        string v = Convert.ToString(mo[prop]);
                        if (!string.IsNullOrEmpty(v)) return v.Trim();
                    }
            }
            catch (Exception) { }
            return "";
        }
    }
}
