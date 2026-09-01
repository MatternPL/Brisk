using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;

namespace Brisk
{
    public class ScreenMode
    {
        public string Device = "";       // \\.\DISPLAY1
        public string Name = "";         // navnet driveren oppgir
        public int Width, Height;
        public int Hz;                   // det som staar naa
        public int MaxHz;                // hoyeste ved samme opplosning
        public bool Primary;

        // Fra skjermens egen EDID, ikke fra driveren.
        public string Model = "";        // "Odyssey G93SC"
        public string HardwareId = "";   // "SAM7412", kobler skjerm mot WMI
        public double Inches;            // diagonal, regnet fra fysisk storrelse
        public int Year;

        public bool AtMax { get { return MaxHz > 0 && Hz >= MaxHz; } }
    }

    // ------------------------------------------------------------------
    //  Skjerm
    //
    //  To ting: oppdateringsfrekvens og farge.
    //
    //  Frekvensen er den vanligste tapte ytelsen som finnes. En skjerm
    //  kjopt for 144 eller 240 Hz staar rett som det er paa 60 fordi
    //  Windows valgte det ved forste oppstart, eller fordi en kabel ble
    //  byttet. Det koster ingenting aa rette, og forskjellen ser man med
    //  en gang.
    //
    //  Fargen gjores i to lag. Gammakurven finnes paa alle skjermkort og
    //  styrer kontrast og svartnivaa. Metning - det NVIDIA kaller Digital
    //  Vibrance - finnes bare gjennom deres eget bibliotek, og brukes hvis
    //  det er der. Begge deler lagres for de endres, saa Tilbakestill
    //  faktisk setter tilbake det du hadde.
    // ------------------------------------------------------------------
    public static class ScreenTools
    {
        // ---------------------------------------------------------------
        //  Oppdateringsfrekvens
        // ---------------------------------------------------------------
        public static List<ScreenMode> Displays()
        {
            List<ScreenMode> l = new List<ScreenMode>();
            try
            {
                for (uint i = 0; ; i++)
                {
                    DISPLAY_DEVICE dd = new DISPLAY_DEVICE();
                    dd.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));
                    if (!EnumDisplayDevices(null, i, ref dd, 0)) break;
                    if ((dd.StateFlags & 1) == 0) continue;      // ikke tilkoblet skrivebordet

                    DEVMODE naa = new DEVMODE();
                    naa.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
                    if (!EnumDisplaySettings(dd.DeviceName, -1, ref naa)) continue;

                    ScreenMode m = new ScreenMode();
                    m.Device = dd.DeviceName;
                    m.Name = dd.DeviceString == null ? "" : dd.DeviceString.Trim();
                    m.Width = (int)naa.dmPelsWidth;
                    m.Height = (int)naa.dmPelsHeight;
                    m.Hz = (int)naa.dmDisplayFrequency;
                    m.Primary = (dd.StateFlags & 4) != 0;

                    // Hoyeste frekvens ved samme opplosning. Aa bytte
                    // opplosning for aa vinne Hz er ikke vaar avgjorelse.
                    for (int n = 0; ; n++)
                    {
                        DEVMODE d = new DEVMODE();
                        d.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
                        if (!EnumDisplaySettings(dd.DeviceName, n, ref d)) break;
                        if (d.dmPelsWidth == naa.dmPelsWidth &&
                            d.dmPelsHeight == naa.dmPelsHeight &&
                            d.dmBitsPerPel == naa.dmBitsPerPel &&
                            (int)d.dmDisplayFrequency > m.MaxHz)
                            m.MaxHz = (int)d.dmDisplayFrequency;
                    }
                    // Skjermens eget navn ligger i EDID, ikke hos driveren.
                    // Den andre EnumDisplayDevices-runden gir maskinvare-id-en
                    // (f.eks. SAM7412), som ogsaa staar i WMI sin InstanceName.
                    // Uten den koblingen kan ikke to skjermer skilles fra
                    // hverandre - rekkefolgen er ikke garantert lik.
                    DISPLAY_DEVICE mon = new DISPLAY_DEVICE();
                    mon.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));
                    if (EnumDisplayDevices(dd.DeviceName, 0, ref mon, 0))
                        m.HardwareId = HardwareId(mon.DeviceID);

                    l.Add(m);
                }
                Beskriv(l);
            }
            catch (Exception ex) { Util.Log("Kunne ikke lese skjermer: " + ex.Message); }
            return l;
        }

        // "MONITOR\SAM7412\{...}\0001" -> "SAM7412"
        static string HardwareId(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return "";
            string[] d = deviceId.Split('\\');
            return d.Length > 1 ? d[1] : "";
        }

        static void Beskriv(List<ScreenMode> skjermer)
        {
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(
                    new ManagementScope(@"\\.\root\wmi"), new ObjectQuery("SELECT * FROM WmiMonitorID")))
                foreach (ManagementObject mo in s.Get())
                {
                    string inst = Convert.ToString(mo["InstanceName"]);
                    ScreenMode treff = null;
                    foreach (ScreenMode m in skjermer)
                        if (m.HardwareId.Length > 0 && inst != null &&
                            inst.IndexOf(m.HardwareId, StringComparison.OrdinalIgnoreCase) >= 0)
                        { treff = m; break; }
                    if (treff == null) continue;

                    treff.Model = FraTegn(mo["UserFriendlyName"]);
                    try { treff.Year = Convert.ToInt32(mo["YearOfManufacture"]); }
                    catch (Exception) { }
                }
            }
            catch (Exception ex) { Util.Log("Kunne ikke lese skjermnavn: " + ex.Message); }

            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(
                    new ManagementScope(@"\\.\root\wmi"),
                    new ObjectQuery("SELECT * FROM WmiMonitorBasicDisplayParams")))
                foreach (ManagementObject mo in s.Get())
                {
                    string inst = Convert.ToString(mo["InstanceName"]);
                    foreach (ScreenMode m in skjermer)
                    {
                        if (m.HardwareId.Length == 0 || inst == null ||
                            inst.IndexOf(m.HardwareId, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        double bredde = Convert.ToDouble(mo["MaxHorizontalImageSize"]);
                        double hoyde = Convert.ToDouble(mo["MaxVerticalImageSize"]);
                        // Oppgitt i hele centimeter, saa dette er en avrunding,
                        // ikke en presis maalt diagonal.
                        m.Inches = Math.Sqrt(bredde * bredde + hoyde * hoyde) / 2.54;
                        break;
                    }
                }
            }
            catch (Exception ex) { Util.Log("Kunne ikke lese skjermstørrelse: " + ex.Message); }
        }

        // WMI gir navnene som tegnkoder med nuller paa slutten.
        static string FraTegn(object o)
        {
            ushort[] a = o as ushort[];
            if (a == null) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (ushort u in a) if (u != 0) sb.Append((char)u);
            return sb.ToString().Trim();
        }

        // Returnerer null naar det gikk bra, ellers en forklaring.
        public static string SetHz(ScreenMode m, int hz)
        {
            if (m == null) return L.T("Ingen skjerm valgt.");
            try
            {
                DEVMODE d = new DEVMODE();
                d.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
                if (!EnumDisplaySettings(m.Device, -1, ref d))
                    return L.T("Fikk ikke lest gjeldende oppsett.");

                d.dmDisplayFrequency = (uint)hz;
                d.dmFields = 0x400000;      // DM_DISPLAYFREQUENCY

                // Prov forst uten aa lagre, saa vi ikke skriver et oppsett
                // skjermen ikke klarer.
                int test = ChangeDisplaySettingsEx(m.Device, ref d, IntPtr.Zero, 0x02 /* CDS_TEST */, IntPtr.Zero);
                if (test != 0) return Svar(test);

                int r = ChangeDisplaySettingsEx(m.Device, ref d, IntPtr.Zero, 0x01 /* CDS_UPDATEREGISTRY */, IntPtr.Zero);
                if (r != 0) return Svar(r);

                Util.Log("Skjerm " + m.Device + " satt til " + hz + " Hz.");
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        static string Svar(int kode)
        {
            switch (kode)
            {
                case 0: return null;
                case -2: return L.T("Skjermen støtter ikke denne innstillingen.");
                case -1: return L.T("Endringen krever omstart.");
                case -4: return L.T("Windows avviste endringen.");
                case -5: return L.T("Dette krever administrator.");
                default: return L.F("Windows svarte med kode {0}.", kode);
            }
        }

        // ---------------------------------------------------------------
        //  Farge: gammakurve. Virker paa alle skjermkort.
        // ---------------------------------------------------------------
        // Maalt: GetDC(null) gir ikke lesetilgang til gammakurven paa denne
        // maskinen, men CreateDC mot den enkelte skjermen gjor det. Derfor
        // gaar alt per skjerm - som ogsaa er riktigere, siden to skjermer kan
        // trenge hver sin kurve.
        const string GammaKey = "SkjermGamma";     // originalkurvene, base64
        const string VibKey = "SkjermMetning";     // opprinnelig metningsnivaa
        const string BruktKey = "SkjermBrukt";     // hva vi faktisk satte: gamma;kontrast

        // Hva profilen som staar naa faktisk endret. Statuslinja sa lenge bare
        // «metning 20», som fikk det til aa hoeres ut som om metningen var det
        // eneste - gammakurven ble endret samtidig.
        public static bool AppliedCurve(out double gamma, out double kontrast)
        {
            gamma = 0; kontrast = 0;
            string s = Util.Setting(BruktKey);
            if (s == null) return false;
            string[] d = s.Split(';');
            return d.Length == 2
                && double.TryParse(d[0], System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out gamma)
                && double.TryParse(d[1], System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out kontrast);
        }

        public static bool ColourChanged
        {
            get
            {
                string s = Util.Setting(GammaKey);
                return s != null && s.Length > 0;
            }
        }

        // Leser og lagrer kurvene slik de er naa - én gang. Uten dette ville
        // «Tilbakestill» vaert en gjetning om hva som var normalt, og en
        // kalibrert skjerm ville mistet kalibreringen sin.
        static void LagreOriginal()
        {
            if (ColourChanged) return;                 // alt lagret fra for
            List<string> deler = new List<string>();
            foreach (ScreenMode m in Displays())
            {
                ushort[] r = LesRamp(m.Device);
                if (r == null) continue;
                byte[] b = new byte[r.Length * 2];
                Buffer.BlockCopy(r, 0, b, 0, b.Length);
                deler.Add(m.Device + "=" + Convert.ToBase64String(b));
            }
            if (deler.Count > 0)
                Util.SetSetting(GammaKey, string.Join("|", deler.ToArray()));

            int v = Vibrance();
            if (v >= 0) Util.SetSetting(VibKey, v.ToString());
        }

        static ushort[] LesRamp(string device)
        {
            IntPtr dc = CreateDC(device, device, null, IntPtr.Zero);
            if (dc == IntPtr.Zero) return null;
            try
            {
                ushort[] r = new ushort[256 * 3];
                return GetDeviceGammaRamp(dc, r) ? r : null;
            }
            catch (Exception) { return null; }
            finally { DeleteDC(dc); }
        }

        static bool SkrivRamp(string device, ushort[] ramp)
        {
            IntPtr dc = CreateDC(device, device, null, IntPtr.Zero);
            if (dc == IntPtr.Zero) return false;
            try { return SetDeviceGammaRamp(dc, ramp); }
            catch (Exception) { return false; }
            finally { DeleteDC(dc); }
        }

        // gamma < 1 gir dypere svart, kontrast > 1 gir mer sprang.
        public static string ApplyColour(double gamma, double kontrast, int metning)
        {
            LagreOriginal();

            ushort[] ramp = new ushort[256 * 3];
            for (int i = 0; i < 256; i++)
            {
                double v = i / 255.0;
                v = Math.Pow(v, gamma);
                v = (v - 0.5) * kontrast + 0.5;
                if (v < 0) v = 0; else if (v > 1) v = 1;
                ushort u = (ushort)Math.Round(v * 65535.0);
                ramp[i] = u; ramp[256 + i] = u; ramp[512 + i] = u;
            }

            int ok = 0, alle = 0;
            foreach (ScreenMode m in Displays())
            {
                alle++;
                if (SkrivRamp(m.Device, ramp)) ok++;
            }
            if (metning >= 0) SetVibrance(metning);

            if (ok == 0)
                return L.T("Windows tok ikke imot fargekurven. Noen skjermkort tillater den ikke.");

            Util.SetSetting(BruktKey,
                gamma.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";" +
                kontrast.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Util.Log("Fargeprofil satt paa " + ok + " av " + alle + " skjermer: gamma " +
                     gamma + ", kontrast " + kontrast + ", metning " + metning);
            return null;
        }

        public static string ResetColour()
        {
            string lagret = Util.Setting(GammaKey);
            int ok = 0, alle = 0;

            // Den rette linja er det Windows selv bruker naar ingenting har
            // rort kurven. Den brukes bare hvis vi ikke har originalen.
            ushort[] rett = new ushort[256 * 3];
            for (int i = 0; i < 256; i++)
            {
                ushort u = (ushort)(i * 257);
                rett[i] = u; rett[256 + i] = u; rett[512 + i] = u;
            }

            foreach (ScreenMode m in Displays())
            {
                alle++;
                ushort[] ramp = rett;
                if (lagret != null)
                {
                    foreach (string del in lagret.Split('|'))
                    {
                        int lik = del.IndexOf('=');
                        if (lik <= 0 || del.Substring(0, lik) != m.Device) continue;
                        try
                        {
                            byte[] b = Convert.FromBase64String(del.Substring(lik + 1));
                            ushort[] r = new ushort[b.Length / 2];
                            Buffer.BlockCopy(b, 0, r, 0, b.Length);
                            if (r.Length == 256 * 3) ramp = r;
                        }
                        catch (Exception) { }
                        break;
                    }
                }
                if (SkrivRamp(m.Device, ramp)) ok++;
            }

            string v = Util.Setting(VibKey);
            int niva;
            SetVibrance(v != null && int.TryParse(v, out niva) ? niva : 0);

            Util.SetSetting(GammaKey, "");
            Util.SetSetting(VibKey, "");
            Util.SetSetting(BruktKey, "");

            if (ok == 0)
                return L.T("Windows tok ikke imot fargekurven. Noen skjermkort tillater den ikke.");
            Util.Log("Farge tilbakestilt paa " + ok + " av " + alle + " skjermer.");
            return null;
        }

        // ---------------------------------------------------------------
        //  Farge: metning gjennom NVIDIA sitt eget bibliotek
        // ---------------------------------------------------------------
        //  NVAPI har ingen vanlige eksporter. Alt gaar gjennom
        //  nvapi_QueryInterface, som gir en funksjonspeker for en fast id.
        //  Er kortet fra AMD eller Intel finnes ikke dll-en, og da hoppes
        //  hele denne delen over - gammakurven virker uansett.
        public static bool HasVibrance { get { return NvInit(); } }

        static bool nvProvd, nvOk;
        static IntPtr nvDisplay;
        static SetDvcDel nvSet;
        static GetDvcDel nvGet;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int InitDel();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int EnumDel(int i, ref IntPtr h);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int SetDvcDel(IntPtr h, IntPtr o, int lvl);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int GetDvcDel(IntPtr h, IntPtr o, ref DvcInfo i);

        [StructLayout(LayoutKind.Sequential)]
        struct DvcInfo { public uint version, currentLevel, minLevel, maxLevel; }

        static bool NvInit()
        {
            if (nvProvd) return nvOk;
            nvProvd = true;
            try
            {
                IntPtr q = nvapi_QueryInterface(0x0150E828);            // NvAPI_Initialize
                if (q == IntPtr.Zero) return false;
                InitDel init = (InitDel)Marshal.GetDelegateForFunctionPointer(q, typeof(InitDel));
                if (init() != 0) return false;

                IntPtr e = nvapi_QueryInterface(0x9ABDD40D);            // EnumNvidiaDisplayHandle
                IntPtr s = nvapi_QueryInterface(0x172409B4);            // SetDVCLevel
                IntPtr g = nvapi_QueryInterface(0x4085DE45);            // GetDVCInfo
                if (e == IntPtr.Zero || s == IntPtr.Zero || g == IntPtr.Zero) return false;

                EnumDel enu = (EnumDel)Marshal.GetDelegateForFunctionPointer(e, typeof(EnumDel));
                IntPtr h = IntPtr.Zero;
                if (enu(0, ref h) != 0 || h == IntPtr.Zero) return false;

                nvDisplay = h;
                nvSet = (SetDvcDel)Marshal.GetDelegateForFunctionPointer(s, typeof(SetDvcDel));
                nvGet = (GetDvcDel)Marshal.GetDelegateForFunctionPointer(g, typeof(GetDvcDel));
                nvOk = true;
            }
            catch (Exception ex) { Util.Log("NVAPI ikke tilgjengelig: " + ex.Message); }
            return nvOk;
        }

        // Standard er 50. Over det gir mer metning.
        public static int Vibrance()
        {
            if (!NvInit()) return -1;
            try
            {
                DvcInfo i = new DvcInfo();
                i.version = (uint)((Marshal.SizeOf(typeof(DvcInfo))) | (1 << 16));
                return nvGet(nvDisplay, IntPtr.Zero, ref i) == 0 ? (int)i.currentLevel : -1;
            }
            catch (Exception) { return -1; }
        }

        public static bool SetVibrance(int level)
        {
            if (!NvInit()) return false;
            try { return nvSet(nvDisplay, IntPtr.Zero, level) == 0; }
            catch (Exception) { return false; }
        }

        [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr nvapi_QueryInterface(uint id);

        // ---------------------------------------------------------------
        [DllImport("gdi32.dll")] static extern bool GetDeviceGammaRamp(IntPtr hdc, ushort[] r);
        [DllImport("gdi32.dll")] static extern bool SetDeviceGammaRamp(IntPtr hdc, ushort[] r);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr CreateDC(string drv, string dev, string port, IntPtr dm);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool EnumDisplayDevices(string dev, uint n, ref DISPLAY_DEVICE d, uint f);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool EnumDisplaySettings(string dev, int mode, ref DEVMODE dm);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int ChangeDisplaySettingsEx(string dev, ref DEVMODE dm, IntPtr wnd, uint flags, IntPtr param);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public uint StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public uint dmFields;
            public int dmPositionX, dmPositionY;
            public uint dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags,
                        dmDisplayFrequency, dmICMMethod, dmICMIntent, dmMediaType,
                        dmDitherType, dmReserved1, dmReserved2,
                        dmPanningWidth, dmPanningHeight;
        }
    }
}
