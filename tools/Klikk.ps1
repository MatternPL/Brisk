# Klikker i et vindu og tar bilde, for aa teste UI-veier som ikke naas via argumenter.
param(
    [string]$Exe,
    [string]$Argumenter = "",
    [string]$Ut,
    [int]$VentMs = 8000,
    [int[]]$KlikkX = @(),
    [int[]]$KlikkY = @(),
    [int]$MellomMs = 900,
    [string]$Taster = ""
)

Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public class K {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, IntPtr e);
  [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Rt, B; }

  public static void Click(IntPtr h, int cx, int cy) {
    R r; GetWindowRect(h, out r);
    SetCursorPos(r.L + cx, r.T + cy);
    System.Threading.Thread.Sleep(120);
    mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);
    mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);
  }

  // Hele skjermen, slik at menyer utenfor vinduet blir med.
  public static Bitmap GrabScreen(IntPtr h, int pad) {
    R r; GetWindowRect(h, out r);
    int x = Math.Max(0, r.L - pad), y = Math.Max(0, r.T - pad);
    int w = (r.Rt - r.L) + pad * 2, ht = (r.B - r.T) + pad * 2;
    Bitmap bmp = new Bitmap(w, ht);
    using (Graphics g = Graphics.FromImage(bmp))
      g.CopyFromScreen(x, y, 0, 0, new Size(w, ht));
    return bmp;
  }
}
"@

$p = if ($Argumenter) { Start-Process $Exe -ArgumentList $Argumenter -PassThru } else { Start-Process $Exe -PassThru }
Start-Sleep -Milliseconds $VentMs
$p.Refresh()
$h = $p.MainWindowHandle
if ($h -eq 0) { Write-Output "INGEN VINDU"; exit 1 }
[void][K]::SetForegroundWindow($h)
Start-Sleep -Milliseconds 900

for ($i = 0; $i -lt $KlikkX.Count; $i++) {
    [K]::Click($h, $KlikkX[$i], $KlikkY[$i])
    Start-Sleep -Milliseconds $MellomMs
}

if ($Taster) {
    Add-Type -AssemblyName System.Windows.Forms
    [Windows.Forms.SendKeys]::SendWait($Taster)
    Start-Sleep -Milliseconds 1500
}

$bmp = [K]::GrabScreen($h, 0)
$bmp.Save($Ut, [Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Stop-Process -Id $p.Id -Force
Write-Output "lagret $Ut"
