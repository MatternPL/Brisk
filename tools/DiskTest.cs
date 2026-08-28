using System;
using System.Collections.Generic;
using System.Threading;
using Vaktmester;

static class DiskTest
{
    static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        string root = args.Length > 0 ? args[0] : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Console.WriteLine("Analyserer " + root);
        DateTime t0 = DateTime.Now;
        List<SizeEntry> fo, fi;
        DiskTools.Scan(root, new CancellationTokenSource().Token, null, out fo, out fi);
        Console.WriteLine("Tok " + (int)(DateTime.Now - t0).TotalSeconds + " s");
        Console.WriteLine();
        Console.WriteLine("STORSTE MAPPER");
        int n = 0;
        foreach (SizeEntry e in fo) { if (n++ >= 10) break;
            Console.WriteLine("  " + Util.Bytes(e.Size).PadLeft(9) + "  " + e.Path); }
        Console.WriteLine();
        Console.WriteLine("STORSTE FILER");
        n = 0;
        foreach (SizeEntry e in fi) { if (n++ >= 8) break;
            Console.WriteLine("  " + Util.Bytes(e.Size).PadLeft(9) + "  " + e.Name); }
        return 0;
    }
}
