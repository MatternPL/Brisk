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

# ---------------------------------------------------------------------------
# Notatet havner ordrett i oppdatering.json og vises i oppdateringsvinduet
# akkurat slik det står. Det går ikke gjennom L.T(), for det er ikke en
# grensesnittstreng - det er data som følger utgivelsen. Derfor MÅ det være
# engelsk: appen er engelsk som standard, og norsk er et valg.
#
# Dette gikk galt i 1.7.1 til 1.7.3, der notatet ble stående på norsk i et
# engelsk grensesnitt. Ingenting fanget det opp, fordi teksten bare ble kopiert
# videre. Nå gjør denne sperren det.
# ---------------------------------------------------------------------------
if ($Notat.Length -gt 0) {
    $norsk = @('paa', 'ikke', 'naar', 'vinduet', 'skjermen', 'maskinen', 'brukeren',
               'oppdatering', 'innstilling', 'aapner', 'ogsaa', 'slaa', 'seg', 'som',
               'det', 'den', 'blir', 'ble', 'kan', 'har')
    $ord = [regex]::Matches($Notat.ToLower(), '[a-zæøå]+') | ForEach-Object { $_.Value }
    $treff = @($ord | Where-Object { $norsk -contains $_ } | Select-Object -Unique)

    if ($Notat -match '[æøåÆØÅ]') {
        throw "Notatet inneholder æ, ø eller å. Det vises i oppdateringsvinduet og skal være på engelsk. Ingen utgivelse er laget."
    }
    if ($treff.Count -ge 2) {
        throw "Notatet ser norsk ut (fant: $($treff -join ', ')). Det vises i oppdateringsvinduet og skal være på engelsk. Ingen utgivelse er laget."
    }
    if ($Notat.Length -lt 80) {
        throw "Notatet er bare $($Notat.Length) tegn. Det er teksten brukeren leser før han oppdaterer - skriv hva han merker, ikke en stikkordsliste. Ingen utgivelse er laget."
    }
}

Write-Host "== Setter versjon $Versjon ==" -ForegroundColor Cyan

$ai = Join-Path $rot "src\AssemblyInfo.cs"
$t = Get-Content $ai -Raw -Encoding UTF8
$t = $t -replace 'AssemblyVersion\("[\d\.]+"\)', "AssemblyVersion(`"$Versjon.0`")"
$t = $t -replace 'AssemblyFileVersion\("[\d\.]+"\)', "AssemblyFileVersion(`"$Versjon.0`")"
[IO.File]::WriteAllText($ai, $t, (New-Object Text.UTF8Encoding $true))

$iai = Join-Path $rot "installer\AssemblyInfo.cs"
$t = Get-Content $iai -Raw -Encoding UTF8
$t = $t -replace 'AssemblyVersion\("[\d\.]+"\)', "AssemblyVersion(`"$Versjon.0`")"
$t = $t -replace 'AssemblyFileVersion\("[\d\.]+"\)', "AssemblyFileVersion(`"$Versjon.0`")"
[IO.File]::WriteAllText($iai, $t, (New-Object Text.UTF8Encoding $true))

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

# ---------------------------------------------------------------------------
# En utgivelse skal vaere signert. Punktum.
#
# signer.ps1 hopper med vilje over signeringen naar det ikke finnes noe
# sertifikat - ellers kunne ingen andre klone repoet og bygge. Men da gaar
# bygget gjennom med "Ferdig", og uten denne sperren ville skriptet regnet ut
# sjekksummen av en USIGNERT fil, skrevet manifestet og sagt at alt gikk bra.
# SimplySign-okta lukker seg etter en stund, saa det er ikke et teoretisk
# tilfelle: det holder aa glemme aa logge inn for man bygger.
#
# Strengheten hoerer hjemme her og ikke i bygg.cmd. Bygget skal fortsatt virke
# for alle; det er utgivelsen som ikke skal kunne bli usignert ved et uhell.
# ---------------------------------------------------------------------------
Write-Host "== Kontrollerer signaturen ==" -ForegroundColor Cyan
foreach ($f in @((Join-Path $rot "Brisk.exe"), $exe)) {
    $sig = Get-AuthenticodeSignature $f
    $navn = Split-Path $f -Leaf
    if ($sig.Status -ne "Valid") {
        throw "$navn er ikke signert (status: $($sig.Status)). Logg inn i SimplySign Desktop og bygg paa nytt. Ingen utgivelse er laget."
    }
    if (-not $sig.TimeStamperCertificate) {
        throw "$navn er signert, men uten tidsstempel. Da blir signaturen ugyldig den dagen sertifikatet gaar ut. Ingen utgivelse er laget."
    }
    $eier = $sig.SignerCertificate.GetNameInfo("SimpleName", $false)
    Write-Host ("  {0,-22} {1}" -f $navn, $eier)
}

# ---------------------------------------------------------------------------
# Og kopien INNE i installasjonsfila.
#
# Det er den som faktisk havner paa maskinen til brukeren. At den frittstaaende
# Brisk.exe er signert sier ingenting om den, for den pakkes inn som en ressurs
# under byggingen. Sto signeringen sist i bygg.cmd, ble den usignerte kopien
# bakt inn - og det gikk gjennom fem utgivelser (1.7.0 til 1.7.4) uten at noe
# sa fra, fordi begge filene paa utgivelsen var signert.
#
# Rekkefolgen i bygg.cmd er rettet, men rekkefolge er lett aa rote til igjen.
# Denne sjekker resultatet i stedet for aa stole paa den.
# ---------------------------------------------------------------------------
Write-Host "== Kontrollerer kopien inne i installasjonsfila ==" -ForegroundColor Cyan
$midl = Join-Path ([IO.Path]::GetTempPath()) ("brisk-payload-" + [Guid]::NewGuid().ToString("N") + ".exe")
try {
    $asm = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($exe))
    $str = $asm.GetManifestResourceStream("Brisk.payload")
    if ($null -eq $str) { throw "Installasjonsfila inneholder ingen Brisk.payload. Ingen utgivelse er laget." }
    $ut = [IO.File]::Create($midl)
    try { $str.CopyTo($ut) } finally { $ut.Close(); $str.Close() }

    $ps = Get-AuthenticodeSignature $midl
    if ($ps.Status -ne "Valid") {
        throw "Brisk.exe inne i installasjonsfila er ikke signert (status: $($ps.Status)). Det er den kopien brukeren faar. Ingen utgivelse er laget."
    }
    if (-not $ps.TimeStamperCertificate) {
        throw "Brisk.exe inne i installasjonsfila mangler tidsstempel. Ingen utgivelse er laget."
    }
    Write-Host ("  {0,-22} {1}" -f "Brisk.payload", $ps.SignerCertificate.GetNameInfo("SimpleName", $false))
}
finally { if (Test-Path $midl) { Remove-Item $midl -Force -ErrorAction SilentlyContinue } }

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
