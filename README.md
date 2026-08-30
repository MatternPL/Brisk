# Brisk

Windows maintenance that does what it says. No paywall, no subscription, no telemetry.

One 330 KB executable. Nothing to install first — it runs on the .NET Framework 4.8
that already ships with Windows 10 and 11.

English by default. Norwegian is one click away, bottom left.

---

## Getting it

Download **`Brisk-Installer.exe`** from
[Releases](https://github.com/MatternPL/Vaktmester/releases).

* Installs to `%LOCALAPPDATA%\Programs\Brisk` — **no UAC prompt to install**
* Start menu shortcut, optional desktop shortcut
* Shows up in Apps & features, uninstalls from there
* Or just run `Brisk.exe` on its own from a USB stick

The files are not code-signed, so SmartScreen will warn you the first time:
*More info → Run anyway*. A certificate costs money every year; this doesn't.

---

## What it does

**Overview** opens with a verdict — *Everything looks fine*, or *3 things are worth a
look* — and a list of what those things are. Double-click a row to go there. One blue
button per page, and the colour tells you what a button does: blue acts, green is safe,
red deletes.

| Page | |
|---|---|
| **Cleanup** | Temp files, Windows Update leftovers, crash dumps, browser and shader caches, system logs. On the machine it was built on: 39 GB. |
| **Disk space** | Four modes. *Largest* shows where the space sits, *Duplicates* finds identical files by content, *Forgotten files* lists big files you haven't touched in six months, and *Space Windows reserves* shows the hibernation file, restore points and page file — usually tens of GB that no cleaner touches. |
| **Startup** | Everything that starts with Windows, with **how many seconds each one adds to boot** — read from Windows' own measurements, not guesswork. |
| **Memory** | What is actually using RAM, and an honest note about why "RAM boosters" don't help. |
| **Health** | Drive wear and temperature, battery capacity on laptops, a **blue screen analyser** that reads the crash dump and names the driver that failed, and **app crashes** — which programs crash, how often, and which module faulted. |
| **Network** | Adapter, gateway, internet, DNS, Wi-Fi signal, and a check of the hosts file and proxy — the two things malware likes to hijack. |
| **Updates** | Drivers and Windows updates from Microsoft's own catalog through the Windows Update API, plus your graphics card and its driver age — with a link to the maker, because Windows Update is always behind on those. |
| **Software** | winget updates, plus every installed program sorted by size, with uninstall. |
| **Maintenance** | sfc, DISM, TRIM, restore point, system report, weekly scheduled cleanup. |

### What it does not do

* **RAM "boosters" are mostly theatre.** Windows uses free RAM as cache on purpose.
  The two buttons under Memory do something real, but rarely help — and the app says so.
  Fewer startup programs is the fix that lasts.
* **No registry cleaning.** It has never produced a measurable speedup.
* **No third-party driver scraping.** Drivers come from Microsoft, signed.
* **No CPU temperature.** That needs a kernel driver. Not worth it.

### Reading a blue screen

Double-click a crash under Health and Brisk parses the kernel dump Windows wrote
(`PAGEDU64`), pulls out the stop code, the loaded module list and the call stack, and
maps the fault address to a module. It then tells you which driver is the likely cause,
where each driver came from, and what to do about it — with a *Copy summary* button for
when you need to ask someone else.

The parsing is self-checking: the stop code and its four parameters must match what
Windows logged, and module 0 must be `ntoskrnl.exe`. If either fails, it says it could
not read the dump rather than guessing.

Crash dumps are therefore **excluded from the automatic cleanup** and unticked by
default — deleting them throws away the evidence. Brisk also reads the real dump path
from the registry rather than assuming the default, since Windows does not always use it.

### Safety

* Cleanup has a hard-coded list of folders that can never be touched: your user folder,
  Documents, Desktop, Pictures, Windows, Program Files and the drive root.
* Files in use are skipped and counted, not forced.
* Windows.old, crash dumps and browser cache are unticked by default and never part of the automatic weekly cleanup.
* Browser cleanup removes cached pages and images only. History, passwords, bookmarks, logins and autofill live in other files and are never touched.
* Startup entries that matter (audio, touchpad, antivirus, password manager) are flagged
  amber and warn before being disabled.
* Duplicates and forgotten files are listed, never deleted for you.
* Everything lands in `%LOCALAPPDATA%\Brisk\brisk.log`.

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
utgivelse.cmd 1.2.0 https://github.com/MatternPL/Vaktmester/releases/download/v1.2.0/Brisk-Installer.exe "What changed"
```

Sets the version in the source, builds, computes the sha256 and writes
`oppdatering.json`. Then upload `Brisk-Installer.exe` as a release asset and commit
`oppdatering.json` to `main` — **in that order**, so nobody sees an update that isn't
downloadable yet.

Clients read `Updater.DefaultManifestUrl` in `src/Updater.cs`.

---

## Command line

| | |
|---|---|
| `Brisk.exe /auto` | Runs the safe cleanup with no window. Used by the scheduled task. |
| `Brisk.exe /side:helse` | Opens a specific page (`oversikt`, `rydding`, `diskplass`, `oppstart`, `minne`, `helse`, `nettverk`, `drivere`, `programmer`, `vedlikehold`, `logg`). |
| `Brisk-Installer.exe /S` | Silent install. Add `/start` to launch afterwards. |
| `Uninstall.exe /uninstall` | Uninstall. Add `/S` for silent. |

---

## Building

```
bygg.cmd
```

No Visual Studio, no NuGet, no SDK — it uses the C# compiler already sitting in
`C:\Windows\Microsoft.NET\Framework64\v4.0.30319`. That compiler only supports C# 5,
so no string interpolation, no `?.`, no `nameof`.

```
src/
  Program.cs          entry point, arguments, error handling
  MainForm*.cs        window and pages
  Lang.cs             English/Norwegian, Norwegian text is the key
  Theme.cs            dark theme, buttons, dark list headers
  Logo.cs Icons.cs    the mark and the sidebar glyphs
  Cleaner.cs          cleanup engine and the do-not-touch list
  StartupTools.cs     startup entries and scheduled tasks
  BootTools.cs        boot time and what delays it, from the event log
  HealthTools.cs      drive wear, blue screens, battery
  AppCrashTools.cs    app crashes from the event log
  SpaceTools.cs       hibernation file, restore points, page file
  DumpTools.cs        kernel crash dump parser
  CrashDialog.cs      the blue screen analysis window
  NetTools.cs         connectivity, hosts file, proxy
  SystemTools.cs      memory, winget, disk health, maintenance
  DriverTools.cs      drivers via Windows Update
  Extras.cs           Windows updates, disk usage, duplicates, uninstall, report
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
