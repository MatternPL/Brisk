using System;
using System.Collections.Generic;
using Brisk;

static class DrvTest
{
    static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("Registrerer Microsoft Update ... " + DriverTools.EnsureMicrosoftUpdate());
        DateTime t0 = DateTime.Now;
        string note;
        List<DriverUpdate> l = DriverTools.SearchDrivers(out note);
        Console.WriteLine("Sok tok " + (int)(DateTime.Now - t0).TotalSeconds + " s");
        Console.WriteLine("Treff: " + l.Count);
        Console.WriteLine("Notat: " + note);
        foreach (DriverUpdate d in l)
            Console.WriteLine("  - " + d.Title + "   [" + d.Driver + "]  " + Util.Bytes(d.Size));
        return 0;
    }
}
