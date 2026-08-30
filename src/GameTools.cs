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
            g.Estimate = "0–15 %";
            g.Name = "Strømplan";
            g.What = "Balansert lar prosessoren senke klokken mellom bildene. På bærbare og på balansert oppsett er dette ofte det som merkes mest.";
            g.Cost = "Mer strøm og mer varme. På bærbar: kortere batteritid.";
            g.Gain = Gain.Varierer;
            g.NeedsAdmin = true;

            int code;
            string ut = Util.RunCapture("powercfg", "/getactivescheme", out code);
            string lav = (ut ?? "").ToLowerInvariant();
            bool bra = lav.Contains(Ultimate) || lav.Contains(HighPerf);
            g.Optimal = bra;
            g.State = bra ? "Høy ytelse" : "Balansert eller strømsparing";
            return g;
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
                        SetHklm(DeviceGuard, "EnableVirtualizationBasedSecurity", forGaming ? 0 : 1);
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
                        {
                            int code;
                            Util.RunCapture("powercfg", "/setactive " + (forGaming ? Ultimate : "381b4222-f694-41f0-9685-ff5bb260df2e"), out code);
                            if (code != 0 && forGaming)
                                Util.RunCapture("powercfg", "/setactive " + HighPerf, out code);
                            return code == 0 ? null : "powercfg svarte " + code;
                        }
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
