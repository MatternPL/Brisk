# Legge til verktøy, ikoner og knapper

Denne guiden dekker tre ting: å legge til et verktøy på Verktøy-sida, å lage et
nytt ikon, og å legge til en knapp på Vedlikehold.

Verktøy-sida bygges fra én liste i
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
    "https://notepad-plus-plus.org/", "dokument"));
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
| **Ikon** | Nøkkel fra `src/Icons.cs`. Se listen lenger ned. |

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
    "https://github.com/ChrisTitusTech/winutil", "tilpass", true, true));
//                                                          ^^^^  ^^^^
//                                                      Remote  OwnWindow
```

**`Remote`** — sett den når kommandoen henter kode fra nettet og kjører den med
en gang. Da får flisen oransje stripe i stedet for blå, og brukeren får en ekstra
bekreftelse som viser hele kommandoen før noe skjer. Brisk kan ikke se hva som
ligger på den andre siden, og det skal brukeren få vite.

**`OwnWindow`** — sett den når verktøyet åpner sitt eget vindu. Da startes det i
hevet PowerShell, og konsollen sier fra at utdata ikke vises i Brisk. Uten denne
prøver Brisk å fange utdata fra noe som aldri skriver noe, og det ser ut som om
ingenting skjer.

Bruker du den korte `Make(...)` med sju felter, blir begge `false`.

---

## Ikonene

Ikonene tegnes som vektor i [`src/Icons.cs`](../src/Icons.cs), ikke som bildefiler.
Derfor er de skarpe i alle størrelser og følger fargen de blir bedt om.

Disse finnes i dag:

| Nøkkel | Motiv |
|---|---|
| `oversikt` | fire ruter |
| `rydding` | feiestrøk |
| `diskplass` | sektor i en sirkel |
| `oppstart` | av/på-symbol |
| `minne` | brikke med bein |
| `helse` | pulslinje |
| `nettverk` | wifi-buer |
| `oppdateringer` | pil ned i en boks |
| `programvare` | eske |
| `vedlikehold` | skiftenøkkel |
| `logg` | tre linjer |
| `verktoy` | skrunøkkel og skrutrekker i kryss |
| `tilpass` | skyveknapper |
| `sok` | forstørrelsesglass |
| `disk` | plate med nav |
| `temperatur` | termometer |
| `skjold` | skjold |
| `usb` | usb-plugg |
| `nedlasting` | pil ned mot en strek |
| `klokke` | urskive |
| `dokument` | ark med brett |

### Lage et nytt ikon

Legg et `case` i `Icons.Draw`. Alt tegnes i et kvadrat der `x`, `y` er hjørnet og
`s` er sidelengden, og du bruker brøkdeler av `s` så det skalerer:

```csharp
case "nokkel":                  // hva det forestiller
    g.DrawEllipse(p, x + s * 0.10f, y + s * 0.10f, s * 0.80f, s * 0.80f);
    g.DrawLine(p, x + s * 0.50f, y + s * 0.30f, x + s * 0.50f, y + s * 0.70f);
    break;
```

`p` er en penn som allerede har riktig farge og tykkelse, `b` er en pensel i samme
farge. Ikke sett egne farger — ikonet skal ta fargen fra der det brukes.

Hold det til to–fire streker. Ikonet tegnes så lite som 18 piksler, og detaljer
under det blir til grøt.

---

## Legge til en knapp på Vedlikehold

Vedlikehold-sida bygges i `PageMaint()` i
[`src/MainForm.Pages.cs`](../src/MainForm.Pages.cs). Den er delt i tre grupper med
hver sin overskrift. Å legge til noe er tre små endringer:

**1. Lag flisen** — i riktig gruppe:

```csharp
ActionTile tNytt = new ActionTile(L.T("Kort navn"),
    L.T("Én setning om hva knappen gjør og hva den koster deg.")).With("verktoy");
```

`.With("...")` gir ikonet. `.AsPrimary()` gjør tittelen blå, `.AsDanger()` rød
for noe som sletter, `.AsWarn()` gul for noe som advarer.

**2. Legg den i en rad** — radene tar tre fliser hver:

```csharp
Panel r2 = Widgets.Row(98, tComp, tOpt, tNytt);
```

Trenger du en fjerde gruppe, lag en overskrift og en rad til, og husk at
`p.Controls.Add(...)` leses **nedenfra og opp** — det som legges til sist havner
øverst på sida.

**3. Koble klikket** — og ta flisen med i `all`, så den slås av mens noe kjører:

```csharp
Control[] all = new Control[] { tSfc, tDism, tRp, tComp, tOpt, tNytt, ... };

tNytt.Click += async delegate
{
    await Job(all, delegate { MaintenanceTools.GjorNoe(w); });
};
```

`Job(...)` slår av knappene, viser framdriftslinja og fanger feil. `w` sender hver
linje til utdatafeltet nederst. Bruk den — kjører du noe utenfor `Job`, fryser
vinduet mens det står på.

Beskrivelsene går gjennom `L.T`, så kjør `python tools\sprak_sjekk.py` etterpå.

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
