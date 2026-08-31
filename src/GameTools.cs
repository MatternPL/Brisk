using System;
using System.Collections.Generic;
using System.Management;
using Microsoft.Win32;

namespace Brisk
{
    // ==================================================================
    //  SPILLMODUS
    // ==================================================================
    // Bare ting som faktisk kan maales i bilder per sekund. Ingen
    // «SystemResponsiveness», ingen «unpark cores», ingen timeroopplosning -
    // det er placebo, og placebo hoerer ikke hjemme i dette programmet.
    //
    // Hver post sier hva den gir OG hva den koster. Noen av dem senker
    // sikkerheten reelt, og det skal staa, ikke gjemmes bak ordet «gaming».

    public enum Gain { Stor, Liten, Varierer }

    public class GameSetting
    {
        public string Key = "";          // intern nokkel
        public string Name = "";         // vises som overskrift
        public string What = "";         // hva innstillingen er
        public string Cost = "";         // hva du gir fra deg. Tom = ingenting
        public Gain Gain = Gain.Liten;
        // Typisk maalt spenn, ikke et loefte. Hva du faar avhenger av spillet
        // og av om det er prosessoren eller skjermkortet som er flaskehalsen.
        public string Estimate = "";
        public bool NeedsReboot;
        public bool NeedsAdmin;

        public bool Available = true;    // finnes den paa denne maskinen
        public string Unavailable = "";  // hvorfor ikke
        public bool Optimal;             // staar den allerede slik spill liker
        public string State = "";        // klartekst om hva den staar paa naa

        // Registeret er endret, men Windows kjorer fortsatt paa det gamle til
        // maskinen startes paa nytt. Uten dette ville kortet sagt "Kjorer" rett
        // etter at du slo den av, og sett ut som om ingenting skjedde.
        public bool PendingReboot;
    }

    public static class GameTools
    {
        const string DeviceGuard = @"SYSTEM\CurrentControlSet\Control\DeviceGuard";
        const string Hvci = DeviceGuard + @"\Scenarios\HypervisorEnforcedCodeIntegrity";
        const string Lsa = @"SYSTEM\CurrentControlSet\Control\Lsa";
        const string GfxDrivers = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
        const string GameConfig = @"System\GameConfigStore";
        const string GameDvr = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";
        const string GameBar = @"Software\Microsoft\GameBar";

        // Ultimate Performance finnes ikke overalt; High performance gjor.
        const string HighPerf = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        const string Ultimate = "e9a42b02-d5df-448d-aa00-03f14749eb61";

        public static List<GameSetting> Read()
        {
            List<GameSetting> l = new List<GameSetting>();
            l.Add(ReadVbs());
            l.Add(ReadHvci());
            l.Add(ReadGameDvr());
            l.Add(ReadGameBar());
            l.Add(ReadHags());
            l.Add(ReadPowerPlan());
            return l;
        }

        // ---------------------------------------------------------------
        static GameSetting ReadVbs()
        {
            GameSetting g = new GameSetting();
            g.Key = "vbs";
            g.Estimate = "5–15 %";
            g.Name = "Virtualiseringsbasert sikkerhet";
            g.What = "Kjører deler av Windows i en virtuell maskin. Koster ytelse i alle spill fordi alt går gjennom hypervisoren.";
            g.Cost = "Senker sikkerheten reelt. Credential Guard beskytter innloggingene dine mot tyveri.";
            g.Gain = Gain.Stor;
            g.NeedsReboot = true;
            g.NeedsAdmin = true;

            int status = -1;
            try
            {
                ManagementScope sc = new ManagementScope(@"\\.\root\Microsoft\Windows\DeviceGuard");
                sc.Connect();
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(sc,
                    new ObjectQuery("SELECT * FROM Win32_DeviceGuard")))
                    foreach (ManagementObject mo in s.Get())
                    {
                        status = Convert.ToInt32(Opt(mo, "VirtualizationBasedSecurityStatus", -1));
                        break;
                    }
            }
            catch (Exception) { }

            if (status < 0)
            {
                g.Available = false;
                g.Unavailable = "Windows melder ikke om denne på denne maskinen.";
                return g;
            }

            // Windows kjorer VBS til maskinen startes paa nytt, ogsaa etter at
            // registeret er satt til av. Da er ikke innstillingen "paa" - den
            // venter paa omstart, og det skal staa.
            object reg = HklmValue(DeviceGuard, "EnableVirtualizationBasedSecurity");
            bool slaattAv = reg != null && Convert.ToInt32(reg) == 0;

            if (status == 2 && slaattAv)
            {
                g.PendingReboot = true;
                g.Optimal = true;
                g.State = "Slått av — venter på omstart";
                return g;
            }

            g.Optimal = status != 2;
            g.State = status == 2 ? "Kjører" : status == 1 ? "På, men kjører ikke" : "Av";
            return g;
        }

        static GameSetting ReadHvci()
        {
            GameSetting g = new GameSetting();
            g.Key = "hvci";
            g.Estimate = "3–10 %";
            g.Name = "Minneintegritet";
            g.What = "Kjernekode kontrolleres av hypervisoren. Merkes mest i spill som belaster prosessoren.";
            g.Cost = "Senker sikkerheten. Beskytter mot drivere som prøver å kjøre kode i kjernen.";
            g.Gain = Gain.Stor;
            g.NeedsReboot = true;
            g.NeedsAdmin = true;

            object v = HklmValue(Hvci, "Enabled");
            if (v == null)
            {
                g.Available = true;
                g.Optimal = true;
                g.State = "Av";
                return g;
            }
            g.Optimal = Convert.ToInt32(v) == 0;
            g.State = g.Optimal ? "Av" : "På";
            return g;
        }

        static GameSetting ReadGameDvr()
        {
            GameSetting g = new GameSetting();
            g.Key = "gamedvr";
            g.Estimate = "1–5 %";
            g.Name = "Bakgrunnsopptak";
            g.What = "Windows spiller inn spillet ditt hele tiden i tilfelle du vil lagre de siste minuttene. Det koster bilder selv når du aldri lagrer noe.";
            g.Cost = "Du mister «ta opp de siste 30 sekundene» i Game Bar.";
            g.Gain = Gain.Liten;
            g.NeedsAdmin = false;

            object a = HkcuValue(GameConfig, "GameDVR_Enabled");
            object b = HkcuValue(GameDvr, "AppCaptureEnabled");
            bool paa = (a == null || Convert.ToInt32(a) != 0) || (b == null || Convert.ToInt32(b) != 0);
            g.Optimal = !paa;
            g.State = paa ? "På" : "Av";
            return g;
        }

        static GameSetting ReadGameBar()
        {
            GameSetting g = new GameSetting();
            g.Key = "gamebar";
            g.Estimate = "0–2 %";
            g.Name = "Game Bar";
            g.What = "Overlegget som åpnes med Windows+G. Ligger og lytter etter tastetrykk mens du spiller.";
            g.Cost = "Snarveiene for opptak og skjermbilde slutter å virke.";
            g.Gain = Gain.Liten;
            g.NeedsAdmin = false;

            object v = HkcuValue(GameBar, "UseNexusForGameBarEnabled");
            bool paa = v == null || Convert.ToInt32(v) != 0;
            g.Optimal = !paa;
            g.State = paa ? "På" : "Av";
            return g;
        }

        static GameSetting ReadHags()
        {
            GameSetting g = new GameSetting();
            g.Key = "hags";
            g.Estimate = "−5 til +5 %";
            g.Name = "Maskinvareakselerert GPU-planlegging";
            g.What = "Lar skjermkortet styre sin egen minneplanlegging i stedet for Windows.";
            g.Cost = "Ingenting, men effekten varierer. Noen spill vinner, noen taper. Enkelte eldre drivere blir ustabile.";
            g.Gain = Gain.Varierer;
            g.NeedsReboot = true;
            g.NeedsAdmin = true;

            object v = HklmValue(GfxDrivers, "HwSchMode");
            if (v == null)
            {
                g.Available = false;
                g.Unavailable = "Skjermkortet eller driveren støtter ikke dette.";
                return g;
            }
            int m = Convert.ToInt32(v);
            g.Optimal = m == 2;
            g.State = m == 2 ? "På" : "Av";
            return g;
        }

        static GameSetting ReadPowerPlan()
        {
            GameSetting g = new GameSetting();
            g.Key = "power";
            g.Estimate = "0\u201315 %";
            g.Name = "Str\u00f8mplan";
            g.What = "Balansert lar prosessoren senke klokken mellom bildene. P\u00e5 b\u00e6rbare og p\u00e5 balansert oppsett er dette ofte det som merkes mest.";
            g.Cost = "Mer str\u00f8m og mer varme. P\u00e5 b\u00e6rbar: kortere batteritid.";
            g.Gain = Gain.Varierer;
            g.NeedsAdmin = true;

            string aktiv, navn;
            List<Plan> planer = Planer(out aktiv, out navn);
            if (planer.Count == 0)
            {
                g.Available = false;
                g.Unavailable = "Fikk ikke lest str\u00f8mplanene.";
                return g;
            }

            g.Optimal = ErRask(navn, aktiv);
            g.State = g.Optimal ? "H\u00f8y ytelse" : "Balansert eller str\u00f8msparing";
            return g;
        }

        class Plan
        {
            public string Guid = "";
            public string Name = "";
            public bool Active;
        }

        // Leser de faktiske planene i stedet for aa stole paa faste GUID-er.
        // Windows lager en ny GUID hver gang en plan dupliseres, saa Ultimate
        // Performance kan ligge under et helt annet nummer enn det kjente - og
        // paa mange maskiner, saerlig baerbare, finnes den ikke i det hele tatt.
        static List<Plan> Planer(out string aktivGuid, out string aktivNavn)
        {
            List<Plan> l = new List<Plan>();
            aktivGuid = "";
            aktivNavn = "";
            try
            {
                int code;
                string ut = Util.RunCapture("powercfg", "/list", out code);
                foreach (string raw in (ut ?? "").Replace("\r", "").Split('\n'))
                {
                    string linje = raw.Trim();
                    int i = linje.IndexOf("GUID:", StringComparison.OrdinalIgnoreCase);
                    if (i < 0) continue;
                    string rest = linje.Substring(i + 5).Trim();
                    int sp = rest.IndexOf(' ');
                    if (sp <= 0) continue;

                    Plan pl = new Plan();
                    pl.Guid = rest.Substring(0, sp).Trim();
                    string etter = rest.Substring(sp).Trim();
                    int a = etter.IndexOf('('), b = etter.LastIndexOf(')');
                    pl.Name = a >= 0 && b > a ? etter.Substring(a + 1, b - a - 1).Trim() : etter;
                    pl.Active = etter.EndsWith("*");
                    l.Add(pl);
                    if (pl.Active) { aktivGuid = pl.Guid; aktivNavn = pl.Name; }
                }
            }
            catch (Exception) { }
            return l;
        }

        // Navnet er oversatt til systemspraaket, saa vi ser bade paa navn og
        // paa de to GUID-ene Microsoft selv bruker.
        static bool ErRask(string navn, string guid)
        {
            string n = (navn ?? "").ToLowerInvariant();
            if (n.IndexOf("ultimate") >= 0) return true;
            if (n.IndexOf("high perf") >= 0) return true;
            if (n.IndexOf("h\u00f8y ytelse") >= 0) return true;
            if (n.IndexOf("h\u00f6gpresta") >= 0) return true;
            string gg = (guid ?? "").ToLowerInvariant();
            return gg == Ultimate || gg == HighPerf;
        }

        // ---------------------------------------------------------------
        // Setter innstillingen slik spill liker den, eller tilbake igjen.
        // Returnerer null ved suksess, ellers en feiltekst.
        public static string Apply(string key, bool forGaming)
        {
            try
            {
                switch (key)
                {
                    case "vbs":
                        // Alle tre maa settes. Bare EnableVirtualizationBasedSecurity
                        // er ikke nok naar Credential Guard er slaatt paa av Windows
                        // selv, slik den er som standard paa Enterprise.
                        SetHklm(DeviceGuard, "EnableVirtualizationBasedSecurity", forGaming ? 0 : 1);
                        SetHklm(DeviceGuard, "RequirePlatformSecurityFeatures", forGaming ? 0 : 1);
                        SetHklm(DeviceGuard + @"\Scenarios\CredentialGuard", "Enabled", forGaming ? 0 : 1);
                        SetHklm(Lsa, "LsaCfgFlags", forGaming ? 0 : 1);
                        return null;

                    case "hvci":
                        SetHklm(Hvci, "Enabled", forGaming ? 0 : 1);
                        return null;

                    case "gamedvr":
                        SetHkcu(GameConfig, "GameDVR_Enabled", forGaming ? 0 : 1);
                        SetHkcu(GameDvr, "AppCaptureEnabled", forGaming ? 0 : 1);
                        return null;

                    case "gamebar":
                        SetHkcu(GameBar, "UseNexusForGameBarEnabled", forGaming ? 0 : 1);
                        return null;

                    case "hags":
                        SetHklm(GfxDrivers, "HwSchMode", forGaming ? 2 : 1);
                        return null;

                    case "power":
                        return SetPowerPlan(forGaming);
                }
                return "Ukjent innstilling.";
            }
            catch (UnauthorizedAccessException)
            {
                return "Krever administrator.";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // Velger en plan som finnes, i stedet for aa sette en fast GUID som
        // kanskje ikke er der. Finnes ingen rask plan, lages Ultimate
        // Performance ved aa duplisere Microsofts egen - det virker paa
        // stasjonaere maskiner, men ikke alltid paa baerbare.
        static string SetPowerPlan(bool forGaming)
        {
            int code;
            string aktiv, navn;
            List<Plan> planer = Planer(out aktiv, out navn);
            if (planer.Count == 0) return "Fikk ikke lest strømplanene.";

            string mål = "";
            if (forGaming)
            {
                foreach (Plan pl in planer)
                    if (pl.Name.ToLowerInvariant().IndexOf("ultimate") >= 0) { mål = pl.Guid; break; }
                if (mål.Length == 0)
                    foreach (Plan pl in planer)
                        if (ErRask(pl.Name, pl.Guid)) { mål = pl.Guid; break; }

                if (mål.Length == 0)
                {
                    // Ingen rask plan finnes. Lag Ultimate Performance.
                    string ut = Util.RunCapture("powercfg", "-duplicatescheme " + Ultimate, out code);
                    if (code == 0)
                    {
                        planer = Planer(out aktiv, out navn);
                        foreach (Plan pl in planer)
                            if (pl.Name.ToLowerInvariant().IndexOf("ultimate") >= 0) { mål = pl.Guid; break; }
                    }
                }
                if (mål.Length == 0) mål = HighPerf;
            }
            else
            {
                mål = "381b4222-f694-41f0-9685-ff5bb260df2e";   // Balansert
            }

            Util.RunCapture("powercfg", "/setactive " + mål, out code);
            if (code != 0) return "powercfg svarte " + code;

            // Kontroller at det faktisk ble satt.
            string a2, n2;
            Planer(out a2, out n2);
            if (!string.Equals(a2, mål, StringComparison.OrdinalIgnoreCase))
                return "Planen lot seg ikke aktivere.";

            Util.Log("Strømplan satt til " + n2 + " (" + a2 + ")");
            return null;
        }

        // ---------------------------------------------------------------
        static object HklmValue(string path, string name)
        {
            try
            {
                using (RegistryKey k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                                                 .OpenSubKey(path))
                    return k == null ? null : k.GetValue(name);
            }
            catch (Exception) { return null; }
        }

        static object HkcuValue(string path, string name)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(path))
                    return k == null ? null : k.GetValue(name);
            }
            catch (Exception) { return null; }
        }

        static void SetHklm(string path, string name, int value)
        {
            using (RegistryKey k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                                              .CreateSubKey(path))
            {
                if (k == null) throw new Exception("Fikk ikke åpnet " + path);
                k.SetValue(name, value, RegistryValueKind.DWord);
            }
        }

        static void SetHkcu(string path, string name, int value)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(path))
            {
                if (k == null) throw new Exception("Fikk ikke åpnet " + path);
                k.SetValue(name, value, RegistryValueKind.DWord);
            }
        }

        static object Opt(ManagementObject mo, string prop, object fallback)
        {
            try { object v = mo[prop]; return v ?? fallback; }
            catch (Exception) { return fallback; }
        }
    }
}
