using System;
using System.IO;
using System.Reflection;

// Kjorer den ekte oppdateringskoden mot det publiserte manifestet, men uten aa
// installere noe. Bygges med versjon 1.2.0 slik at 1.3.0 framstaar som nyere.
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

namespace Brisk
{
    static class SjekkOppdatering
    {
        static int Main()
        {
            Console.WriteLine("Klienten later som den er " + Updater.CurrentVersion);
            Console.WriteLine("Manifest: " + Updater.DefaultManifestUrl);
            Console.WriteLine();

            string error;
            UpdateInfo u = Updater.Check(out error);
            if (error != null) { Console.WriteLine("FEIL ved sjekk: " + error); return 1; }
            if (u == null) { Console.WriteLine("FEIL: ingen oppdatering tilbudt"); return 1; }

            Console.WriteLine("Tilbudt versjon : " + u.Version);
            Console.WriteLine("Nedlasting      : " + u.Url);
            Console.WriteLine("Forventet sha256: " + u.Sha256);
            Console.WriteLine("Forventet stoer.: " + u.Size);
            Console.WriteLine();
            Console.WriteLine("Laster ned og verifiserer...");

            string path = Updater.Download(u, null, out error);
            if (error != null) { Console.WriteLine("FEIL ved nedlasting: " + error); return 1; }
            if (path == null || !File.Exists(path)) { Console.WriteLine("FEIL: ingen fil"); return 1; }

            Console.WriteLine("OK. Sjekksum godkjent av klienten selv.");
            Console.WriteLine("Lastet til      : " + path);
            Console.WriteLine("Faktisk stoer.  : " + new FileInfo(path).Length);
            Console.WriteLine();
            Console.WriteLine("Endringsnotat klienten viser:");
            Console.WriteLine("  " + u.Notes);

            try { File.Delete(path); } catch (Exception) { }
            return 0;
        }
    }
}
