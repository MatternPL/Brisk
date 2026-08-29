using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

// Tar bilde av hovedvinduet til et program med PrintWindow, slik at vinduer
// oppa ikke forstyrrer. Kalles: Grab.exe <exe> <utfil.png> [ventMs]
class Grab
{
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out R r);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct R { public int L, T, Rt, B; }

    static int Main(string[] a)
    {
        if (a.Length < 2) { Console.WriteLine("Grab.exe <exe> <ut.png> [ventMs] [argumenter]"); return 2; }
        int vent = a.Length > 2 ? int.Parse(a[2]) : 7000;

        string extra = a.Length > 3 ? a[3] : "";
        Process p = extra.Length > 0
            ? Process.Start(a[0], extra)
            : Process.Start(a[0]);
        Thread.Sleep(vent);
        p.Refresh();
        if (p.MainWindowHandle == IntPtr.Zero)
        {
            Console.WriteLine("INGEN VINDU (avsluttet=" + p.HasExited + ")");
            return 1;
        }

        SetForegroundWindow(p.MainWindowHandle);
        Thread.Sleep(600);

        R r;
        GetWindowRect(p.MainWindowHandle, out r);
        int w = r.Rt - r.L, h = r.B - r.T;
        if (w <= 0 || h <= 0) { Console.WriteLine("TOMT VINDU"); return 1; }

        using (Bitmap bmp = new Bitmap(w, h))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();
                try { PrintWindow(p.MainWindowHandle, hdc, 2); }   // 2 = PW_RENDERFULLCONTENT
                finally { g.ReleaseHdc(hdc); }
            }
            bmp.Save(a[1], ImageFormat.Png);
        }

        Console.WriteLine("Skrev " + a[1] + " (" + w + "x" + h + ")");
        try { p.Kill(); }
        catch (Exception) { }
        return 0;
    }
}
