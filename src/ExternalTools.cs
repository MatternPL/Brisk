using System.Collections.Generic;

namespace Brisk
{
    // Ett verktoy som Brisk peker paa, men aldri kjorer selv.
    public class ExternalTool
    {
        public string Name = "";        // vises som overskrift, oversettes ikke
        public string By = "";          // hvem som har laget det
        public string Licence = "";     // "MIT", "Gratis", "Freeware" ...
        public string What = "";        // én linje paa norsk, gaar gjennom L.T
        public string Command = "";     // kommandoen brukeren kopierer
        public string Url = "";         // prosjektsiden
        public bool Remote;             // kommandoen henter og kjorer kode fra nettet
        public bool OwnWindow;          // aapner sitt eget vindu, utdata vises ikke her
    }

    // ==================================================================
    //  VERKTOY-SIDA
    // ==================================================================
    // Vil du legge til et verktoy: legg en ny blokk i listen under, og en
    // engelsk oversettelse av What i src/Lang.cs. Se docs/verktoy.md.
    //
    // Regelen for denne sida: kommandoen skal alltid staa synlig, saa brukeren
    // ser hva som kjores for han trykker. Sett Remote=true hvis kommandoen
    // henter kode fra nettet - da kommer det en ekstra bekreftelse.
    public static class ExternalTools
    {
        public static List<ExternalTool> All()
        {
            List<ExternalTool> l = new List<ExternalTool>();

            l.Add(Make("WinUtil", "Chris Titus Tech", "MIT",
                "Rydder Windows og fjerner apper du ikke ba om.",
                "irm https://christitus.com/win | iex",
                "https://github.com/ChrisTitusTech/winutil", true, true));

            l.Add(Make("PowerToys", "Microsoft", "MIT",
                "Ekstra Windows-verktøy fra Microsoft.",
                "winget install Microsoft.PowerToys",
                "https://github.com/microsoft/PowerToys"));

            l.Add(Make("Process Explorer", "Microsoft Sysinternals", "Gratis",
                "Ser hvilken prosess som låser en fil.",
                "winget install Microsoft.Sysinternals.ProcessExplorer",
                "https://learn.microsoft.com/sysinternals/downloads/process-explorer"));

            l.Add(Make("Autoruns", "Microsoft Sysinternals", "Gratis",
                "Alt som starter med Windows.",
                "winget install Microsoft.Sysinternals.Autoruns",
                "https://learn.microsoft.com/sysinternals/downloads/autoruns"));

            l.Add(Make("CrystalDiskInfo", "Crystal Dew World", "MIT",
                "Diskhelse. Varsler før en disk ryker.",
                "winget install CrystalDewWorld.CrystalDiskInfo",
                "https://crystalmark.info/en/software/crystaldiskinfo/"));

            l.Add(Make("HWiNFO", "REALiX", "Gratis til privat bruk",
                "Temperaturer, vifter og sensorer.",
                "winget install REALiX.HWiNFO",
                "https://www.hwinfo.com/"));

            l.Add(Make("O&O ShutUp10++", "O&O Software", "Gratis",
                "Slår av sporing og telemetri i Windows.",
                "winget install OO-Software.ShutUp10",
                "https://www.oo-software.com/en/shutup10"));

            l.Add(Make("Everything", "voidtools", "Gratis",
                "Finner filer på navn, med én gang.",
                "winget install voidtools.Everything",
                "https://www.voidtools.com/"));

            l.Add(Make("Rufus", "Pete Batard", "GPLv3",
                "Lager oppstartbar USB fra en ISO.",
                "winget install Rufus.Rufus",
                "https://rufus.ie/"));

            l.Add(Make("7-Zip", "Igor Pavlov", "LGPL",
                "Pakker ut alle arkivformater.",
                "winget install 7zip.7zip",
                "https://www.7-zip.org/"));

            return l;
        }

        static ExternalTool Make(string name, string by, string licence,
                                 string what, string command, string url)
        {
            return Make(name, by, licence, what, command, url, false, false);
        }

        static ExternalTool Make(string name, string by, string licence,
                                 string what, string command, string url,
                                 bool remote, bool ownWindow)
        {
            ExternalTool t = new ExternalTool();
            t.Name = name;
            t.By = by;
            t.Licence = licence;
            t.What = what;
            t.Command = command;
            t.Url = url;
            t.Remote = remote;
            t.OwnWindow = ownWindow;
            return t;
        }
    }
}
