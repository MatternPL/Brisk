# Signerer Brisk.exe og BriskInstaller.exe hvis det finnes et
# kodesigneringssertifikat i lageret. Gjor det ikke det, hopper skriptet over
# uten aa feile - da skal bygget fortsatt gaa gjennom for alle andre som
# kloner repoet.
#
# Tidsstempling er ikke valgfritt i praksis: uten det blir alt du har signert
# ugyldig den dagen sertifikatet gaar ut.

param([string]$Filer)

$ErrorActionPreference = "Stop"

# Kalles fra bygg.cmd som -Filer a.exe,b.exe. Gjennom powershell.exe -File
# kommer det inn som én streng, ikke som en liste - derfor splittes den her.
# Uten dette lette skriptet etter en fil som het "a.exe,b.exe", fant den ikke,
# og gikk videre uten aa signere noe som helst.
$Liste = $Filer.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }
$Tidsstempel = "http://time.certum.pl"

# -CodeSigningCert finnes bare i Windows PowerShell, ikke i PowerShell 7.
# Bruksomraadet leses derfor rett av sertifikatet: 1.3.6.1.5.5.7.3.3 er
# «Code Signing». Da virker skriptet uansett hvilken som kjorer det.
$KodeSignering = "1.3.6.1.5.5.7.3.3"
$sert = @(Get-ChildItem Cert:\CurrentUser\My -ErrorAction SilentlyContinue | Where-Object {
    $_.NotAfter -gt (Get-Date) -and $_.HasPrivateKey -and
    ($_.EnhancedKeyUsageList | Where-Object { $_.ObjectId -eq $KodeSignering })
})

if ($sert.Count -eq 0) {
    Write-Host "  ingen sertifikat i lageret - hopper over signering"
    exit 0
}

$s = $sert[0]
Write-Host "  signerer med: $($s.Subject)"
Write-Host "  gyldig til  : $($s.NotAfter.ToString('yyyy-MM-dd'))"

# signtool foerst naar den finnes: den er Microsofts eget verktoy og haandterer
# flere tilfeller enn PowerShell-varianten. Uten Windows SDK finnes den ikke,
# og da brukes Set-AuthenticodeSignature, som ligger i Windows fra for.
$signtool = $null
foreach ($rot in @("${env:ProgramFiles(x86)}\Windows Kits\10\bin", "$env:ProgramFiles\Windows Kits\10\bin")) {
    if (-not (Test-Path $rot)) { continue }
    $funn = Get-ChildItem $rot -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\' } |
            Sort-Object FullName -Descending | Select-Object -First 1
    if ($funn) { $signtool = $funn.FullName; break }
}

$feil = 0
foreach ($f in $Liste) {
    if (-not (Test-Path $f)) { Write-Host "  $f finnes ikke"; continue }

    if ($signtool) {
        & $signtool sign /fd SHA256 /td SHA256 /tr $Tidsstempel /sha1 $s.Thumbprint $f | Out-Null
        $ok = $LASTEXITCODE -eq 0
    } else {
        $r = Set-AuthenticodeSignature -FilePath $f -Certificate $s `
             -HashAlgorithm SHA256 -TimestampServer $Tidsstempel
        $ok = $r.Status -eq "Valid"
    }

    $status = (Get-AuthenticodeSignature $f).Status
    Write-Host ("  {0,-22} {1}" -f (Split-Path $f -Leaf), $status)
    if (-not $ok -or $status -ne "Valid") { $feil++ }
}

if ($feil -gt 0) {
    Write-Host "  SIGNERING FEILET for $feil fil(er)"
    exit 1
}
exit 0
