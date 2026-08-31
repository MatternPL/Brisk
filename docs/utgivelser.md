# Slik lager jeg en utgivelse

## Framgangsmåte

```
utgivelse.cmd 1.6.4 https://github.com/MatternPL/Brisk/releases/download/v1.6.4/BriskInstaller.exe "Kort sammendrag"
```

Det skriptet setter versjonen i `src/AssemblyInfo.cs` og `installer/Installer.cs`,
bygger, regner ut sha256 av `BriskInstaller.exe`, skriver `oppdatering.json` og
lager et utkast til utgivelsestekst i `docs/utgivelser/<versjon>.md`.

Så, **i denne rekkefølgen**:

1. Fyll ut `docs/utgivelser/<versjon>.md`.
2. Commit versjonsendringen og teksten — men **ikke** `oppdatering.json` ennå.
3. Lag utgivelsen og last opp **begge** filene:
   ```
   gh release create v1.6.4 BriskInstaller.exe Brisk.exe --title "Brisk 1.6.4" --notes-file docs/utgivelser/1.6.4.md
   ```
   `Brisk.exe` skal alltid være med. Den er alternativet for dem som ikke vil
   kjøre en installatør, og den blir sjeldnere feilmeldt av antivirus enn en
   selvutpakkende fil.
4. Last ned fila fra utgivelses-URLen og sjekk at sha256 stemmer med manifestet.
5. Først nå: commit og push `oppdatering.json`.
6. Dra `BriskInstaller.exe` inn på [virustotal.com](https://www.virustotal.com/gui/home/upload)
   og oppdater hash-en i README-en under «Is it safe?». Opplasting krever ingen
   konto. Uten dette peker lenka i README-en på en side som sier «not found»,
   og da er den verre enn ingen lenke.

Rekkefølgen er ikke pedanteri. Manifestet er det klientene leser; pusher du det
først, peker det på en nedlasting som ikke finnes ennå. Det skjedde i 1.6.0.

Installatøren er **ikke byte-identisk mellom to bygg**. Bygger du på nytt etter at
sha256 er regnet ut, stemmer ikke summen lenger, og klienten avviser nedlastingen.
Last opp nøyaktig den fila skriptet regnet på.

## Regler for utgivelsesteksten

Teksten limes inn i «Describe this release» på GitHub. Den skal se lik ut hver gang:

- Tittel: `Brisk X.Y.Z` — ikke `vX.Y.Z`, ikke en commit-tittel
- Språk: engelsk, som resten av programmet
- Skriv hva brukeren merker, ikke hva som ble endret i koden
- Ingen commit-meldinger. De er skrevet til meg selv, ikke til den som laster ned
- Seksjonene er `### New`, `### Changed`, `### Fixed`. Slett de du ikke bruker
- Er utgivelsen viktig å installere, si det i én setning øverst og hvorfor

Tidligere tekster ligger i `docs/utgivelser/` og på utgivelsessidene på GitHub.

## Etter utgivelsen

Klientene ser etter ny versjon hver gang de starter, så den når ut med det samme.
`raw.githubusercontent.com` har noen minutters forsinkelse på endringer i
`oppdatering.json`; det er normalt, ikke en feil.
