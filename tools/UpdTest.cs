using System;
using System.IO;
using Brisk;

static class UpdTest
{
    static int feil = 0;

    static void Sjekk(string hva, bool ok)
    {
        Console.WriteLine((ok ? "  OK    " : "  FEIL  ") + hva);
        if (!ok) feil++;
    }

    static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("== 1. Versjonssammenligning ==");
        Sjekk("1.1.0 > 1.0.0", Updater.Compare("1.1.0", "1.0.0") == 1);
        Sjekk("1.0.0 = 1.0.0", Updater.Compare("1.0.0", "1.0.0") == 0);
        Sjekk("1.0.0 < 1.0.1", Updater.Compare("1.0.0", "1.0.1") == -1);
        Sjekk("2.0 > 1.9.9", Updater.Compare("2.0", "1.9.9") == 1);
        Sjekk("1.10.0 > 1.9.0", Updater.Compare("1.10.0", "1.9.0") == 1);
        Sjekk("naavaerende versjon lest: " + Updater.CurrentVersion,
              Updater.CurrentVersion.Split('.').Length == 3);

        Console.WriteLine();
        Console.WriteLine("== 2. Nedlasting over https + sha256 ==");
        // En liten, stabil fil hos GitHub. Vi henter den, regner ut summen selv,
        // og sjekker at riktig sum godtas og at feil sum avvises.
        string url = "https://raw.githubusercontent.com/github/gitignore/main/Global/Windows.gitignore";

        UpdateInfo probe = new UpdateInfo();
        probe.Version = "9.9.9";
        probe.Url = url;
        probe.Sha256 = new string('0', 64);          // med vilje feil

        string err;
        string path = Updater.Download(probe, null, out err);
        Sjekk("feil sjekksum avvises", path == null);
        Sjekk("feilmelding nevner sjekksum: " + err,
              err != null && err.ToLowerInvariant().Contains("sjekksum"));

        string tmp = Path.Combine(Path.GetTempPath(), "Brisk-9.9.9-installer.exe");
        Sjekk("den avviste filen ble slettet", !File.Exists(tmp));

        // Hent den ordentlig for aa finne fasiten
        string reference = Path.Combine(Path.GetTempPath(), "vm-testfil.bin");
        try
        {
            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12 | (System.Net.SecurityProtocolType)3072;
            using (System.Net.WebClient wc = new System.Net.WebClient())
            {
                wc.Headers.Add("User-Agent", "Brisk-test");
                wc.DownloadFile(url, reference);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("  KUNNE IKKE NAA NETT: " + ex.Message);
            Console.WriteLine("  (hopper over resten av nettverkstesten)");
            return feil == 0 ? 0 : 1;
        }

        string riktig = Updater.Sha256Of(reference);
        Sjekk("sha256 regnet ut (" + riktig.Substring(0, 16) + "...)", riktig.Length == 64);

        probe.Sha256 = riktig;
        long sett = 0;
        path = Updater.Download(probe, delegate(long got, long total) { sett = got; }, out err);
        Sjekk("riktig sjekksum godtas", path != null && File.Exists(path));
        Sjekk("framdrift ble rapportert (" + sett + " byte)", sett > 0);
        if (err != null) Console.WriteLine("  melding: " + err);

        try { File.Delete(reference); } catch { }
        try { if (path != null) File.Delete(path); } catch { }

        Console.WriteLine();
        Console.WriteLine(feil == 0 ? "ALT GRONT." : feil + " TEST(ER) FEILET.");
        return feil == 0 ? 0 : 1;
    }
}
