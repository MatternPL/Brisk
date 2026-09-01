using System;
using System.Runtime.InteropServices;

namespace Brisk
{
    // ------------------------------------------------------------------
    //  NVIDIA sitt innstillingslager (DRS)
    //
    //  Det samme lageret NVIDIA Kontrollpanel skriver til. Naas gjennom
    //  nvapi64.dll, som ikke har vanlige eksporter - alt gaar via
    //  nvapi_QueryInterface med en fast id per funksjon.
    //
    //  ID-ene under er ikke gjettet. NvAPI_DRS_GetSettingNameFromId ble
    //  spurt om hva hver enkelt heter, og svarte:
    //      0x1057EB71  Power management mode
    //      0x00198FFF  Shader Cache
    //      0x007BA09E  Maximum pre-rendered frames
    //
    //  «Angre» sletter innstillingen fra profilen i stedet for aa skrive en
    //  antatt standardverdi. Da er det NVIDIA sin egen standard som gjelder
    //  igjen, ikke vaar gjetning om hva den er.
    //
    //  AMD har ingen tilsvarende offentlig tjeneste. Der finnes ikke disse
    //  valgene i det hele tatt - se PageGame.
    // ------------------------------------------------------------------
    public static class NvControl
    {
        public const uint PowerMode = 0x1057EB71;   // 1 = foretrekk maks ytelse
        public const uint ShaderCache = 0x00198FFF; // 0xFFFFFFFF = ubegrenset
        public const uint PreRendered = 0x007BA09E; // 1 = lav ventetid

        public const uint PowerMaxPerformance = 1;
        public const uint ShaderCacheUnlimited = 0xFFFFFFFF;
        public const uint PreRenderedLowLatency = 1;

        // ---------------------------------------------------------------
        [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface",
                   CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr Q(uint id);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Init();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Create(out IntPtr s);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int One(IntPtr s);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Base(IntPtr s, out IntPtr p);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Get(IntPtr s, IntPtr p, uint id, IntPtr set);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Set(IntPtr s, IntPtr p, IntPtr set);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Del(IntPtr s, IntPtr p, uint id);

        // NVDRS_SETTING er stor og versjonsmerket. Vi trenger bare de foerste
        // feltene og verdien, saa den legges ut for haand med romslig plass.
        // Layout: version, settingName[2048 wchar], settingId, settingType,
        //         settingLocation, isCurrentPredefined, isPredefinedValid,
        //         predefinedValue(u32 + fyll), currentValue(u32 + fyll)
        const int NavnBytes = 2048 * 2;
        const int VerdiBytes = 4 + 4096;                 // u32 i en union med en streng
        const int Str = 4 + NavnBytes + 4 * 5 + VerdiBytes * 2;
        // Fem u32-felter mellom navnet og den forste unionen: settingId,
        // settingType, settingLocation, isCurrentPredefined, isPredefinedValid.
        // Foerste forsok brukte fire, og da havnet verdien fire byte feil -
        // skrivingen gikk gjennom, men leste alltid tilbake 0.
        const int OffsetId = 4 + NavnBytes;
        const int OffsetType = OffsetId + 4;
        const int OffsetPredef = OffsetId + 20;
        const int OffsetCurrent = OffsetPredef + VerdiBytes;

        static bool provd, ok;
        static Create create; static One load, save, destroy;
        static Base basep; static Get get; static Set set; static Del del;

        public static bool Available { get { return Klar(); } }

        static bool Klar()
        {
            if (provd) return ok;
            provd = true;
            try
            {
                IntPtr i = Q(0x0150E828);
                if (i == IntPtr.Zero) return false;
                if (((Init)Marshal.GetDelegateForFunctionPointer(i, typeof(Init)))() != 0) return false;

                create = D<Create>(0x0694D52E);
                load = D<One>(0x375DBD6B);
                save = D<One>(0xFCBC7E14);
                destroy = D<One>(0xDAD9CFF8);
                basep = D<Base>(0xDA8466A0);
                get = D<Get>(0x73BF8338);
                set = D<Set>(0x577DD202);
                del = D<Del>(0xE4A26362);

                ok = create != null && load != null && save != null && basep != null
                     && get != null && set != null && del != null;
            }
            catch (Exception ex) { Util.Log("NVAPI DRS utilgjengelig: " + ex.Message); }
            return ok;
        }

        static T D<T>(uint id) where T : class
        {
            IntPtr p = Q(id);
            return p == IntPtr.Zero ? null
                 : Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
        }

        // Hver operasjon aapner og lukker sin egen okt. NVIDIA sin
        // dokumentasjon er tydelig paa at en okt ikke skal holdes aapen.
        static bool MedOkt(Func<IntPtr, IntPtr, bool> arbeid, bool lagre)
        {
            if (!Klar()) return false;
            IntPtr s = IntPtr.Zero;
            try
            {
                if (create(out s) != 0) return false;
                if (load(s) != 0) return false;
                IntPtr p;
                if (basep(s, out p) != 0) return false;
                bool r = arbeid(s, p);
                if (r && lagre) save(s);
                return r;
            }
            catch (Exception ex) { Util.Log("NVAPI DRS feilet: " + ex.Message); return false; }
            finally { if (s != IntPtr.Zero && destroy != null) destroy(s); }
        }

        static IntPtr NyttFelt()
        {
            IntPtr b = Marshal.AllocHGlobal(Str);
            for (int i = 0; i < Str; i++) Marshal.WriteByte(b, i, 0);
            Marshal.WriteInt32(b, 0, (Str & 0xFFFF) | (1 << 16));
            return b;
        }

        // Returnerer verdien, eller -1 naar innstillingen ikke er satt.
        // Ikke satt betyr at NVIDIA sin egen standard gjelder.
        public static long Read(uint id)
        {
            long ut = -1;
            MedOkt(delegate(IntPtr s, IntPtr p)
            {
                IntPtr b = NyttFelt();
                try
                {
                    if (get(s, p, id, b) != 0) return false;
                    ut = (uint)Marshal.ReadInt32(b, OffsetCurrent);
                    return true;
                }
                finally { Marshal.FreeHGlobal(b); }
            }, false);
            return ut;
        }

        public static bool Write(uint id, uint verdi)
        {
            bool r = MedOkt(delegate(IntPtr s, IntPtr p)
            {
                IntPtr b = NyttFelt();
                try
                {
                    Marshal.WriteInt32(b, OffsetId, (int)id);
                    Marshal.WriteInt32(b, OffsetType, 0);        // NVDRS_DWORD_TYPE
                    Marshal.WriteInt32(b, OffsetCurrent, (int)verdi);
                    return set(s, p, b) == 0;
                }
                finally { Marshal.FreeHGlobal(b); }
            }, true);
            Util.Log("NVIDIA 0x" + id.ToString("X8") + " = " + verdi + (r ? " satt." : " FEILET."));
            return r;
        }

        // Sletter innstillingen, saa NVIDIA sin standard gjelder igjen.
        public static bool Clear(uint id)
        {
            bool r = MedOkt(delegate(IntPtr s, IntPtr p)
            {
                int rc = del(s, p, id);
                return rc == 0 || rc == -9;      // -9 = var ikke satt fra for
            }, true);
            Util.Log("NVIDIA 0x" + id.ToString("X8") + (r ? " tilbakestilt." : " FEILET aa tilbakestille."));
            return r;
        }
    }
}
