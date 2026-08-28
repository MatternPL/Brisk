# Vaktmester

Windows maintenance that does what it says. No paywall, no subscription, no telemetry.

One 280 KB executable. Nothing to install first — it runs on the .NET Framework 4.8
that already ships with Windows 10 and 11.

English by default. Norwegian is one click away, bottom left.

---

## Getting it

Download **`Vaktmester-Installer.exe`** from
[Releases](https://github.com/MatternPL/Vaktmester/releases).

* Installs to `%LOCALAPPDATA%\Programs\Vaktmester` — **no UAC prompt to install**
* Start menu shortcut, optional desktop shortcut
* Shows up in Apps & features, uninstalls from there
* Or just run `Vaktmester.exe` on its own from a USB stick

The files are not code-signed, so SmartScreen will warn you the first time:
*More info → Run anyway*. A certificate costs money every year; this doesn't.

---

## What it does

| Page | |
|---|---|
| **Overview** | Four numbers and a list of things worth acting on. Double-click a row to jump there. |
| **Cleanup** | Temp files, Windows Update leftovers, crash dumps, browser and shader caches, system logs. On the machine it was built on: 39 GB. |
| **Disk space** | Walks the drive and shows which folders and files actually hold the space. Deletes nothing. |
| **Startup** | Everything that starts with Windows — registry, startup folder, scheduled tasks. Reversible, same mechanism as Task Manager. |
| **Updates** | Drivers and Windows updates from Microsoft's own catalog through the Windows Update API. |
| **Software** | winget updates, plus every installed program sorted by size, with uninstall. |
| **Maintenance** | sfc, DISM, TRIM, restore point, system report, weekly scheduled cleanup. |

### What it does not do

* **RAM "boosters" are mostly theatre.** Windows uses free RAM as cache on purpose.
  The two buttons under Memory do something real, but rarely help — and the app says so.
  Fewer startup programs is the fix that lasts.
* **No registry cleaning.** It has never produced a measurable speedup.
* **No third-party driver scraping.** Drivers come from Microsoft, signed.

### Safety

* Cleanup has a hard-coded list of folders that can never be touched: your user folder,
  Documents, Desktop, Pictures, Windows, Program Files and the drive root.
* Files in use are skipped and counted, not forced.
* Windows.old is unticked by default.
* Startup entries that matter (audio, touchpad, antivirus, password manager) are flagged
  amber and warn before being disabled.
* Everything lands in `%LOCALAPPDATA%\Vaktmester\vaktmester.log`.

---

## Updating itself

Checks at most once a day, in the background. If there is a newer version you get a
dialog with the release notes and two buttons. Nothing happens unless you say yes.
Turn it off under *Maintenance → Automatic*.

Both the version file and the download must be `https`, and the download is verified
against a sha256 from the version file **before** it is executed. Mismatch means the
file is deleted and nothing runs.

### Publishing a release

```bash
utgivelse.cmd 1.1.0 https://github.com/MatternPL/Vaktmester/releases/download/v1.1.0/Vaktmester-Installer.exe "What changed"
```

Sets the version in the source, builds, computes the sha256 and writes
`oppdatering.json`. Then upload `Vaktmester-Installer.exe` as a release asset and
commit `oppdatering.json` to `main`.

Clients read `Updater.DefaultManifestUrl` in `src/Updater.cs`. It can be pointed
elsewhere without rebuilding:

```powershell
New-ItemProperty HKCU:\Software\Vaktmester -Name OppdateringsUrl -Value "https://your.host/oppdatering.json" -Force
```

---

## Command line

| | |
|---|---|
| `Vaktmester.exe /auto` | Runs the safe cleanup with no window. Used by the scheduled task. |
| `Vaktmester.exe /side:cleanup` | Opens a specific page (`oversikt`, `rydding`, `diskplass`, `oppstart`, `minne`, `drivere`, `programmer`, `vedlikehold`, `logg`). |
| `Vaktmester-Installer.exe /S` | Silent install. Add `/start` to launch afterwards. |
| `Avinstaller.exe /uninstall` | Uninstall. Add `/S` for silent. |

---

## Building

```
bygg.cmd
```

That's it. No Visual Studio, no NuGet, no SDK — it uses the C# compiler already sitting
in `C:\Windows\Microsoft.NET\Framework64\v4.0.30319`. That compiler only supports C# 5,
so no string interpolation, no `?.`, no `nameof`.

```
src/
  Program.cs          entry point, arguments, error handling
  MainForm*.cs        window and pages
  Lang.cs             English/Norwegian, Norwegian text is the key
  Theme.cs Logo.cs    dark theme and the mark
  Cleaner.cs          cleanup engine and the do-not-touch list
  StartupTools.cs     startup entries and scheduled tasks
  SystemTools.cs      memory, winget, disk health, maintenance
  DriverTools.cs      drivers via Windows Update
  Extras.cs           Windows updates, disk usage, uninstall, report
  Updater.cs          self-update
  Native.cs           P/Invoke
installer/            the installer
tools/                build helpers and self-tests
```

`tools/SelfTest.cs` walks memory, cleanup (measure only), startup, disks, problem
devices and winget, and prints real numbers. `tools/sprak_sjekk.py` checks that every
`L.T()` key in the source has an English translation.

## Licence

MIT.
