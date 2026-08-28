# Vaktmester

PC-vedlikehold uten tull. Gratis, ingen betalingsmur, ingen abonnement, ingen datainnsamling.

Ett program på ca. 200 KB som kjører på alle Windows 10/11-maskiner uten at noe må
installeres på forhånd — .NET Framework 4.8 ligger allerede i Windows.

---

## Sende til en kompis

Send **`Vaktmester-Installer.exe`**. Den er alt som trengs.

* Installerer i `%LOCALAPPDATA%\Programs\Vaktmester` — **ingen UAC-melding ved installasjon**
* Lager snarvei i Start-menyen og (valgfritt) på skrivebordet
* Dukker opp i «Apper og funksjoner» som et vanlig program
* Avinstalleres derfra, eller med `Avinstaller.exe` i programmappa

Vil du heller ha den bærbare varianten: send `Vaktmester.exe` alene. Den kan kjøres
rett fra en minnepinne uten å installere noe.

**Om SmartScreen:** filene er ikke kodesignert (et sertifikat koster penger i året).
Første gang kan Windows si «PC-en din ble beskyttet». Trykk *Mer info → Kjør likevel*.
Det er samme melding all usignert programvare gir.

---

## Hva programmet faktisk gjør

### Virker, og monner

| Side | Hva den gjør |
|---|---|
| **Rydding** | Sletter ekte søppel: temp-filer, Windows Update-rester, krasjdumper, nettleser-cache, grafikk-cache, systemlogger. På testmaskinen: 39 GB. |
| **Diskplass** | Går gjennom disken og viser hvilke mapper og enkeltfiler som faktisk tar plassen. Sletter ingenting. |
| **Oppstart** | Viser alt som starter med Windows — register, oppstartsmappe og planlagte oppgaver — og lar deg slå av det du ikke trenger. Dette er den største reelle hastighetsgevinsten. |
| **Oppdateringer** | Henter drivere og Windows-oppdateringer direkte fra Microsoft via Windows Update-APIet. Signert og trygt. |
| **Programvare** | Oppdaterer installerte programmer med winget, og lar deg avinstallere det du ikke bruker — sortert etter størrelse. |
| **Vedlikehold** | `sfc /scannow` og DISM reparerer ødelagte systemfiler. TRIM holder SSD-en rask. Gjenopprettingspunkt, systemrapport og planlagt ukentlig rydding. |

### Automatisk oppdatering

Programmet ser etter nye versjoner **høyst én gang i døgnet**, i bakgrunnen. Finner
den en nyere versjon får du en dialog med hva som er nytt og to knapper: *Oppdater nå*
eller *Ikke nå*. Sier du ja lastes den ned og installeres, og programmet starter seg selv
på nytt. Ingenting skjer uten at du har trykket ja.

Kan slås av under *Vedlikehold → Sjekk automatisk*. Der ligger også knappen for å
sjekke manuelt.

**Sikkerhet:** både versjonsfilen og nedlastingen må ligge på `https`. Den nedlastede
filen sjekkes mot en sha256-sum fra versjonsfilen **før** den kjøres. Stemmer den ikke,
slettes filen og ingenting kjøres.

#### Sette opp oppdateringskilden

Klienten henter en liten JSON-fil:

```json
{
  "versjon": "1.1.0",
  "url": "https://.../Vaktmester-Installer.exe",
  "sha256": "cb80b937e954...",
  "storrelse": 288768,
  "notat": "Hva som er nytt i denne versjonen."
}
```

Lag den med:

```bash
utgivelse.cmd 1.1.0 https://github.com/BRUKER/vaktmester/releases/download/v1.1.0/Vaktmester-Installer.exe "Hva som er nytt"
```

Skriptet setter versjonsnummeret i kildekoden, bygger, regner ut sha256 og skriver
`oppdatering.json`. Så laster du opp begge filene.

Adressen klienten sjekker står i `Updater.DefaultManifestUrl` i `src/Updater.cs`.
Den kan også overstyres uten å bygge på nytt:

```powershell
New-ItemProperty HKCU:\Software\Vaktmester -Name OppdateringsUrl -Value "https://din.adresse/oppdatering.json" -Force
```

### Ærlige forbehold

* **«Frigjør RAM» er stort sett bløff** i kommersielle verktøy. Windows bruker ledig
  RAM som cache med vilje — det er sånn det skal være. Knappene under *Minne* gjør
  noe ekte (tømmer arbeidssett / standby-liste), men hjelper bare i spesielle tilfeller,
  og det står tydelig i programmet. Vil du ha varig lavere RAM-bruk: kutt oppstartsprogrammer.
* **«Rense registeret» gir ingen målbar hastighetsgevinst.** Derfor finnes det ikke her.
* **Ingen «driver updater»-svindel.** Drivere kommer fra Microsofts katalog, ikke fra
  tvilsomme nettsider som skanner gratis og tar betalt for å fikse.
* **Nettverk:** programmet snakker med Windows Update, winget og — hvis automatisk
  oppdatering står på — oppdateringskilden din. Ingenting annet. Ingen telemetri.

### Sikkerhetsnett

* Ryddingen har en fast liste over mapper som **aldri** kan slettes — brukermappa,
  Dokumenter, Skrivebord, Bilder, Windows, Program Files og rota på disken.
* Filer som er i bruk hoppes over. Det er normalt, og telles opp i statuslinjen.
* Windows.old er avmerket som standard og må hukes av manuelt.
* Systemnære oppstartsoppføringer (lyd, styreplate, antivirus, passordbehandler)
  er merket i gult, og du får en ekstra advarsel før de slås av.
* Alt som gjøres skrives til `%LOCALAPPDATA%\Vaktmester\vaktmester.log`.

---

## Kommandolinje

| Argument | Hva den gjør |
|---|---|
| `Vaktmester.exe /auto` | Kjører den trygge ryddingen uten vindu. Brukes av den planlagte oppgaven. |
| `Vaktmester.exe /side:rydding` | Åpner en bestemt side direkte (`oversikt`, `rydding`, `diskplass`, `oppstart`, `minne`, `drivere`, `programmer`, `vedlikehold`, `logg`). |
| `Vaktmester-Installer.exe /S` | Stille installasjon, ingen vindu. Legg til `/start` for å kjøre programmet etterpå. |
| `Avinstaller.exe /uninstall` | Avinstallerer med vindu. Legg til `/S` for stille. |

---

## Bygge selv

```
bygg.cmd
```

Det er alt. Ingen Visual Studio, ingen NuGet, ingen SDK — skriptet bruker
C#-kompilatoren som allerede ligger i `C:\Windows\Microsoft.NET\Framework64\v4.0.30319`.

Resultatet blir `Vaktmester.exe` og `Vaktmester-Installer.exe` i rotmappa.

### Filer

```
src/                  Programmet
  Program.cs            Oppstart, argumenter, feilhåndtering
  MainForm*.cs          Vinduet og sidene
  Theme.cs  Logo.cs     Utseende og merke
  Cleaner.cs            Ryddemotoren og sperrelista
  StartupTools.cs       Oppstartsoppføringer og planlagte oppgaver
  SystemTools.cs        Minne, winget, diskhelse, vedlikehold
  DriverTools.cs        Drivere via Windows Update
  Extras.cs             Windows-oppdateringer, diskplass, avinstallering, rapport
  Native.cs             P/Invoke
installer/            Installasjonsprogrammet
tools/                Byggeverktøy og selvtester
```

### Selvtest

`tools/SelfTest.cs` kjører gjennom minne, rydding (kun analyse), oppstart, disker,
problemenheter og winget, og skriver ut ekte tall. Nyttig for å sjekke at en endring
ikke har brukket noe uten å måtte klikke seg gjennom vinduet.
