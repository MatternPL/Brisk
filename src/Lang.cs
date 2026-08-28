using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace Brisk
{
    // Språklag.
    //
    // Standardspråket er ENGELSK. Kildekoden bruker den norske teksten som nøkkel
    // fordi programmet ble skrevet på norsk først — L.T("Rydding") gir "Cleanup"
    // med mindre brukeren har valgt norsk, og da gis nøkkelen tilbake uendret.
    //
    // tools/sprak_sjekk.py finner nøkler i koden som mangler i tabellen.
    public static class L
    {
        const string SettingsKey = @"Software\Brisk";

        static string lang;

        public static string Lang
        {
            get
            {
                if (lang == null)
                {
                    lang = ReadSetting("Sprak");
                    if (lang != "no") lang = "en";       // engelsk er standard
                }
                return lang;
            }
            set
            {
                lang = (value == "no") ? "no" : "en";
                WriteSetting("Sprak", lang);
            }
        }

        public static bool IsNorwegian { get { return Lang == "no"; } }

        public static string T(string no)
        {
            if (no == null) return null;
            if (Lang == "no") return no;
            string en;
            return Map.TryGetValue(no, out en) && en.Length > 0 ? en : no;
        }

        // Oversetter og setter inn verdier. Nøkkelen bruker {0}, {1} … slik at
        // ordstillingen kan være ulik på de to språkene.
        public static string F(string no, params object[] args)
        {
            try { return string.Format(T(no), args); }
            catch { return T(no); }
        }

        static string ReadSetting(string name)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(SettingsKey))
                    return k == null ? null : Convert.ToString(k.GetValue(name));
            }
            catch { return null; }
        }

        static void WriteSetting(string name, string value)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(SettingsKey))
                    if (k != null) k.SetValue(name, value);
            }
            catch { }
        }

        // ---------------------------------------------------------------
        // Bygges ved første oppslag. Kan ikke bygges i et felt-initialiserer:
        // statiske felt settes i deklarasjonsrekkefølge, og Pairs står lenger nede.
        static Dictionary<string, string> map;

        static Dictionary<string, string> Map
        {
            get
            {
                if (map == null)
                {
                    Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.Ordinal);
                    for (int i = 0; i + 1 < Pairs.Length; i += 2)
                        d[Pairs[i]] = Pairs[i + 1];
                    map = d;
                }
                return map;
            }
        }

        public static int Count { get { return Map.Count; } }
        public static bool Has(string no) { return Map.ContainsKey(no); }

        // Par: norsk nøkkel, engelsk tekst.
        static readonly string[] Pairs =
        {
            // ---- skall ----
            "Administrator", "Administrator",
            "Begrenset", "Limited",
            "Kjør som administrator", "Run as administrator",
            "Språk", "Language",

            "Oversikt", "Overview",
            "Rydding", "Cleanup",
            "Diskplass", "Disk space",
            "Oppstart", "Startup",
            "Minne", "Memory",
            "Oppdateringer", "Updates",
            "Programvare", "Software",
            "Vedlikehold", "Maintenance",
            "Logg", "Log",

            "Tilstanden akkurat nå.", "How things stand right now.",
            "Filer som bare tar plass.", "Files that only take up space.",
            "Hvor plassen har blitt av.", "Where the space went.",
            "Det som starter med Windows.", "What starts with Windows.",
            "Hva RAM-en brukes til.", "What the RAM is used for.",
            "Fra Windows Update.", "Straight from Windows Update.",
            "Oppdater eller fjern programmer.", "Update or remove programs.",
            "Reparasjon og diskhelse.", "Repair and disk health.",
            "Alt som er gjort.", "Everything that has been done.",

            // ---- oversikt ----
            "Ledig på systemdisken", "Free on system drive",
            "Starter med Windows", "Start with Windows",
            "Søppel", "Junk",
            "Kjør sjekk", "Run check",
            "Oppdater", "Refresh",
            "Verdt å se på", "Worth a look",
            "Funn", "Finding",
            "Hva du kan gjøre", "What you can do",
            "Måler søppelfiler og ser etter ting som er verdt å gjøre noe med.",
                "Measures junk files and looks for anything worth acting on.",
            "Måler …", "Measuring …",
            "Avbrutt: ", "Cancelled: ",
            "Ingenting å påpeke.", "Nothing to flag.",
            "Kjør en sjekk for å måle søppelfiler", "Run a check to measure junk files",
            "Bare {0} ledig på {1}", "Only {0} free on {1}",
            "Rydd, eller se hva som tar plassen", "Clean up, or see what's using it",
            "{0} søppelfiler", "{0} of junk files",
            "Rens dem", "Clean them out",
            "{0} programmer starter med Windows", "{0} programs start with Windows",
            "Slå av det du ikke trenger", "Turn off what you don't need",
            "Minnet er {0} % fullt", "Memory is {0}% full",
            "Se hva som bruker det", "See what's using it",
            "{0} enheter melder feil", "{0} devices reporting errors",
            "Se etter drivere", "Look for drivers",
            "Windows.old ligger igjen etter en oppgradering", "Windows.old left over from an upgrade",
            "Kan slettes under Rydding", "Can be deleted under Cleanup",
            "Disken {0} melder «{1}»", "Drive {0} reports \"{1}\"",
            "Ta sikkerhetskopi nå", "Back up now",
            "{0} av {1}", "{0} of {1}",
            "{0} % av {1}", "{0}% of {1}",
            "av {0}", "of {0}",
            "{0} kan ryddes bort.", "{0} can be cleared out.",
            "Kunne ikke lese systemtall: ", "Could not read system figures: ",

            // ---- rydding ----
            "Analyser", "Analyse",
            "Rens", "Clean",
            "Merk alle", "Select all",
            "Kategori", "Category",
            "Størrelse", "Size",
            "Filer", "Files",
            "Merknad", "Note",
            "Les beskrivelsen", "Read the description",
            "Ferdig", "Done",
            "Måler hvor mye hver kategori inneholder. Sletter ingenting.",
                "Measures how much each category holds. Deletes nothing.",
            "{0} kan slettes.", "{0} can be deleted.",
            "Ingenting er merket.", "Nothing is selected.",
            "Sletter {0} kategorier. Dine egne filer, passord og bokmerker røres ikke.",
                "Deleting {0} categories. Your own files, passwords and bookmarks are left alone.",
            "Én av dem er merket med forbehold. Les beskrivelsen først.",
                "One of them is flagged. Read its description first.",
            "Fortsette?", "Continue?",
            "{0} i bruk", "{0} in use",
            "Frigjorde {0}", "Freed {0}",
            "Frigjorde {0}. {1} filer slettet, {2} var i bruk.",
                "Freed {0}. {1} files deleted, {2} were in use.",

            // kategorinavn
            "Midlertidige filer (bruker)", "Temporary files (user)",
            "Midlertidige filer (Windows)", "Temporary files (Windows)",
            "Papirkurv", "Recycle Bin",
            "Windows Update-nedlastinger", "Windows Update downloads",
            "Delivery Optimization", "Delivery Optimization",
            "Krasjdumper", "Crash dumps",
            "Feilrapportering", "Error reporting",
            "Systemlogger", "System logs",
            "Miniatyrbilde- og ikon-cache", "Thumbnail and icon cache",
            "Grafikk-cache", "Graphics cache",
            "Nettleser-cache", "Browser cache",
            "App-cache", "App cache",
            "Windows.old", "Windows.old",
            "Oppsettsrester", "Setup leftovers",

            // kategoribeskrivelser
            "%TEMP%. Rester etter installasjoner og programmer.",
                "%TEMP%. Leftovers from installers and programs.",
            "C:\\Windows\\Temp.", "C:\\Windows\\Temp.",
            "Alle disker.", "All drives.",
            "Ferdig installerte pakker. Lastes ned igjen hvis det trengs.",
                "Already-installed packages. Downloaded again if needed.",
            "Oppdateringer mellomlagret for deling på nettverket.",
                "Updates cached for sharing across the network.",
            "Dumpfiler fra kræsj og blåskjerm.", "Dump files from crashes and blue screens.",
            "Rapporter som lå i kø til Microsoft.", "Reports queued up for Microsoft.",
            "CBS, Windows Update og DISM. Blir fort hundrevis av MB.",
                "CBS, Windows Update and DISM. Easily hundreds of MB.",
            "Bygges opp igjen. Fikser gale miniatyrbilder.",
                "Rebuilt automatically. Fixes wrong thumbnails.",
            "Kompilerte shadere fra DirectX, NVIDIA og AMD. Bygges opp igjen.",
                "Compiled shaders from DirectX, NVIDIA and AMD. Rebuilt automatically.",
            "Bare cache. Passord, bokmerker og innlogginger røres ikke.",
                "Cache only. Passwords, bookmarks and logins are left alone.",
            "Discord, Spotify, Teams, Slack og Office.", "Discord, Spotify, Teams, Slack and Office.",
            "Rester etter oppgradering, ofte 10–30 GB. Sletting fjerner muligheten til å rulle tilbake.",
                "Left over from an upgrade, often 10–30 GB. Deleting it removes the option to roll back.",
            "Panther-logger etter store Windows-oppdateringer.",
                "Panther logs from major Windows updates.",

            // ---- oppstart ----
            "Slå av", "Turn off",
            "Slå på", "Turn on",
            "Planlagte oppgaver", "Scheduled tasks",
            "Navn", "Name",
            "Status", "Status",
            "Utgiver", "Publisher",
            "Hvor", "Where",
            "Kommando", "Command",
            "På", "On",
            "Av", "Off",
            "Behold", "Keep",
            "Reversibelt. Samme mekanisme som Oppgavebehandling — programmet avinstalleres ikke.",
                "Reversible. Same mechanism as Task Manager — nothing gets uninstalled.",
            "{0} oppføringer, {1} på.", "{0} entries, {1} on.",
            "Merk av det du vil endre først.", "Tick what you want to change first.",
            "Disse gjør noe du sannsynligvis vil beholde:", "These do something you probably want to keep:",
            "Slå av likevel?", "Turn off anyway?",
            "Endret {0}.", "Changed {0}.",
            "Endret {0}. {1} feilet — krever administrator.",
                "Changed {0}. {1} failed — administrator required.",
            "Register (denne brukeren)", "Registry (this user)",
            "Register (alle brukere)", "Registry (all users)",
            "Register (32-bit)", "Registry (32-bit)",
            "Oppstartsmappe", "Startup folder",
            "Planlagt oppgave", "Scheduled task",
            "Windows Sikkerhet-ikonet", "Windows Security icon",
            "Lydbehandling (Realtek)", "Audio (Realtek)",
            "Lydbehandling (Waves)", "Audio (Waves)",
            "Lydbehandling (Nahimic)", "Audio (Nahimic)",
            "Styreplate", "Touchpad",
            "Intel-grafikk", "Intel graphics",
            "Hurtigtaster for grafikk", "Graphics hotkeys",
            "NVIDIA-grafikk", "NVIDIA graphics",
            "AMD-grafikk", "AMD graphics",
            "Antivirus", "Antivirus",
            "Skysynkronisering", "Cloud sync",
            "Passordbehandler", "Password manager",

            // ---- minne ----
            "Frigjør arbeidssett", "Trim working sets",
            "Tøm standby-cache", "Purge standby cache",
            "Program", "Program",
            "Minnebruk", "Memory",
            "Prosesser", "Processes",
            "Andel", "Share",
            "Dytter data fra RAM til disk. Tallet faller, men programmene leser det inn igjen. Sjelden noen reell gevinst.",
                "Pushes data from RAM to disk. The number drops, but programs read it straight back. Rarely a real gain.",
            "Sletter Windows sin filcache. Kan hjelpe rett før et stort spill eller en tung render. Ellers gjør den maskinen tregere en stund.",
                "Clears Windows' file cache. Can help right before a big game or a heavy render. Otherwise it makes the machine slower for a while.",
            "Windows bruker ledig RAM som cache med vilje. Vil du ha varig lavere forbruk: kutt oppstartsprogrammer.",
                "Windows uses free RAM as cache on purpose. For lasting lower usage, cut startup programs.",
            "Arbeidssett frigjort.", "Working sets trimmed.",
            "Krever administrator.", "Administrator required.",
            "Tilgjengelig minne endret seg med {0}{1}.", "Available memory changed by {0}{1}.",
            "Klarte ikke tømme standby-cachen.", "Could not purge the standby cache.",
            "av {0}  ·  {1} %", "of {0}  ·  {1}%",
            "Tilgjengelig: {0}", "Available: {0}",
            "Standby-cache: {0}", "Standby cache: {0}",

            // ---- oppdateringer ----
            "Søk", "Search",
            "Installer merkede", "Install selected",
            "Enhetsbehandling", "Device Manager",
            "Spør Windows Update om drivere og systemoppdateringer. Tar gjerne et minutt.",
                "Asks Windows Update for drivers and system updates. Usually takes a minute.",
            "Enheter med problem", "Devices with problems",
            "Enhet", "Device",
            "Problem", "Problem",
            "Enhets-ID", "Device ID",
            "Tilgjengelig fra Microsoft", "Available from Microsoft",
            "Type", "Type",
            "Oppdatering", "Update",
            "Detaljer", "Details",
            "Windows", "Windows",
            "Driver", "Driver",
            "Leser enhetsliste …", "Reading device list …",
            "Spør Windows Update om drivere …", "Asking Windows Update for drivers …",
            "Spør Windows Update om systemoppdateringer …", "Asking Windows Update for system updates …",
            "Ingen enheter med problemer.", "No devices reporting problems.",
            "Alvorlighet: {0}", "Severity: {0}",
            "{0} Windows-oppdateringer og {1} drivere.", "{0} Windows updates and {1} drivers.",
            "Alt er oppdatert.", "Everything is up to date.",
            "Installerer {0} fra Microsoft. Skjermen kan blinke, og noe krever omstart.",
                "Installing {0} from Microsoft. The screen may flicker, and some of it needs a restart.",
            "Installerte {0} av {1}.", "Installed {0} of {1}.",
            "Omstart kreves.", "Restart required.",
            "Omstart", "Restart",
            "Noe av dette krever omstart for å bli aktivt.", "Some of this needs a restart to take effect.",
            "Uten administrator kan du søke, men ikke installere.",
                "Without administrator you can search, but not install.",
            "(ukjent enhet)", "(unknown device)",
            "Enheten er ikke riktig konfigurert", "The device is not configured correctly",
            "Driveren kan være ødelagt, eller systemet er tomt for minne",
                "The driver may be damaged, or the system is out of memory",
            "Enheten kan ikke starte", "The device cannot start",
            "Finner ikke nok ledige ressurser", "Not enough free resources",
            "Krever omstart for å virke", "Needs a restart to work",
            "Driveren må installeres på nytt", "The driver must be reinstalled",
            "Registeret er skadet for denne enheten", "The registry is damaged for this device",
            "Windows fjerner enheten", "Windows is removing the device",
            "Enheten er deaktivert", "The device is disabled",
            "Enheten er ikke til stede eller virker ikke", "The device is absent or not working",
            "Driveren er ikke installert", "No driver is installed",
            "Windows finner ikke driver som virker", "Windows cannot find a working driver",
            "Enheten er ikke koblet til nå", "The device is not connected right now",
            "Feilkode {0}", "Error code {0}",
            "Ingen nye drivere fra Windows Update.", "No new drivers from Windows Update.",
            "Driversøket feilet: ", "Driver search failed: ",
            "Laster ned {0} driver(e).", "Downloading {0} driver(s).",
            "Nedlasting ferdig.", "Download finished.",
            "Ingen drivere ble lastet ned.", "No drivers were downloaded.",
            "Installerer.", "Installing.",
            "Installasjon feilet: ", "Installation failed: ",
            "Windows er oppdatert.", "Windows is up to date.",
            "Oppdateringssøket feilet: ", "Update search failed: ",
            "Laster ned {0} oppdatering(er).", "Downloading {0} update(s).",
            "Ingenting ble lastet ned.", "Nothing was downloaded.",

            // ---- diskplass ----
            "Analyser plass", "Analyse space",
            "Stopp", "Stop",
            "Åpne i Utforsker", "Open in Explorer",
            "Leser gjennom hele treet. Sletter ingenting.", "Walks the whole tree. Deletes nothing.",
            "Dobbeltklikk en rad gjør det samme.", "Double-clicking a row does the same.",
            "Største mapper", "Largest folders",
            "Største filer (over 100 MB)", "Largest files (over 100 MB)",
            "Mappe", "Folder",
            "Fil", "File",
            "Velg en rad først.", "Select a row first.",
            "Kunne ikke åpne: ", "Could not open: ",
            "Går gjennom {0} …", "Walking {0} …",
            "Avbrutt.", "Cancelled.",
            "Avbryter …", "Cancelling …",
            "{0} mapper, {1} store filer", "{0} folders, {1} large files",
            "Ferdig på {0} s. Største post: {1}.", "Done in {0} s. Largest item: {1}.",

            // ---- programvare ----
            "Se etter oppdateringer", "Check for updates",
            "Oppdater merkede", "Update selected",
            "Programoppdateringer (winget)", "Program updates (winget)",
            "Installert", "Installed",
            "Ny versjon", "New version",
            "Pakke-ID", "Package ID",
            "Installerte programmer", "Installed programs",
            "Avinstaller", "Uninstall",
            "Versjon", "Version",
            "winget mangler. Installer «App Installer» fra Microsoft Store.",
                "winget is missing. Install \"App Installer\" from the Microsoft Store.",
            "Spør winget …", "Asking winget …",
            "{0} kan oppdateres.", "{0} can be updated.",
            "Oppdaterte {0} av {1}.", "Updated {0} of {1}.",
            "Velg et program i den nedre lista.", "Select a program in the lower list.",
            "Avinstaller «{0}»? Programmets egen avinstallering starter.",
                "Uninstall \"{0}\"? The program's own uninstaller will start.",
            "Startet avinstallering av {0}.", "Started uninstalling {0}.",
            "Fant ingen avinstalleringskommando for {0}.", "No uninstall command found for {0}.",
            "{0} programmer · {1}", "{0} programs · {1}",
            "Kunne ikke lese programlista: ", "Could not read the program list: ",
            "Fant ingen liste fra winget.", "No list came back from winget.",
            "Klarte ikke tolke winget-utdata.", "Could not parse winget output.",
            "Alt winget kjenner til er oppdatert.", "Everything winget knows about is up to date.",

            // ---- vedlikehold ----
            "Gjenopprettingspunkt", "Restore point",
            "Rydd komponentlager", "Clean component store",
            "Optimaliser disker", "Optimise drives",
            "Tøm DNS-cache", "Flush DNS cache",
            "Planlagt rydding", "Scheduled cleanup",
            "Systemrapport", "System report",
            "Loggmappe", "Log folder",
            "Se etter oppdatering", "Check for update",
            "Automatisk", "Automatic",
            "Lager et tilbakerullingspunkt før du endrer noe.", "Creates a rollback point before you change anything.",
            "Finner og reparerer ødelagte systemfiler. Tar 5–15 minutter.",
                "Finds and repairs damaged system files. Takes 5–15 minutes.",
            "Reparerer kilden sfc henter friske filer fra. Kjør denne først hvis sfc feiler.",
                "Repairs the source sfc pulls clean files from. Run this first if sfc fails.",
            "Fjerner gamle oppdateringsversjoner i WinSxS. Kan ta lang tid og frigjøre flere GB.",
                "Removes superseded update versions in WinSxS. Slow, but can free several GB.",
            "TRIM på SSD, defragmentering på harddisk.", "TRIM on SSDs, defragmentation on hard drives.",
            "Lar Windows kjøre den trygge ryddingen ukentlig av seg selv.",
                "Lets Windows run the safe cleanup weekly on its own.",
            "Lagrer en tekstfil du kan sende til den som hjelper deg.",
                "Saves a text file you can send to whoever is helping you.",
            "Henter versjonsfilen og sjekker nedlastingen mot sha256 før noe kjøres.",
                "Fetches the version file and verifies the download against sha256 before anything runs.",
            "Disk / volum", "Disk / volume",
            "Helse", "Health",
            "Plass", "Space",
            "Volum", "Volume",
            "Frisk", "Healthy",
            "Advarsel", "Warning",
            "Usunn", "Unhealthy",
            "Ukjent", "Unknown",
            "Lite plass", "Low space",
            "{0} ledig av {1}", "{0} free of {1}",
            "Ukentlig rydding er allerede satt opp. Fjerne den?",
                "Weekly cleanup is already set up. Remove it?",
            "Fjernet.", "Removed.",
            "Kjøre den trygge ryddingen hver søndag kl. 12? Windows.old blir aldri tatt.",
                "Run the safe cleanup every Sunday at 12? Windows.old is never touched.",
            "Satt opp: hver søndag kl. 12.", "Set up: every Sunday at 12.",
            "Klarte ikke opprette oppgaven.", "Could not create the task.",
            "Rapport lagret på skrivebordet.", "Report saved to the desktop.",
            "Kunne ikke lagre rapporten: ", "Could not save the report: ",
            "Uten administrator vil de fleste av disse feile.",
                "Without administrator most of these will fail.",
            "Oppretter gjenopprettingspunkt.", "Creating restore point.",
            "Kjører sfc /scannow. Tar 5–15 minutter.", "Running sfc /scannow. Takes 5–15 minutes.",
            "Kjører DISM /RestoreHealth. Kan ta lang tid.", "Running DISM /RestoreHealth. Can take a while.",
            "Rydder komponentlageret (WinSxS).", "Cleaning the component store (WinSxS).",
            "Optimaliserer {0}", "Optimising {0}",
            "Tømmer DNS-cache.", "Flushing DNS cache.",

            // ---- logg ----
            "Åpne loggfil", "Open log file",
            "Tøm visning", "Clear view",

            // ---- oppdatering av programmet ----
            "Ser etter oppdateringer …", "Checking for updates …",
            "Versjon {0} er tilgjengelig.", "Version {0} is available.",
            "Du har nyeste versjon ({0}).", "You have the latest version ({0}).",
            "Kunne ikke vise oppdateringen: ", "Could not show the update: ",
            "Ny versjon tilgjengelig", "Update available",
            "Brisk {0}", "Brisk {0}",
            "Du har {0}", "You have {0}",
            "Ingen endringsbeskrivelse.", "No release notes.",
            "Oppdater nå", "Update now",
            "Ikke nå", "Not now",
            "Lukk", "Close",
            "Laster ned …", "Downloading …",
            "Laster ned … {0} av {1}", "Downloading … {0} of {1}",
            "Nedlastingen feilet.", "The download failed.",
            "Sjekksum bekreftet. Starter installasjonen …", "Checksum verified. Starting the install …",
            "Oppdateringsadressen må være https.", "The update address must be https.",
            "Fikk ikke kontakt med oppdateringskilden: ", "Could not reach the update source: ",
            "Versjonsfilen kunne ikke tolkes.", "The version file could not be parsed.",
            "Nedlastingsadressen i versjonsfilen må være https.",
                "The download address in the version file must be https.",
            "Versjonsfilen mangler en gyldig sha256-sjekksum. Avbryter.",
                "The version file has no valid sha256 checksum. Aborting.",
            "Nedlastingen feilet: ", "The download failed: ",
            "Sjekksummen stemte ikke. Filen ble slettet — ingenting er kjørt.",
                "The checksum did not match. The file was deleted — nothing was run.",
            "Klarte ikke starte installasjonen: ", "Could not start the installation: ",

            // ---- feil ----
            "Feil: ", "Error: ",

            // ---- installer ----
            "Installer Brisk", "Install Brisk",
            "Avinstaller Brisk", "Uninstall Brisk",
            "Fjerner programmet og snarveiene fra denne maskinen.",
                "Removes the program and its shortcuts from this machine.",
            "Installer", "Install",
            "Avbryt", "Cancel",
            "Oppdater til {0}", "Update to {0}",
            "Allerede installert.", "Already installed.",
            "Rydder søppelfiler, viser hva som starter med Windows, henter drivere og Windows-oppdateringer fra Microsoft, og finner hvor lagringsplassen har blitt av.",
                "Cleans junk files, shows what starts with Windows, pulls drivers and Windows updates from Microsoft, and finds where your storage went.",
            "Ingen betalingsmur, ingen abonnement, ingen datainnsamling.",
                "No paywall, no subscription, no data collection.",
            "Installeres i:", "Installs to:",
            "Trenger ikke administrator.", "No administrator needed.",
            "Lag snarvei på skrivebordet", "Create a desktop shortcut",
            "Start etter installasjon", "Launch when finished",
            "Dette fjerner programfilene, snarveiene og oppføringen i «Apper og funksjoner». Endringer du har gjort i oppstartsprogrammer beholdes.",
                "This removes the program files, the shortcuts and the entry in Apps & features. Changes you made to startup programs stay as they are.",
            "Installerer …", "Installing …",
            "Avinstallerer …", "Uninstalling …",
            "Det gikk galt", "Something went wrong",
            "Ferdig installert", "Installed",
            "Du finner Brisk i Start-menyen.", "You'll find Brisk in the Start menu.",
            "Brisk er fjernet", "Brisk has been removed",
            "Start Brisk", "Start Brisk",

            // ---- oversikt, ny utgave ----
            "Sjekk PC-en", "Check my PC",
            "Rydd opp", "Clean up",
            "Rydd opp {0}", "Clean up {0}",
            "Alt ser bra ut", "Everything looks fine",
            "Ingenting trenger oppmerksomhet nå.", "Nothing needs your attention.",
            "Kjør en sjekk for å være sikker.", "Run a check to be sure.",
            "Én ting er verdt å se på", "One thing is worth a look",
            "{0} ting er verdt å se på", "{0} things are worth a look",
            "Dobbeltklikk en rad under for å gå dit.", "Double-click a row below to go there.",
            "Ledig plass", "Free space",
            "ikke målt", "not measured",
            "Trykk «Rydd opp»", "Press Clean up",
            "Trykk «Sjekk PC-en» for å måle søppelfiler", "Press Check my PC to measure junk files",
            "Måler søppelfiler og ser etter ting som er verdt å gjøre noe med. Endrer ingenting.",
                "Measures junk files and looks for anything worth acting on. Changes nothing.",
            "Sletter bare det som er merket trygt. Windows.old og dine egne filer røres aldri.",
                "Deletes only what is marked safe. Windows.old and your own files are never touched.",
            "Sletter {0} søppelfiler.", "Deleting {0} of junk files.",
            "Dine egne filer, bilder, passord og bokmerker røres ikke.",
                "Your own files, photos, passwords and bookmarks are left alone.",
            "Begrenset tilgang", "Limited access",
            "Rydding av systemfiler, drivere og reparasjon krever administrator.",
                "Cleaning system files, installing drivers and running repairs need administrator.",
            "{0} blåskjermer siste måned", "{0} blue screens in the last month",
            "Se detaljene under Helse", "See the details under Health",

            // ---- helse ----
            "Helse", "Health",
            "Disker, kræsj og batteri.", "Drives, crashes and battery.",
            "Disker", "Drives",
            "Disk", "Drive",
            "Tilstand", "Condition",
            "Slitasje", "Wear",
            "Temperatur", "Temperature",
            "{0} % brukt", "{0}% used",
            "Blåskjermer", "Blue screens",
            "Når", "When",
            "Stoppkode", "Stop code",
            "Sannsynlig årsak", "Likely cause",
            "Ingen blåskjermer i loggen.", "No blue screens in the log.",
            "Batteri", "Battery",
            "Batteri: {0} % av opprinnelig kapasitet", "Battery: {0}% of its original capacity",
            "Lag rapport", "Save report",
            "Lagrer en tekstfil på skrivebordet du kan sende til den som hjelper deg.",
                "Saves a text file on your desktop you can send to whoever is helping you.",
            "Leser disker …", "Reading drives …",
            "Leser hendelseslogg …", "Reading event log …",

            // stoppkoder
            "Driver rørte minne den ikke skulle", "A driver touched memory it should not have",
            "Feil i minnehåndteringen — test RAM-en", "Memory management fault — test the RAM",
            "Feil i en systemtjeneste", "A system service faulted",
            "Lesing fra ugyldig minne — ofte RAM eller driver",
                "Read from invalid memory — usually RAM or a driver",
            "Driver kræsjet", "A driver crashed",
            "Driver hang under strømsparing", "A driver hung during power saving",
            "En kritisk systemprosess døde", "A critical system process died",
            "Grafikkortet svarte ikke — ofte driver eller varme",
                "The graphics card stopped responding — usually driver or heat",
            "Feil i grafikkdriveren", "Fault in the graphics driver",
            "Maskinvarefeil — CPU, minne eller hovedkort", "Hardware fault — CPU, memory or motherboard",
            "En driver holdt prosessoren for lenge", "A driver held the processor too long",
            "Windows oppdaget minneødeleggelse", "Windows detected memory corruption",
            "Uhåndtert feil i kjernen", "Unhandled fault in the kernel",
            "Driver frigjorde minne feil", "A driver released memory incorrectly",

            // ---- nettverk ----
            "Nettverk", "Network",
            "Er tilkoblingen som den skal?", "Is the connection behaving?",
            "Test tilkoblingen", "Test the connection",
            "Nettverksinnstillinger", "Network settings",
            "Nullstill nettverket", "Reset the network",
            "Test", "Check",
            "Resultat", "Result",
            "Sjekker nettverkskort, gateway, internett, DNS, Wi-Fi, hosts-fil og proxy. Endrer ingenting.",
                "Checks the adapter, gateway, internet, DNS, Wi-Fi, hosts file and proxy. Changes nothing.",
            "Siste utvei når ingenting virker. Nullstiller Winsock og TCP/IP, og krever omstart.",
                "Last resort when nothing works. Resets Winsock and TCP/IP, and needs a restart.",
            "Dette nullstiller nettverksoppsettet og krever omstart. Lagrede Wi-Fi-passord beholdes.",
                "This resets the network configuration and needs a restart. Saved Wi-Fi passwords are kept.",
            "Trykk «Test tilkoblingen» for å komme i gang.", "Press Test the connection to begin.",
            "Alt ser normalt ut.", "Everything looks normal.",
            "{0} problemer funnet.", "{0} problems found.",
            "Nettverkskort", "Adapter",
            "Gateway", "Gateway",
            "Internett", "Internet",
            "DNS", "DNS",
            "Wi-Fi", "Wi-Fi",
            "hosts-fil", "hosts file",
            "Proxy", "Proxy",
            "Ingen aktiv tilkobling", "No active connection",
            "Ingen svar", "No reply",
            "svarer ikke", "no reply",
            "klarte ikke slå opp navn", "could not resolve a name",
            "ukjent", "unknown",
            "Ikke i bruk (kablet)", "Not in use (wired)",
            "Finnes ikke", "Missing",
            "Ren", "Clean",
            "{0} omdirigeringer: ", "{0} redirects: ",
            "Ingen", "None",
            "all trafikk går via denne", "all traffic goes through this",
            "Fornyer IP-adresse.", "Renewing IP address.",
            "Nullstiller Winsock.", "Resetting Winsock.",
            "Nullstiller TCP/IP.", "Resetting TCP/IP.",
            "Ferdig. Start maskinen på nytt for at det skal tre i kraft.",
                "Done. Restart the machine for this to take effect.",

            // ---- oppstartsmåling ----
            "Sinker oppstart", "Delays startup",
            "Oppstart tar {0}", "Startup takes {0}",
            "Forsinkelsen er hentet fra Windows' egen måling av de siste oppstartene.",
                "The delay comes from Windows' own measurement of recent startups.",
            "Windows har ikke logget noen oppstart ennå.", "Windows has not logged a startup yet.",
            "Tjeneste", "Service",
            "Vis logg", "Show log",

            // ---- diskplass, nye moduser ----
            "Største mapper og filer", "Largest folders and files",
            "Duplikater", "Duplicates",
            "Glemte filer", "Forgotten files",
            "Største viser hvor plassen ligger. Duplikater finner like filer. Glemte filer er store filer du ikke har rørt på et halvår.",
                "Largest shows where the space sits. Duplicates finds identical files. Forgotten files are big files you have not touched in six months.",
            "Leser gjennom hele treet. Sletter aldri noe.", "Walks the whole tree. Never deletes anything.",
            "Like filer — behold én, slett resten selv", "Identical files — keep one, delete the rest yourself",
            "Store filer du ikke har rørt på lenge", "Big files you have not touched in a long time",
            "Kopier", "Copies",
            "Kan spares", "Recoverable",
            "{0} kan spares", "{0} recoverable",
            "Sist rørt", "Last touched",
            "{0} dager siden", "{0} days ago",
            "Ingen duplikater funnet. Brukte {0} s.", "No duplicates found. Took {0} s.",
            "{0} grupper med like filer. Brukte {1} s.", "{0} groups of identical files. Took {1} s.",
            "{0} filer, til sammen {1}.", "{0} files, {1} in total.",

            // ---- vedlikehold ----
            "Reparasjon og verktøy.", "Repair and tools.",
        };
    }
}
