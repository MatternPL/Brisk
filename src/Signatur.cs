using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Brisk
{
    // ------------------------------------------------------------------
    //  Er denne fila signert av oss?
    //
    //  Sjekksummen i oppdatering.json sier bare at fila stemmer med det
    //  manifestet sa. Men sjekksummen kommer FRA manifestet - kan noen
    //  servere et endret manifest, stemmer den mot deres egen fil. En
    //  Authenticode-signatur er en uavhengig kontroll: den binder fila til
    //  et sertifikat en offentlig utsteder har gaatt god for, og det kan
    //  ikke forfalskes ved aa bytte ut en JSON-fil.
    //
    //  To ting kontrolleres, og begge maa stemme:
    //    1. WinVerifyTrust - er signaturen ekte og kjeden klarert
    //    2. navnet i sertifikatet - er det VAAR signatur, ikke bare en
    //       hvilken som helst gyldig en
    //
    //  Uten punkt 2 ville enhver signert fil sluppet gjennom.
    // ------------------------------------------------------------------
    public static class Signatur
    {
        // Den delen av emnet som ikke endrer seg naar sertifikatet fornyes.
        // Certum setter «Open Source Developer» foran navnet i CN, og det
        // kan de finne paa aa skrive annerledes ved fornyelse - navnet gjor
        // de ikke.
        public const string Eier = "Mathias Arne Andresen";

        static readonly Guid GenericVerifyV2 =
            new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        static extern uint WinVerifyTrust(IntPtr hwnd, ref Guid actionId, IntPtr data);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        class FileInfoW
        {
            public uint cbStruct;
            public string pcwszFilePath;
            public IntPtr hFile = IntPtr.Zero;
            public IntPtr pgKnownSubject = IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        class TrustData
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData = IntPtr.Zero;
            public IntPtr pSIPClientData = IntPtr.Zero;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile = IntPtr.Zero;
            public uint dwStateAction;
            public IntPtr hWVTStateData = IntPtr.Zero;
            public IntPtr pwszURLReference = IntPtr.Zero;
            public uint dwProvFlags;
            public uint dwUIContext;
        }

        const uint UiNone = 2;
        const uint RevokeNone = 0;
        const uint ChoiceFile = 1;
        const uint StateVerify = 1;
        const uint StateClose = 2;

        // Returnerer null naar alt er i orden, ellers en forklaring til
        // brukeren. Feiler noe uventet, er svaret «ikke godkjent» - en
        // signaturkontroll som gir etter naar den er usikker er ingen
        // kontroll.
        public static string Sjekk(string path)
        {
            if (!File.Exists(path)) return L.T("Filen finnes ikke.");

            uint svar;
            FileInfoW fil = new FileInfoW();
            fil.cbStruct = (uint)Marshal.SizeOf(typeof(FileInfoW));
            fil.pcwszFilePath = path;

            IntPtr pFil = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(FileInfoW)));
            TrustData data = new TrustData();
            IntPtr pData = IntPtr.Zero;
            try
            {
                Marshal.StructureToPtr(fil, pFil, false);

                data.cbStruct = (uint)Marshal.SizeOf(typeof(TrustData));
                data.dwUIChoice = UiNone;
                // Tilbakekalling krever nett. Blir svaret tregt eller borte,
                // vil vi ikke at en gyldig oppdatering skal stoppe - selve
                // signaturen og kjeden kontrolleres uansett.
                data.fdwRevocationChecks = RevokeNone;
                data.dwUnionChoice = ChoiceFile;
                data.pFile = pFil;
                data.dwStateAction = StateVerify;

                pData = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TrustData)));
                Marshal.StructureToPtr(data, pData, false);

                Guid handling = GenericVerifyV2;
                svar = WinVerifyTrust(IntPtr.Zero, ref handling, pData);

                // Maa lukkes, ellers lekker tilstanden WinVerifyTrust satte av.
                TrustData igjen = (TrustData)Marshal.PtrToStructure(pData, typeof(TrustData));
                igjen.dwStateAction = StateClose;
                Marshal.StructureToPtr(igjen, pData, false);
                Guid h2 = GenericVerifyV2;
                WinVerifyTrust(IntPtr.Zero, ref h2, pData);
            }
            catch (Exception ex)
            {
                Util.Log("Signaturkontroll feilet: " + ex.Message);
                return L.T("Signaturen kunne ikke kontrolleres.");
            }
            finally
            {
                if (pData != IntPtr.Zero) Marshal.FreeHGlobal(pData);
                Marshal.FreeHGlobal(pFil);
            }

            if (svar != 0)
            {
                Util.Log("Signatur avvist av Windows, kode 0x" + svar.ToString("X8") + ": " + path);
                if (svar == 0x800B0100) return L.T("Filen er ikke signert.");
                if (svar == 0x800B0109) return L.T("Filen er signert av noen Windows ikke stoler på.");
                return L.T("Signaturen er ikke gyldig.");
            }

            // Signaturen er ekte - men av hvem?
            string emne;
            try
            {
                X509Certificate2 c = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
                emne = c.Subject;
            }
            catch (Exception ex)
            {
                Util.Log("Kunne ikke lese signatarens sertifikat: " + ex.Message);
                return L.T("Signaturen kunne ikke kontrolleres.");
            }

            if (emne.IndexOf(Eier, StringComparison.OrdinalIgnoreCase) < 0)
            {
                Util.Log("Signert av feil eier: " + emne);
                return L.T("Filen er signert, men ikke av utvikleren av Brisk.");
            }

            Util.Log("Signatur godkjent: " + emne);
            return null;
        }
    }
}
