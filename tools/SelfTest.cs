using System;
using System.Collections.Generic;
using System.Threading;
using Vaktmester;

static class SelfTest
{
    static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("== 1. MINNE ==");
        MemSnapshot m = MemoryTools.Snapshot();
        Console.WriteLine("  Totalt      : " + Util.Bytes(m.TotalPhys));
        Console.WriteLine("  I bruk      : " + Util.Bytes(m.UsedPhys) + "  (" + m.LoadPercent + " %)");
        Console.WriteLine("  Tilgjengelig: " + Util.Bytes(m.AvailPhys));
        Console.WriteLine("  Standby     : " + Util.Bytes(m.Standby));
        Console.WriteLine("  Modified    : " + Util.Bytes(m.Modified));
        if (m.TotalPhys == 0) return Fail("GlobalMemoryStatusEx ga 0");
        if (m.Standby == 0) Console.WriteLine("  ADVARSEL: standby-teller er 0 (WMI-teller kan mangle)");

        Console.WriteLine();
        Console.WriteLine("== 2. TOPP PROSESSER ==");
        foreach (ProcMem p in MemoryTools.TopProcesses(6))
            Console.WriteLine("  " + p.Name.PadRight(24) + Util.Bytes(p.Bytes).PadLeft(10) + "   x" + p.Count);

        Console.WriteLine();
        Console.WriteLine("== 3. RYDDING (kun analyse, sletter ingenting) ==");
        long total = 0;
        CancellationTokenSource cts = new CancellationTokenSource();
        foreach (CleanTarget t in Cleaner.BuildTargets())
        {
            DateTime t0 = DateTime.Now;
            Cleaner.Scan(t, cts.Token, null);
            total += t.FoundBytes;
            Console.WriteLine("  " + t.Name.PadRight(42) + Util.Bytes(t.FoundBytes).PadLeft(10) +
                "  " + t.FoundFiles.ToString("N0").PadLeft(8) + " filer   " +
                ((int)(DateTime.Now - t0).TotalMilliseconds) + " ms");
        }
        Console.WriteLine("  SUM: " + Util.Bytes(total));

        Console.WriteLine();
        Console.WriteLine("== 4. OPPSTART ==");
        List<StartupItem> items = StartupTools.Enumerate(true);
        int act = 0, tasks = 0;
        foreach (StartupItem it in items)
        {
            if (it.Enabled) act++;
            if (it.Kind == StartupKind.Task) tasks++;
        }
        Console.WriteLine("  " + items.Count + " oppføringer (" + act + " aktive, " + tasks + " planlagte oppgaver)");
        int shown = 0;
        foreach (StartupItem it in items)
        {
            if (shown++ >= 8) break;
            Console.WriteLine("  " + (it.Enabled ? "[x] " : "[ ] ") + it.Name.PadRight(28) +
                it.KindText.PadRight(26) + it.Publisher);
        }
        if (items.Count == 0) return Fail("Fant ingen oppstartsoppføringer i det hele tatt");

        Console.WriteLine();
        Console.WriteLine("== 5. DISKER ==");
        foreach (DiskInfo d in MaintenanceTools.PhysicalDisks())
            Console.WriteLine("  " + d.Name.PadRight(34) + d.Media.PadRight(8) + d.Health.PadRight(10) + Util.Bytes(d.Size));
        foreach (VolumeInfo v in MaintenanceTools.Volumes())
            Console.WriteLine("  " + v.Letter.PadRight(34) + "Volum   " +
                Util.Bytes(v.Free) + " ledig av " + Util.Bytes(v.Total));

        Console.WriteLine();
        Console.WriteLine("== 6. PROBLEMENHETER ==");
        List<ProblemDevice> devs = DriverTools.FindProblemDevices();
        Console.WriteLine("  " + devs.Count + " enheter med feilkode");
        foreach (ProblemDevice d in devs)
            Console.WriteLine("  - " + d.Name + "  →  " + d.ErrorText);

        Console.WriteLine();
        Console.WriteLine("== 7. WINGET ==");
        if (!WingetTools.IsAvailable()) Console.WriteLine("  winget ikke tilgjengelig");
        else
        {
            string note;
            List<AppUpgrade> ups = WingetTools.ListUpgrades(out note);
            Console.WriteLine("  " + ups.Count + " oppdateringer. " + note);
            int n = 0;
            foreach (AppUpgrade a in ups)
            {
                if (n++ >= 8) break;
                Console.WriteLine("  - " + a.Name.PadRight(34) + a.Current.PadRight(18) + "→ " +
                    a.Available.PadRight(18) + a.Id);
            }
        }

        Console.WriteLine();
        Console.WriteLine("ALLE TESTER KJØRT.");
        return 0;
    }

    static int Fail(string s)
    {
        Console.WriteLine("FEIL: " + s);
        return 1;
    }
}
