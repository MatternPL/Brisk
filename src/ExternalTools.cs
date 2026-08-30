using System.Collections.Generic;

namespace Brisk
{
    // Ett verktoy paa Verktoy-sida.
    public class ExternalTool
    {
        public string Name = "";        // overskrift paa flisen, oversettes ikke
        public string By = "";          // hvem som har laget det
        public string Licence = "";     // "MIT", "Gratis", "GPLv3" ...
        public string What = "";        // én kort linje paa norsk, gaar gjennom L.T
        public string Command = "";     // kommandoen som vises og kjores
        public string Url = "";         // prosjektsiden
        public string Icon = "verktoy"; // ikonnokkel, se src/Icons.cs

        // Hvordan kommandoen skal kjores:
        //   ""           program og argumenter direkte. Gjelder winget, sfc,
        //                ipconfig - alt som er ett program med flagg.
        //   "powershell" hele linja sendes til powershell -Command. Trengs saa
        //                snart du bruker ror, cmdlets, variabler eller & 'sti'.
        //   "cmd"        hele linja sendes til cmd /c. For gammeldagse
        //                bat-kommandoer og %-variabler.
        public string Shell = "";

        // Hva som startes naar verktoyet er installert. Kan vaere navnet paa
        // exe-fila winget la i Links-mappa ("procexp"), eller et ord som finnes
        // i snarveien i Start-menyen ("CrystalDiskInfo"). Er den tom, brukes
        // Name. Gjelder ikke verktoy som selv er en kommando.
        public string Launch = "";

        // Kommandoen trenger administrator. Kjorer Brisk allerede som admin,
        // arver kommandoen det, og utdata vises i konsollen som vanlig. Gjor den
        // ikke det, aapnes kommandoen i et hevet vindu i stedet - Windows lar
        // ikke en vanlig prosess fange utdata fra en hevet.
        public bool Admin;

        public bool Remote;             // henter kode fra nettet
        public bool OwnWindow;          // aapner eget vindu -> hevet, utdata vises ikke her
    }

    // ==================================================================
    //  VERKTOY-SIDA
    // ==================================================================
    // Legg til et verktoy ved aa lime inn en blokk under, og en engelsk
    // oversettelse av What i src/Lang.cs. Se docs/verktoy.md.
    //
    // Regelen for denne sida: kommandoen skal alltid staa synlig, saa brukeren
    // ser hva som kjores for han trykker.
    public static class ExternalTools
    {
        public static List<ExternalTool> All()
        {
            List<ExternalTool> l = new List<ExternalTool>();

            l.Add(new ExternalTool
            {
                Name = "WinUtil",
                By = "Chris Titus Tech",
                Licence = "MIT",
                What = "Rydder Windows og fjerner apper du ikke ba om.",
                Command = "irm https://christitus.com/win | iex",
                Url = "https://github.com/ChrisTitusTech/winutil",
                Icon = "tilpass",
                Shell = "powershell",
                Admin = true,
                Remote = true,
                OwnWindow = true,
            });

            l.Add(new ExternalTool
            {
                Name = "PowerToys",
                Launch = "PowerToys",
                By = "Microsoft",
                Licence = "MIT",
                What = "Ekstra Windows-verktøy fra Microsoft.",
                Command = "winget install Microsoft.PowerToys",
                Url = "https://github.com/microsoft/PowerToys",
                Icon = "verktoy",
            });

            l.Add(new ExternalTool
            {
                Name = "Process Explorer",
                Launch = "procexp",
                By = "Microsoft Sysinternals",
                Licence = "Gratis",
                What = "Ser hvilken prosess som låser en fil.",
                Command = "winget install Microsoft.Sysinternals.ProcessExplorer",
                Url = "https://learn.microsoft.com/sysinternals/downloads/process-explorer",
                Icon = "logg",
            });

            l.Add(new ExternalTool
            {
                Name = "Autoruns",
                Launch = "Autoruns",
                By = "Microsoft Sysinternals",
                Licence = "Gratis",
                What = "Alt som starter med Windows.",
                Command = "winget install Microsoft.Sysinternals.Autoruns",
                Url = "https://learn.microsoft.com/sysinternals/downloads/autoruns",
                Icon = "oppstart",
            });

            l.Add(new ExternalTool
            {
                Name = "CrystalDiskInfo",
                Launch = "CrystalDiskInfo",
                By = "Crystal Dew World",
                Licence = "MIT",
                What = "Diskhelse. Varsler før en disk ryker.",
                Command = "winget install CrystalDewWorld.CrystalDiskInfo",
                Url = "https://crystalmark.info/en/software/crystaldiskinfo/",
                Icon = "disk",
            });

            l.Add(new ExternalTool
            {
                Name = "HWiNFO",
                Launch = "HWiNFO",
                By = "REALiX",
                Licence = "Gratis til privat bruk",
                What = "Temperaturer, vifter og sensorer.",
                Command = "winget install REALiX.HWiNFO",
                Url = "https://www.hwinfo.com/",
                Icon = "temperatur",
            });

            l.Add(new ExternalTool
            {
                Name = "O&O ShutUp10++",
                Launch = "OOSU10",
                By = "O&O Software",
                Licence = "Gratis",
                What = "Slår av sporing og telemetri i Windows.",
                Command = "winget install OO-Software.ShutUp10",
                Url = "https://www.oo-software.com/en/shutup10",
                Icon = "skjold",
            });

            l.Add(new ExternalTool
            {
                Name = "Everything",
                Launch = "Everything",
                By = "voidtools",
                Licence = "Gratis",
                What = "Finner filer på navn, med én gang.",
                Command = "winget install voidtools.Everything",
                Url = "https://www.voidtools.com/",
                Icon = "sok",
            });

            l.Add(new ExternalTool
            {
                Name = "Rufus",
                Launch = "Rufus",
                By = "Pete Batard",
                Licence = "GPLv3",
                What = "Lager oppstartbar USB fra en ISO.",
                Command = "winget install Rufus.Rufus",
                Url = "https://rufus.ie/",
                Icon = "usb",
            });

            l.Add(new ExternalTool
            {
                Name = "7-Zip",
                Launch = "7-Zip File Manager",
                By = "Igor Pavlov",
                Licence = "LGPL",
                What = "Pakker ut alle arkivformater.",
                Command = "winget install 7zip.7zip",
                Url = "https://www.7-zip.org/",
                Icon = "programvare",
            });

            l.Add(new ExternalTool
            {
                Name    = "CPU-Z",
                Launch  = "CPU-Z",
                By      = "CPUID",
                Licence = "Gratis",
                What    = "Viser hva slags maskinvare du faktisk har.",
                Command = "winget install CPUID.CPU-Z",
                Url     = "https://www.cpuid.com/softwares/cpu-z.html",
                Icon    = "minne",
            });

            // Eksempel paa en ren PowerShell-kommando. Ingen winget, ingen
            // installasjon - den aapner et PowerShell-vindu som blir staaende.

            return l;
        }
    }
}
