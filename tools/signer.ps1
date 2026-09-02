# Signerer Brisk.exe og BriskInstaller.exe hvis det finnes et
# kodesigneringssertifikat i lageret. Gjor det ikke det, hopper skriptet over
# uten aa feile - da skal bygget fortsatt gaa gjennom for alle andre som
# kloner repoet.
#
# Tidsstempling er ikke valgfritt i praksis: uten det blir alt du har signert
# ugyldig den dagen sertifikatet gaar ut. Sertifikatet her varer ett aar.

param([string]$Filer)

$ErrorActionPreference = "Stop"

# Kalles fra bygg.cmd som -Filer a.exe,b.exe. Gjennom powershell.exe -File
# kommer det inn som én streng, ikke som en liste - derfor splittes den her.
# Uten dette lette skriptet etter en fil som het "a.exe,b.exe", fant den ikke,
# og gikk videre uten aa signere noe som helst.
$Liste = $Filer.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }
$Tidsstempel = "http://time.certum.pl"
$KodeSignering = "1.3.6.1.5.5.7.3.3"

# ---------------------------------------------------------------------------
# Sertifikatet hentes med X509Store, ikke gjennom Cert:-stasjonen.
#
# Cert: lages av modulen Microsoft.PowerShell.Security, og den lastes ikke
# alltid. Startes skriptet fra bygg.cmd - altsa cmd.exe -> powershell.exe -File
# - kaster «Cert:» DriveNotFoundException. Med -ErrorAction SilentlyContinue
# ble den feilen slukt, skriptet konkluderte «ingen sertifikat i lageret», og
# bygget la fra seg USIGNERTE filer uten aa si fra. Det var akkurat den stille
# svikten som er verst: alt saa vellykket ut.
#
# X509Store er API-et provideren selv pakker inn, og det virker uansett hvordan
# skriptet startes. Samme grunn til at bruksomraadet leses rett av utvidelsen i
# stedet for gjennom EnhancedKeyUsageList, som ogsaa kommer fra den modulen.
# ---------------------------------------------------------------------------
function Finn-Sertifikat {
    $naa = Get-Date
    foreach ($sted in @("CurrentUser", "LocalMachine")) {
        $lager = New-Object System.Security.Cryptography.X509Certificates.X509Store "My", $sted
        try { $lager.Open("ReadOnly") } catch { continue }
        try {
            foreach ($c in $lager.Certificates) {
                if (-not $c.HasPrivateKey) { continue }
                if ($c.NotAfter -le $naa -or $c.NotBefore -gt $naa) { continue }
                foreach ($u in $c.Extensions) {
                    if ($u -isnot [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]) { continue }
                    foreach ($oid in $u.EnhancedKeyUsages) {
                        if ($oid.Value -eq $KodeSignering) { return $c }
                    }
                }
            }
        }
        finally { $lager.Close() }
    }
    return $null
}

$s = Finn-Sertifikat
if ($null -eq $s) {
    Write-Host "  ingen sertifikat i lageret - hopper over signering"
    exit 0
}

Write-Host "  signerer med: $($s.Subject)"
Write-Host "  gyldig til  : $($s.NotAfter.ToString('yyyy-MM-dd'))"

# signtool er Microsofts eget verktoy og haandterer flere tilfeller enn
# PowerShell-varianten. Uten Windows SDK finnes den ikke, og da brukes
# Set-AuthenticodeSignature - men den ligger i samme modul som Cert:, saa den
# er ikke alltid tilgjengelig heller. Mangler begge, er det en feil, ikke noe
# aa hoppe stille over.
$signtool = $null
foreach ($rot in @("${env:ProgramFiles(x86)}\Windows Kits\10\bin", "$env:ProgramFiles\Windows Kits\10\bin")) {
    if (-not (Test-Path $rot)) { continue }
    $funn = Get-ChildItem $rot -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\' } |
            Sort-Object FullName -Descending | Select-Object -First 1
    if ($funn) { $signtool = $funn.FullName; break }
}

$harCmdlet = $null -ne (Get-Command Set-AuthenticodeSignature -ErrorAction SilentlyContinue)
if (-not $signtool -and -not $harCmdlet) {
    Write-Host "  FANT VERKEN signtool.exe ELLER Set-AuthenticodeSignature - kan ikke signere"
    exit 1
}

$feil = 0
foreach ($f in $Liste) {
    if (-not (Test-Path $f)) { Write-Host "  $f finnes ikke"; $feil++; continue }

    if ($signtool) {
        & $signtool sign /fd SHA256 /td SHA256 /tr $Tidsstempel /sha1 $s.Thumbprint $f | Out-Null
        $ok = $LASTEXITCODE -eq 0
    } else {
        $r = Set-AuthenticodeSignature -FilePath $f -Certificate $s `
             -HashAlgorithm SHA256 -TimestampServer $Tidsstempel
        $ok = $r.Status -eq "Valid"
    }

    Write-Host ("  {0,-22} {1}" -f (Split-Path $f -Leaf), $(if ($ok) { "signert" } else { "FEILET" }))
    if (-not $ok) { $feil++; continue }

    # Det som avgjor om signaturen holder hos andre er om kjeden ble bakt inn i
    # selve fila. Ligger den ikke der, maa mottakerens maskin hente mellomleddet
    # over nett - og gaar ikke det, staar programmet som usignert hos dem selv
    # om det er signert her. signtool verify /pa leser kjeden ut av fila.
    if ($signtool) {
        $v = & $signtool verify /pa /v $f 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "      ADVARSEL: verifiseringen gikk ikke gjennom"
            $v | Select-Object -Last 4 | ForEach-Object { Write-Host "        $_" }
            $feil++
            continue
        }
        $kjede = $v | Select-String -Pattern "^\s+Issued to:" | ForEach-Object { $_.Line.Trim() }
        if ($kjede) {
            Write-Host "      kjede i fila:"
            $kjede | ForEach-Object { Write-Host "        $_" }
        } else {
            Write-Host "      ADVARSEL: fant ingen kjede i fila - signaturen kan svikte paa maskiner"
            Write-Host "      som ikke har Certum sine mellomledd installert fra for."
        }
    }
}

if ($feil -gt 0) {
    Write-Host "  SIGNERING FEILET for $feil fil(er)"
    exit 1
}
exit 0
