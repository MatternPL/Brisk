# Tar bilde av et vindu med PrintWindow, slik at andre vinduer oppa ikke forstyrrer.
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$Ut,
    [string]$Args = "",
    [int]$VentMs = 7000
)

Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public class PW {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Rt, B; }

  public static Bitmap Grab(IntPtr h) {
    R r; GetWindowRect(h, out r);
    int w = r.Rt - r.L, ht = r.B - r.T;
    if (w <= 0 || ht <= 0) return null;
    Bitmap bmp = new Bitmap(w, ht);
    using (Graphics g = Graphics.FromImage(bmp)) {
      IntPtr hdc = g.GetHdc();
      try { PrintWindow(h, hdc, 2); }   // 2 = PW_RENDERFULLCONTENT
      finally { g.ReleaseHdc(hdc); }
    }
    return bmp;
  }
}
"@

$p = if ($Args) { Start-Process $Exe -ArgumentList $Args -PassThru } else { Start-Process $Exe -PassThru }
Start-Sleep -Milliseconds $VentMs
$p.Refresh()
if ($p.MainWindowHandle -eq 0) {
    Write-Output "INGEN VINDU (avsluttet=$($p.HasExited))"
    exit 1
}
[void][PW]::SetForegroundWindow($p.MainWindowHandle)
Start-Sleep -Milliseconds 600
$bmp = [PW]::Grab($p.MainWindowHandle)
if ($null -eq $bmp) { Write-Output "TOMT VINDU"; exit 1 }
$bmp.Save($Ut, [Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Stop-Process -Id $p.Id -Force
Write-Output "lagret $Ut"
