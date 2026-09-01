# Lager PDF-en Certum ber om: et dokument som knytter deg til
# aapen-kildekode-prosjektet. Skrevet for haand fordi maskinen ikke har noe
# PDF-bibliotek, og fordi det passer resten av prosjektet - ingenting skal
# installeres for aa bygge noe her.
#
#   .\tools\prosjektbevis.ps1 -Navn "Fornavn Etternavn"

param(
    [Parameter(Mandatory = $true)][string]$Navn,
    [string]$Ut = "prosjektbevis.pdf",
    [string]$Prosjekt = "Brisk",
    [string]$Url = "https://github.com/MatternPL/Brisk",
    [string]$Epost = "mathiaspilotarne@gmail.com"
)

$ErrorActionPreference = "Stop"
# Dato paa engelsk uansett systemspraak - dokumentet leses av Certum.
$dato = (Get-Date).ToString("d MMMM yyyy", [Globalization.CultureInfo]::GetCultureInfo("en-GB"))

# (tekst, fet, storrelse, luft over)
$linjer = @(
    @("Open source project declaration", $true, 17, 0),
    @("", $false, 11, 6),
    @("This document accompanies an application for a Certum Open Source", $false, 11, 0),
    @("Code Signing certificate.", $false, 11, 0),
    @("", $false, 11, 14),
    @("Project", $true, 12, 0),
    @("$Prosjekt - a free Windows maintenance tool.", $false, 11, 2),
    @("", $false, 11, 8),
    @("Project URL", $true, 12, 0),
    @($Url, $false, 11, 2),
    @("", $false, 11, 8),
    @("Licence", $true, 12, 0),
    @("MIT. The full source code is public in the repository above.", $false, 11, 2),
    @("", $false, 11, 8),
    @("Applicant", $true, 12, 0),
    @($Navn, $false, 11, 2),
    @($Epost, $false, 11, 2),
    @("", $false, 11, 8),
    @("Relationship to the project", $true, 12, 0),
    @("I am the sole author and maintainer of $Prosjekt. The repository is", $false, 11, 2),
    @("owned by my GitHub account MatternPL, every commit is mine, and the", $false, 11, 0),
    @("MIT licence file in the repository names me as the copyright holder.", $false, 11, 0),
    @("The releases published there are built and published by me.", $false, 11, 0),
    @("", $false, 11, 14),
    @($Navn, $false, 11, 0),
    @($dato, $false, 11, 2)
)

# --- bygger sidens innhold ---
$sb = New-Object Text.StringBuilder
[void]$sb.AppendLine("BT")
$y = 780.0
foreach ($l in $linjer) {
    $tekst = [string]$l[0]; $fet = [bool]$l[1]; $str = [double]$l[2]; $over = [double]$l[3]
    $y -= ($str + 4 + $over)
    if ($tekst -eq "") { continue }
    $esc = $tekst.Replace('\', '\\').Replace('(', '\(').Replace(')', '\)')
    $font = if ($fet) { "/F2" } else { "/F1" }
    [void]$sb.AppendLine("$font $str Tf 1 0 0 1 60 $([math]::Round($y,1)) Tm ($esc) Tj")
}
[void]$sb.AppendLine("ET")
$innhold = $sb.ToString()

# --- setter sammen fila. Byte-avstandene i xref maa stemme paa tegnet. ---
$objekter = @(
    "<< /Type /Catalog /Pages 2 0 R >>",
    "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>",
    "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
    "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>",
    "<< /Length $($innhold.Length) >>`nstream`n$innhold`nendstream"
)

$pdf = New-Object Text.StringBuilder
[void]$pdf.Append("%PDF-1.4`n")
$offset = @()
for ($i = 0; $i -lt $objekter.Count; $i++) {
    $offset += $pdf.Length
    [void]$pdf.Append("$($i+1) 0 obj`n$($objekter[$i])`nendobj`n")
}
$xref = $pdf.Length
[void]$pdf.Append("xref`n0 $($objekter.Count + 1)`n")
[void]$pdf.Append("0000000000 65535 f `n")
foreach ($o in $offset) { [void]$pdf.Append("{0:D10} 00000 n `n" -f $o) }
[void]$pdf.Append("trailer`n<< /Size $($objekter.Count + 1) /Root 1 0 R >>`nstartxref`n$xref`n%%EOF")

# Latin-1, ikke UTF-8: PDF-en oppgir WinAnsiEncoding, og da maa bytene stemme.
[IO.File]::WriteAllText($Ut, $pdf.ToString(), [Text.Encoding]::GetEncoding(28591))
"Skrev $Ut ($((Get-Item $Ut).Length) bytes)"
