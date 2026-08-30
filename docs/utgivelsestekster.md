# Utgivelsestekster

Teksten som skal stå på hver utgivelse på GitHub. Lim inn i beskrivelsesfeltet
når du redigerer utgivelsen.

Fra og med 1.5.0 lager `utgivelse.cmd` fila selv, i `docs/utgivelser/<versjon>.md`,
med seksjonene ferdig satt opp og manifest-teksten som utkast i en kommentar.
Du fyller ut punktene og limer hele fila inn i «Describe this release».

**Regler for disse tekstene**, så de blir like:

- Tittel: `Brisk X.Y.Z` — ikke `vX.Y.Z`, ikke en commit-tittel
- Språk: engelsk, som resten av programmet
- Skriv hva brukeren merker, ikke hva som ble endret i koden
- Ingen commit-meldinger. De er skrevet til meg selv, ikke til den som laster ned

---

## Brisk 1.4.0

```markdown
### New

- **Tools** — a new page with good free tools by other people: WinUtil, PowerToys,
  Process Explorer, Autoruns, CrystalDiskInfo, HWiNFO, O&O ShutUp10++, Everything,
  Rufus, 7-Zip and CPU-Z.
- One click does the whole job: Brisk checks whether the tool is already installed,
  installs it only if it is missing, and then opens it.
- The command is always shown before it runs, output appears in a console on the
  page, and a Stop button ends it.

### Changed

- **Maintenance** was reorganised. Check for update now sits in its own card at the
  top, with the version and when it was last checked. The rest is grouped under
  Repair, Disk and network, and Automation and help.
- Every tile has an icon.

### Fixed

- **Health showed 0 % wear on every drive.** Windows reports nothing useful for
  most NVMe drives, and Brisk repeated it. It now reads the health log from the
  drive itself. On the machine this was found on, two drives that both read 0 %
  turned out to be 1 % and 5 % used.
```

---

## Brisk 1.3.2

```markdown
### Fixed

- **Scanning in Updates crashed** on any machine that actually had pending updates.
  Windows Update leaves the severity field empty on updates that are not security
  fixes, and Brisk read it without checking. Every value read from Windows Update
  now goes through one place that cannot return nothing.

This did not affect machines with no updates waiting, which is why it looked fine
during testing.
```

---

## Brisk 1.3.1

```markdown
### Fixed

- **The install, update and uninstall windows had no visible buttons.** They were
  drawn outside the window and could not be clicked. This was present in every
  version since 1.0.0, so the update prompt has never actually worked — if you got
  here, you downloaded the installer yourself.
- Enter and Escape now work in those windows, so a layout mistake can never lock
  them again.

**If you are on 1.3.0 or older you have to download the installer once by hand.**
Updating from inside the program was not possible before this version.
```

---

## Brisk 1.3.0

```markdown
### Changed

- **The interface is rebuilt.** Options that used to hide in dropdowns are now
  visible tabs, and every button says on the button itself what it does and what
  it will touch.
- Disk space, Health and Updates were reorganised so nothing is buried a click away.

### New

- New app icon, drawn as vector so it stays sharp from 16 to 256 pixels.
```

---

## Brisk 1.2.0

Denne står allerede riktig på GitHub. Den er malen for de andre.
