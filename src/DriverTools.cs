using System;
using System.Collections.Generic;
using System.Management;

namespace Brisk
{
    public class DriverUpdate
    {
        public string Title;
        public string Driver;
        public long Size;
        public bool Selected = true;
        public object Update;        // IUpdate (COM)
    }

    public class ProblemDevice
    {
        public string Name;
        public string DeviceId;
        public int ErrorCode;
        public string ErrorText;
    }

    public class GpuInfo
    {
        public string Name = "";
        public string Vendor = "";
        public string Version = "";
        public DateTime Date;
        public string Url = "";

        public int AgeDays
        {
            get { return Date == DateTime.MinValue ? -1 : (int)(DateTime.Now - Date).TotalDays; }
        }
    }

    public static class DriverTools
    {
        // Skjermkortdrivere er det Windows Update oftest ligger lengst etter på.
        // Vi viser hva som er installert, og peker på produsentens egen side.
        public static List<GpuInfo> Graphics()
        {
            List<GpuInfo> list = new List<GpuInfo>();
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(
                    "SELECT Name, DriverVersion, DriverDate, AdapterCompatibility FROM Win32_VideoController"))
                {
                    foreach (ManagementObject mo in s.Get())
                    {
                        GpuInfo g = new GpuInfo();
                        g.Name = Convert.ToString(mo["Name"]);
                        g.Version = Convert.ToString(mo["DriverVersion"]);
                        g.Vendor = Convert.ToString(mo["AdapterCompatibility"]);
                        try
                        {
                            string d = Convert.ToString(mo["DriverDate"]);
                            if (!string.IsNullOrEmpty(d))
                                g.Date = ManagementDateTimeConverter.ToDateTime(d);
                        }
                        catch { }

                        string v = (g.Vendor + " " + g.Name).ToLowerInvariant();
                        if (v.IndexOf("nvidia", StringComparison.Ordinal) >= 0)
                            g.Url = "https://www.nvidia.com/download/index.aspx";
                        else if (v.IndexOf("amd", StringComparison.Ordinal) >= 0 ||
                                 v.IndexOf("radeon", StringComparison.Ordinal) >= 0)
                            g.Url = "https://www.amd.com/en/support";
                        else if (v.IndexOf("intel", StringComparison.Ordinal) >= 0)
                            g.Url = "https://www.intel.com/content/www/us/en/download-center/home.html";

                        if (!string.IsNullOrEmpty(g.Name)) list.Add(g);
                    }
                }
            }
            catch (Exception ex) { Util.Log("Kunne ikke lese skjermkort: " + ex.Message); }
            return list;
        }

        // Microsoft Update-tjenesten. Gir tilgang til driveroppdateringer,
        // ikke bare Windows-oppdateringer.
        const string MicrosoftUpdateServiceId = "7971f918-a847-4430-9279-4a52d1efe18d";

        // ---------------------------------------------------------------
        // Enheter som Windows melder feil på — typisk manglende driver.
        public static List<ProblemDevice> FindProblemDevices()
        {
            List<ProblemDevice> list = new List<ProblemDevice>();
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(
                    "SELECT Name, DeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0"))
                {
                    foreach (ManagementObject mo in s.Get())
                    {
                        try
                        {
                            ProblemDevice d = new ProblemDevice();
                            d.Name = Convert.ToString(mo["Name"]);
                            d.DeviceId = Convert.ToString(mo["DeviceID"]);
                            d.ErrorCode = Convert.ToInt32(mo["ConfigManagerErrorCode"]);
                            d.ErrorText = ErrorText(d.ErrorCode);
                            if (string.IsNullOrEmpty(d.Name)) d.Name = L.T("(ukjent enhet)");
                            list.Add(d);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex) { Util.Log("Kunne ikke lese enhetsliste: " + ex.Message); }
            return list;
        }

        static string ErrorText(int code)
        {
            switch (code)
            {
                case 1: return L.T("Enheten er ikke riktig konfigurert");
                case 3: return L.T("Driveren kan være ødelagt, eller systemet er tomt for minne");
                case 10: return L.T("Enheten kan ikke starte");
                case 12: return L.T("Finner ikke nok ledige ressurser");
                case 14: return L.T("Krever omstart for å virke");
                case 18: return L.T("Driveren må installeres på nytt");
                case 19: return L.T("Registeret er skadet for denne enheten");
                case 21: return L.T("Windows fjerner enheten");
                case 22: return L.T("Enheten er deaktivert");
                case 24: return L.T("Enheten er ikke til stede eller virker ikke");
                case 28: return L.T("Driveren er ikke installert");
                case 31: return L.T("Windows finner ikke driver som virker");
                case 45: return L.T("Enheten er ikke koblet til nå");
                default: return L.F("Feilkode {0}", code);
            }
        }

        // ---------------------------------------------------------------
        // Registrerer Microsoft Update som kilde. Krever administrator.
        public static bool EnsureMicrosoftUpdate()
        {
            try
            {
                Type t = Type.GetTypeFromProgID("Microsoft.Update.ServiceManager");
                if (t == null) return false;
                dynamic mgr = Activator.CreateInstance(t);
                foreach (dynamic s in mgr.Services)
                {
                    try
                    {
                        if (string.Equals(Util.Str(s.ServiceID), MicrosoftUpdateServiceId,
                                StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    catch { }
                }
                // 7 = AllowPendingRegistration | AllowOnlineRegistration | RegisterServiceWithAU
                mgr.AddService2(MicrosoftUpdateServiceId, 7, "");
                Util.Log("Registrerte Microsoft Update som oppdateringskilde.");
                return true;
            }
            catch (Exception ex)
            {
                Util.Log("Kunne ikke registrere Microsoft Update: " + ex.Message);
                return false;
            }
        }

        public static List<DriverUpdate> SearchDrivers(out string note)
        {
            note = "";
            List<DriverUpdate> list = new List<DriverUpdate>();
            try
            {
                bool mu = EnsureMicrosoftUpdate();

                Type t = Type.GetTypeFromProgID("Microsoft.Update.Session");
                dynamic session = Activator.CreateInstance(t);
                session.ClientApplicationID = "Brisk";
                dynamic searcher = session.CreateUpdateSearcher();
                searcher.Online = true;

                dynamic result = null;
                if (mu)
                {
                    try
                    {
                        searcher.ServerSelection = 3;                 // ssOthers
                        searcher.ServiceID = MicrosoftUpdateServiceId;
                        result = searcher.Search("IsInstalled=0 and Type='Driver' and IsHidden=0");
                    }
                    catch (Exception ex)
                    {
                        Util.Log("Søk mot Microsoft Update feilet, prøver standardkilde: " + ex.Message);
                        result = null;
                    }
                }
                if (result == null)
                {
                    dynamic s2 = session.CreateUpdateSearcher();
                    s2.Online = true;
                    result = s2.Search("IsInstalled=0 and Type='Driver' and IsHidden=0");
                }

                foreach (dynamic u in result.Updates)
                {
                    try
                    {
                        DriverUpdate d = new DriverUpdate();
                        d.Title = Util.Str(u.Title);
                        d.Update = u;
                        try { d.Size = Convert.ToInt64(u.MaxDownloadSize); } catch { }
                        try
                        {
                            string man = Util.Str(u.DriverManufacturer);
                            string cls = Util.Str(u.DriverClass);
                            d.Driver = (man + " " + cls).Trim();
                        }
                        catch { d.Driver = ""; }
                        list.Add(d);
                    }
                    catch { }
                }

                if (list.Count == 0)
                    note = L.T("Ingen nye drivere fra Windows Update.");
            }
            catch (Exception ex)
            {
                note = L.T("Driversøket feilet: ") + ex.Message;
                Util.Log(note);
            }
            return list;
        }

        // Returnerer antall installerte og setter rebootRequired.
        public static int InstallDrivers(List<DriverUpdate> chosen, out bool rebootRequired,
            Action<string> progress)
        {
            rebootRequired = false;
            if (chosen == null || chosen.Count == 0) return 0;
            try
            {
                Type ts = Type.GetTypeFromProgID("Microsoft.Update.Session");
                dynamic session = Activator.CreateInstance(ts);
                session.ClientApplicationID = "Brisk";

                Type tc = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl");
                dynamic coll = Activator.CreateInstance(tc);
                foreach (DriverUpdate d in chosen)
                {
                    dynamic u = d.Update;
                    try
                    {
                        if (!(bool)u.EulaAccepted) u.AcceptEula();
                    }
                    catch { }
                    coll.Add(u);
                }

                if (progress != null) progress(L.F("Laster ned {0} driver(e).", coll.Count));
                dynamic dl = session.CreateUpdateDownloader();
                dl.Updates = coll;
                dynamic dres = dl.Download();
                if (progress != null) progress(L.T("Nedlasting ferdig."));

                Type tc2 = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl");
                dynamic ready = Activator.CreateInstance(tc2);
                foreach (dynamic u in coll)
                {
                    try { if ((bool)u.IsDownloaded) ready.Add(u); }
                    catch { }
                }
                if (ready.Count == 0)
                {
                    if (progress != null) progress(L.T("Ingen drivere ble lastet ned."));
                    return 0;
                }

                if (progress != null) progress(L.T("Installerer."));
                dynamic inst = session.CreateUpdateInstaller();
                inst.Updates = ready;
                dynamic ires = inst.Install();
                rebootRequired = (bool)ires.RebootRequired;
                int ok = 0;
                for (int i = 0; i < ready.Count; i++)
                {
                    try
                    {
                        int rc = Convert.ToInt32(ires.GetUpdateResult(i).ResultCode);
                        if (rc == 2 || rc == 3) ok++;      // 2 = vellykket, 3 = vellykket med feil
                    }
                    catch { }
                }
                Util.Log("Installerte " + ok + " driver(e). Omstart nødvendig: " + rebootRequired);
                return ok;
            }
            catch (Exception ex)
            {
                if (progress != null) progress(L.T("Installasjon feilet: ") + ex.Message);
                Util.Log("Driverinstallasjon feilet: " + ex.Message);
                return 0;
            }
        }
    }
}
