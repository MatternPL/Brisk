# Legge til verktøy på Verktøy-sida

Alt som ligger på Verktøy-sida kommer fra én liste i
[`src/ExternalTools.cs`](../src/ExternalTools.cs). Du trenger ikke røre noe annet
enn den fila og språkfila for å legge til et nytt verktøy.

---

## Kort versjon

1. Legg en `l.Add(Make(...))`-blokk i `ExternalTools.All()`
2. Legg den engelske oversettelsen av beskrivelsen i `src/Lang.cs`
3. Kjør `python tools\sprak_sjekk.py` — den sier fra hvis du glemte noe
4. Kjør `bygg.cmd`

---

## Steg 1 — legg til verktøyet

Åpne `src/ExternalTools.cs` og legg til en blokk i `All()`:

```csharp
l.Add(Make("Notepad++", "Don Ho", "GPLv3",
    "Tekstredigerer som åpner store filer uten å henge.",
    "winget install Notepad++.Notepad++",
    "https://notepad-plus-plus.org/"));
```

Feltene, i rekkefølge:

| Felt | Hva det er |
|---|---|
| **Navn** | Står øverst på flisen. Oversettes ikke. |
| **Laget av** | Vises nederst på flisen, med lav kontrast. |
| **Lisens** | `MIT`, `GPLv3`, `Gratis`, `Gratis til privat bruk` … Bruker du en ny lisenstekst, må den oversettes som alt annet. |
| **Beskrivelse** | **Én kort setning.** Flisen har plass til omtrent 60 tegn på tre linjer. Skriv hva det gjør, ikke hva det er. |
| **Kommando** | Det som faktisk kjøres. |
| **Nettadresse** | Prosjektsiden. |

### Beskrivelsen

Dette er den som avgjør om noen skjønner hva verktøyet er til for. Skriv den
som om du forklarer til en kompis som ikke kjenner programmet.

Bra: *«Ser hvilken prosess som låser en fil.»*
Dårlig: *«Avansert prosessovervåkingsverktøy med DLL-visning og håndtakssøk.»*

---

## Steg 2 — finn riktig winget-ID

Ikke gjett på ID-en. Slå den opp:

```bash
winget search notepad++
```

Bekreft at den finnes nøyaktig slik du skrev den:

```bash
winget show --id Notepad++.Notepad++ --exact
```

Svarer den `Found …`, er ID-en riktig. Svarer den ingenting, er den feil.

Brisk legger selv på `--accept-source-agreements --accept-package-agreements
--disable-interactivity` når kommandoen kjøres. Uten dem ville `winget` stått og
ventet på et svar, siden Brisk fanger utdata og ikke har noe tastatur å svare med.
Flisen viser den korte kommandoen; konsollen viser den fulle.

---

## Steg 3 — oversettelsen

Beskrivelsen går gjennom oversettingstabellen. Åpne `src/Lang.cs` og legg til et
par i blokken merket `// ---- verktøy-sida ----`:

```csharp
"Tekstredigerer som åpner store filer uten å henge.",
    "Text editor that opens huge files without freezing.",
```

Norsk står først — den er nøkkelen. Engelsk er oppslaget.

Kjør så:

```bash
python tools\sprak_sjekk.py
```

Den skriver `Ingen manglende oversettelser.` når du er i mål. Glemte du noe,
skriver den ut ferdige linjer du kan lime rett inn.

---

## Verktøy som ikke bruker winget

To ekstra brytere finnes, og de settes ved å bruke den lange `Make(...)`:

```csharp
l.Add(Make("WinUtil", "Chris Titus Tech", "MIT",
    "Rydder Windows og fjerner apper du ikke ba om.",
    "irm https://christitus.com/win | iex",
    "https://github.com/ChrisTitusTech/winutil", true, true));
//                                              ^^^^  ^^^^
//                                          Remote  OwnWindow
```

**`Remote`** — sett den når kommandoen henter kode fra nettet og kjører den med
en gang. Da får flisen oransje stripe i stedet for blå, og brukeren får en ekstra
bekreftelse som viser hele kommandoen før noe skjer. Brisk kan ikke se hva som
ligger på den andre siden, og det skal brukeren få vite.

**`OwnWindow`** — sett den når verktøyet åpner sitt eget vindu. Da startes det i
hevet PowerShell, og konsollen sier fra at utdata ikke vises i Brisk. Uten denne
prøver Brisk å fange utdata fra noe som aldri skriver noe, og det ser ut som om
ingenting skjer.

Bruker du den korte `Make(...)` med seks felter, blir begge `false`.

---

## Regler som ikke bør brytes

**Kommandoen skal alltid være synlig.** Brukeren skal se hva som kjøres før det
kjøres. Det er hele grunnen til at sida ser ut som den gjør.

**Ikke legg inn noe som omgår lisenser.** Aktiveringsskript og liknende hører
ikke hjemme her. De får Brisk flagget som skadevare av Defender, får repoet tatt
ned av GitHub, og stenger døra til winget og Microsoft Store.

**Ikke legg inn noe du ikke har sett på.** Sjekk at prosjektet er ekte, at
lisensen stemmer, og at det fortsatt vedlikeholdes.

**Hold lista kort.** Ti gode verktøy er mer nyttig enn femti. Er du i tvil om et
verktøy fortjener plass, gjør det sannsynligvis ikke det.

---

## Sjekkliste før du committer

- [ ] `winget show --id <ID> --exact` finner pakken
- [ ] Beskrivelsen er én kort setning
- [ ] `python tools\sprak_sjekk.py` sier ingen manglende oversettelser
- [ ] `bygg.cmd` går gjennom uten feil
- [ ] Du har åpnet Verktøy-sida og sett at flisen ser riktig ut
- [ ] Du har trykket Kjør én gang og sett at konsollen viser noe fornuftig
