# Lager en ny utgivelse: setter versjonsnummer, bygger, regner ut sha256
# og skriver oppdatering.json som klientene sjekker mot.
#
#   .\utgivelse.cmd 1.1.0 https://github.com/BRUKER/brisk/releases/download/v1.1.0/BriskInstaller.exe "Hva som er nytt"

param(
    [Parameter(Mandatory = $true)][string]$Versjon,
    [Parameter(Mandatory = $true)][string]$NedlastingsUrl,
    [string]$Notat = ""
)

$ErrorActionPreference = "Stop"
$rot = Split-Path -Parent $PSScriptRoot

if ($Versjon -notmatch '^\d+\.\d+\.\d+$') { throw "Versjon må være på formen 1.2.3" }
if ($NedlastingsUrl -notlike 'https://*') { throw "Nedlastingsadressen må være https" }

Write-Host "== Setter versjon $Versjon ==" -ForegroundColor Cyan

$ai = Join-Path $rot "src\AssemblyInfo.cs"
$t = Get-Content $ai -Raw -Encoding UTF8
$t = $t -replace 'AssemblyVersion\("[\d\.]+"\)', "AssemblyVersion(`"$Versjon.0`")"
$t = $t -replace 'AssemblyFileVersion\("[\d\.]+"\)', "AssemblyFileVersion(`"$Versjon.0`")"
[IO.File]::WriteAllText($ai, $t, (New-Object Text.UTF8Encoding $true))

$inst = Join-Path $rot "installer\Installer.cs"
$t = Get-Content $inst -Raw -Encoding UTF8
$t = $t -replace 'public const string Version = "[\d\.]+";', "public const string Version = `"$Versjon`";"
[IO.File]::WriteAllText($inst, $t, (New-Object Text.UTF8Encoding $true))

Write-Host "== Bygger ==" -ForegroundColor Cyan
Push-Location $rot
try {
    Get-Process Brisk -ErrorAction SilentlyContinue | Stop-Process -Force
    & (Join-Path $rot "bygg.cmd")
    if ($LASTEXITCODE -ne 0) { throw "Bygget feilet" }
} finally { Pop-Location }

$exe = Join-Path $rot "BriskInstaller.exe"

# Regner ut summen med .NET i stedet for Get-FileHash. Den cmdleten mangler
# hvis skriptet startes fra en PowerShell 7-okt med endret PSModulePath.
$sha256 = [Security.Cryptography.SHA256]::Create()
try {
    $fs = [IO.File]::OpenRead($exe)
    try { $sha = ([BitConverter]::ToString($sha256.ComputeHash($fs))).Replace('-', '').ToLower() }
    finally { $fs.Dispose() }
} finally { $sha256.Dispose() }
$size = ([IO.FileInfo]$exe).Length

# Skriver JSON for hånd så vi slipper avhengigheter og får forutsigbar formatering.
function Esc([string]$s) {
    if ($null -eq $s) { return "" }
    $s.Replace('\', '\\').Replace('"', '\"').Replace("`r`n", '\n').Replace("`n", '\n')
}

$json = @"
{
  "versjon": "$Versjon",
  "url": "$(Esc $NedlastingsUrl)",
  "sha256": "$sha",
  "storrelse": $size,
  "notat": "$(Esc $Notat)"
}
"@

$ut = Join-Path $rot "oppdatering.json"
[IO.File]::WriteAllText($ut, $json, (New-Object Text.UTF8Encoding $false))

# Skriver ogsaa teksten som skal staa paa utgivelsen paa GitHub, saa den ikke
# blir en tilfeldig commit-melding. Tittelen skal vaere "Brisk X.Y.Z".
$notatMappe = Join-Path $rot "docs\utgivelser"
if (-not (Test-Path $notatMappe)) { New-Item -ItemType Directory -Path $notatMappe | Out-Null }
$notatFil = Join-Path $notatMappe "$Versjon.md"
$notatTekst = @"
# Brisk $Versjon

### New

-

### Changed

-

### Fixed

-

<!-- Utkast fra manifestet. Del det opp i punktene over, og slett det du ikke
     bruker. Skriv hva brukeren merker, ikke hva som ble endret i koden.
     Denne teksten limes inn i "Describe this release" paa GitHub.

$Notat
-->
"@
[IO.File]::WriteAllText($notatFil, $notatTekst, (New-Object Text.UTF8Encoding $false))

Write-Host ""
Write-Host "== Ferdig ==" -ForegroundColor Green
Write-Host "  Versjon    : $Versjon"
Write-Host "  Installer  : $exe"
Write-Host "  Størrelse  : $([math]::Round($size/1KB,1)) KB"
Write-Host "  sha256     : $sha"
Write-Host "  Manifest   : $ut"
Write-Host "  Utgivelsestekst: $notatFil"
Write-Host ""
Write-Host "Neste steg:" -ForegroundColor Yellow
Write-Host "  1. Last opp BriskInstaller.exe til adressen over."
Write-Host "     Tittel: Brisk $Versjon. Tekst: lim inn fra $notatFil."
Write-Host "  2. Legg oppdatering.json der klientene henter den fra"
Write-Host "     (se Updater.DefaultManifestUrl i src\Updater.cs)."
Write-Host "  3. Klientene oppdager den innen et døgn, eller straks med"
Write-Host "     knappen 'Se etter oppdatering' under Vedlikehold."
