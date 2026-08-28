# -*- coding: utf-8 -*-
# Korter ned tekstene i motorfilene og pakker dem i L.T().
import io, sys

ROT = r"C:\Users\Mathias\Desktop\Vaktmester\src"


def load(f):
    return io.open(ROT + "\\" + f, encoding="utf-8").read()


def save(f, s):
    io.open(ROT + "\\" + f, "w", encoding="utf-8").write(s)


def rep(s, old, new, required=True):
    if old not in s:
        if required:
            print("IKKE FUNNET i " + rep.f + ":", old[:80].replace("\n", " "))
            sys.exit(1)
        return s
    return s.replace(old, new, 1)


# ================= Cleaner.cs =================
rep.f = "Cleaner.cs"
s = load("Cleaner.cs")

start = s.index("        public static List<CleanTarget> BuildTargets()")
end = s.index("        // ---------------------------------------------------------------\n"
              "        // Loeser opp stjerne-ledd")
ny = '''        public static List<CleanTarget> BuildTargets()
        {
            List<CleanTarget> t = new List<CleanTarget>();

            t.Add(new CleanTarget("Midlertidige filer (bruker)",
                "%TEMP%. Rester etter installasjoner og programmer.")
                .Dir("%LOCALAPPDATA%\\\\Temp"));

            t.Add(new CleanTarget("Midlertidige filer (Windows)",
                "C:\\\\Windows\\\\Temp.")
                .Dir("%SystemRoot%\\\\Temp"));

            CleanTarget rb = new CleanTarget("Papirkurv", "Alle disker.");
            rb.Special = true; rb.SpecialKey = "recyclebin";
            t.Add(rb);

            t.Add(new CleanTarget("Windows Update-nedlastinger",
                "Ferdig installerte pakker. Lastes ned igjen hvis det trengs.")
                .Dir("%SystemRoot%\\\\SoftwareDistribution\\\\Download"));

            t.Add(new CleanTarget("Delivery Optimization",
                "Oppdateringer mellomlagret for deling på nettverket.")
                .Dir("%SystemRoot%\\\\ServiceProfiles\\\\NetworkService\\\\AppData\\\\Local\\\\Microsoft\\\\Windows\\\\DeliveryOptimization"));

            t.Add(new CleanTarget("Krasjdumper",
                "Dumpfiler fra kræsj og blåskjerm.")
                .Dir("%LOCALAPPDATA%\\\\CrashDumps")
                .Dir("%SystemRoot%\\\\Minidump")
                .Files("%SystemRoot%", "MEMORY.DMP"));

            t.Add(new CleanTarget("Feilrapportering",
                "Rapporter som lå i kø til Microsoft.")
                .Dir("%LOCALAPPDATA%\\\\Microsoft\\\\Windows\\\\WER")
                .Dir("%ProgramData%\\\\Microsoft\\\\Windows\\\\WER\\\\ReportQueue")
                .Dir("%ProgramData%\\\\Microsoft\\\\Windows\\\\WER\\\\ReportArchive"));

            t.Add(new CleanTarget("Systemlogger",
                "CBS, Windows Update og DISM. Blir fort hundrevis av MB.")
                .Files("%SystemRoot%\\\\Logs\\\\CBS", "*.log")
                .Files("%SystemRoot%\\\\Logs\\\\CBS", "*.cab")
                .Dir("%SystemRoot%\\\\Logs\\\\WindowsUpdate")
                .Files("%SystemRoot%\\\\Logs\\\\DISM", "*.log")
                .Files("%SystemRoot%", "*.log"));

            t.Add(new CleanTarget("Miniatyrbilde- og ikon-cache",
                "Bygges opp igjen. Fikser gale miniatyrbilder.")
                .Files("%LOCALAPPDATA%\\\\Microsoft\\\\Windows\\\\Explorer", "thumbcache_*.db")
                .Files("%LOCALAPPDATA%\\\\Microsoft\\\\Windows\\\\Explorer", "iconcache_*.db"));

            t.Add(new CleanTarget("Grafikk-cache",
                "Kompilerte shadere fra DirectX, NVIDIA og AMD. Bygges opp igjen.")
                .Dir("%LOCALAPPDATA%\\\\D3DSCache")
                .Dir("%LOCALAPPDATA%\\\\NVIDIA\\\\DXCache")
                .Dir("%LOCALAPPDATA%\\\\NVIDIA\\\\GLCache")
                .Dir("%APPDATA%\\\\NVIDIA\\\\ComputeCache")
                .Dir("%LOCALAPPDATA%\\\\AMD\\\\DxCache")
                .Dir("%LOCALAPPDATA%\\\\AMD\\\\DxcCache"));

            t.Add(new CleanTarget("Nettleser-cache",
                "Bare cache. Passord, bokmerker og innlogginger røres ikke.")
                .Dir("%LOCALAPPDATA%\\\\Google\\\\Chrome\\\\User Data\\\\*\\\\Cache")
                .Dir("%LOCALAPPDATA%\\\\Google\\\\Chrome\\\\User Data\\\\*\\\\Code Cache")
                .Dir("%LOCALAPPDATA%\\\\Google\\\\Chrome\\\\User Data\\\\*\\\\GPUCache")
                .Dir("%LOCALAPPDATA%\\\\Microsoft\\\\Edge\\\\User Data\\\\*\\\\Cache")
                .Dir("%LOCALAPPDATA%\\\\Microsoft\\\\Edge\\\\User Data\\\\*\\\\Code Cache")
                .Dir("%LOCALAPPDATA%\\\\Microsoft\\\\Edge\\\\User Data\\\\*\\\\GPUCache")
                .Dir("%LOCALAPPDATA%\\\\BraveSoftware\\\\Brave-Browser\\\\User Data\\\\*\\\\Cache")
                .Dir("%LOCALAPPDATA%\\\\Vivaldi\\\\User Data\\\\*\\\\Cache")
                .Dir("%APPDATA%\\\\Opera Software\\\\Opera Stable\\\\Cache")
                .Dir("%LOCALAPPDATA%\\\\Mozilla\\\\Firefox\\\\Profiles\\\\*\\\\cache2")
                .Dir("%LOCALAPPDATA%\\\\Microsoft\\\\Windows\\\\INetCache\\\\IE"));

            t.Add(new CleanTarget("App-cache",
                "Discord, Spotify, Teams, Slack og Office.")
                .Dir("%APPDATA%\\\\discord\\\\Cache")
                .Dir("%APPDATA%\\\\discord\\\\Code Cache")
                .Dir("%APPDATA%\\\\discord\\\\GPUCache")
                .Dir("%LOCALAPPDATA%\\\\Spotify\\\\Data")
                .Dir("%APPDATA%\\\\Spotify\\\\Data")
                .Dir("%APPDATA%\\\\Microsoft\\\\Teams\\\\Cache")
                .Dir("%LOCALAPPDATA%\\\\Slack\\\\Cache")
                .Dir("%LOCALAPPDATA%\\\\Microsoft\\\\Office\\\\16.0\\\\OfficeFileCache"));

            CleanTarget wold = new CleanTarget("Windows.old",
                "Rester etter oppgradering, ofte 10–30 GB. Sletting fjerner muligheten til å rulle tilbake.");
            wold.Risk = Risk.Merk;
            wold.DefaultChecked = false;
            wold.DirAndSelf("%SystemDrive%\\\\Windows.old");
            wold.DirAndSelf("%SystemDrive%\\\\$Windows.~BT");
            wold.DirAndSelf("%SystemDrive%\\\\$Windows.~WS");
            t.Add(wold);

            t.Add(new CleanTarget("Oppsettsrester",
                "Panther-logger etter store Windows-oppdateringer.")
                .Dir("%SystemRoot%\\\\Panther")
                .Dir("%SystemRoot%\\\\SoftwareDistribution\\\\DataStore\\\\Logs"));

            return t;
        }

'''
s = s[:start] + ny + s[end:]
save("Cleaner.cs", s)
print("Cleaner.cs: kategorier kortet ned")

# ================= StartupTools.cs =================
rep.f = "StartupTools.cs"
s = load("StartupTools.cs")
s = rep(s, 'case StartupKind.RegistryHKCU: return "Register (denne brukeren)";',
        'case StartupKind.RegistryHKCU: return L.T("Register (denne brukeren)");')
s = rep(s, 'case StartupKind.RegistryHKLM: return "Register (alle brukere)";',
        'case StartupKind.RegistryHKLM: return L.T("Register (alle brukere)");')
s = rep(s, 'case StartupKind.RegistryHKLM32: return "Register (32-bit)";',
        'case StartupKind.RegistryHKLM32: return L.T("Register (32-bit)");')
s = rep(s, 'case StartupKind.Folder: return "Oppstartsmappe";',
        'case StartupKind.Folder: return L.T("Oppstartsmappe");')
s = rep(s, 'default: return "Planlagt oppgave";', 'default: return L.T("Planlagt oppgave");')
s = rep(s, 'if (hay.IndexOf(row[0], StringComparison.Ordinal) >= 0) return row[1];',
        'if (hay.IndexOf(row[0], StringComparison.Ordinal) >= 0) return L.T(row[1]);')
s = rep(s, '"Skysynkronisering - filer slutter aa synkes"', '"Skysynkronisering"')
save("StartupTools.cs", s)
print("StartupTools.cs: sprakssatt")

# ================= SystemTools.cs =================
rep.f = "SystemTools.cs"
s = load("SystemTools.cs")
s = rep(s, '''                note = "Fant ingen oppdateringsliste fra winget. Enten er alt oppdatert, " +
                       "eller så må winget-kildene godkjennes én gang i et vanlig terminalvindu.";''',
        '''                note = L.T("Fant ingen liste fra winget.");''')
s = rep(s, 'if (cId < 0 || cVer < 0 || cAvail < 0) { note = "Klarte ikke tolke winget-utdata."; return list; }',
        'if (cId < 0 || cVer < 0 || cAvail < 0) { note = L.T("Klarte ikke tolke winget-utdata."); return list; }')
s = rep(s, '''            if (list.Count == 0 && note.Length == 0)
                note = "Alle programmer winget kjenner til er allerede oppdatert.";''',
        '''            if (list.Count == 0 && note.Length == 0)
                note = L.T("Alt winget kjenner til er oppdatert.");''')
s = rep(s, 'onLine("Kjører sfc /scannow — dette tar 5–15 minutter …");',
        'onLine(L.T("Kjører sfc /scannow. Tar 5–15 minutter."));')
s = rep(s, 'onLine("Kjører DISM /RestoreHealth — dette kan ta lang tid …");',
        'onLine(L.T("Kjører DISM /RestoreHealth. Kan ta lang tid."));')
s = rep(s, 'onLine("Rydder i komponentlageret (WinSxS). Kan frigjøre flere GB …");',
        'onLine(L.T("Rydder komponentlageret (WinSxS)."));')
s = rep(s, 'onLine("Optimaliserer " + letter + " (TRIM på SSD, defrag på HDD) …");',
        'onLine(L.F("Optimaliserer {0}", letter));')
s = rep(s, 'onLine("Tømmer DNS-cache …");', 'onLine(L.T("Tømmer DNS-cache."));')
s = rep(s, 'onLine("Oppretter systemgjenopprettingspunkt …");',
        'onLine(L.T("Oppretter gjenopprettingspunkt."));')
save("SystemTools.cs", s)
print("SystemTools.cs: sprakssatt")

# ================= DriverTools.cs =================
rep.f = "DriverTools.cs"
s = load("DriverTools.cs")
for no in ["Enheten er ikke riktig konfigurert",
           "Driveren kan være ødelagt, eller systemet er tomt for minne",
           "Enheten kan ikke starte",
           "Finner ikke nok ledige ressurser",
           "Krever omstart for å virke",
           "Driveren må installeres på nytt",
           "Registeret er skadet for denne enheten",
           "Windows fjerner enheten",
           "Enheten er deaktivert",
           "Enheten er ikke til stede eller virker ikke",
           "Driveren er ikke installert",
           "Windows finner ikke driver som virker",
           "Enheten er ikke koblet til nå"]:
    s = rep(s, 'return "%s";' % no, 'return L.T("%s");' % no)
s = rep(s, 'default: return "Feilkode " + code;', 'default: return L.F("Feilkode {0}", code);')
s = rep(s, 'if (string.IsNullOrEmpty(d.Name)) d.Name = "(ukjent enhet)";',
        'if (string.IsNullOrEmpty(d.Name)) d.Name = L.T("(ukjent enhet)");')
s = rep(s, '''                    note = "Windows Update har ingen nye drivere til denne maskinen akkurat nå. " +
                           "Det betyr som regel at driverne allerede er oppdaterte.";''',
        '''                    note = L.T("Ingen nye drivere fra Windows Update.");''')
s = rep(s, 'note = "Driversøket feilet: " + ex.Message;', 'note = L.T("Driversøket feilet: ") + ex.Message;')
s = rep(s, 'if (progress != null) progress("Laster ned " + coll.Count + " driver(e)…");',
        'if (progress != null) progress(L.F("Laster ned {0} driver(e).", coll.Count));')
s = rep(s, 'if (progress != null) progress("Nedlasting ferdig (kode " + Convert.ToString(dres.ResultCode) + ").");',
        'if (progress != null) progress(L.T("Nedlasting ferdig."));')
s = rep(s, 'if (progress != null) progress("Ingen drivere ble lastet ned.");',
        'if (progress != null) progress(L.T("Ingen drivere ble lastet ned."));')
s = rep(s, 'if (progress != null) progress("Installerer…");',
        'if (progress != null) progress(L.T("Installerer."));')
s = rep(s, 'if (progress != null) progress("Installasjon feilet: " + ex.Message);',
        'if (progress != null) progress(L.T("Installasjon feilet: ") + ex.Message);')
save("DriverTools.cs", s)
print("DriverTools.cs: sprakssatt")

# ================= Extras.cs =================
rep.f = "Extras.cs"
s = load("Extras.cs")
s = rep(s, '                    note = "Windows er oppdatert — ingen ventende oppdateringer.";',
        '                    note = L.T("Windows er oppdatert.");')
s = rep(s, 'note = "Oppdateringssøket feilet: " + ex.Message;',
        'note = L.T("Oppdateringssøket feilet: ") + ex.Message;')
s = rep(s, 'if (progress != null) progress("Laster ned " + coll.Count + " oppdatering(er) …");',
        'if (progress != null) progress(L.F("Laster ned {0} oppdatering(er).", coll.Count));')
s = rep(s, 'if (ready.Count == 0) { if (progress != null) progress("Ingenting ble lastet ned."); return 0; }',
        'if (ready.Count == 0) { if (progress != null) progress(L.T("Ingenting ble lastet ned.")); return 0; }')
s = rep(s, 'if (progress != null) progress("Installerer …");',
        'if (progress != null) progress(L.T("Installerer."));')
s = rep(s, 'if (progress != null) progress("Installasjon feilet: " + ex.Message);',
        'if (progress != null) progress(L.T("Installasjon feilet: ") + ex.Message);')
save("Extras.cs", s)
print("Extras.cs: sprakssatt")

# ================= Updater.cs =================
rep.f = "Updater.cs"
s = load("Updater.cs")
for no in ["Oppdateringsadressen må være https.",
           "Versjonsfilen kunne ikke tolkes.",
           "Nedlastingsadressen i versjonsfilen må være https.",
           "Versjonsfilen mangler en gyldig sha256-sjekksum. Avbryter.",
           "Sjekksummen stemte ikke. Filen ble slettet — ingenting er kjørt."]:
    s = rep(s, 'error = "%s";' % no, 'error = L.T("%s");' % no)
s = rep(s, 'error = "Fikk ikke kontakt med oppdateringskilden: " + ex.Message;',
        'error = L.T("Fikk ikke kontakt med oppdateringskilden: ") + ex.Message;')
s = rep(s, 'error = "Nedlastingen feilet: " + ex.Message;',
        'error = L.T("Nedlastingen feilet: ") + ex.Message;')
s = rep(s, 'error = "Klarte ikke starte installasjonen: " + ex.Message;',
        'error = L.T("Klarte ikke starte installasjonen: ") + ex.Message;')
save("Updater.cs", s)
print("Updater.cs: sprakssatt")

print("Ferdig.")
