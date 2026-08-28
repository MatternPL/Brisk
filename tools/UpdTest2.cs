using System;
using Vaktmester;

static class UpdTest2
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
        Console.WriteLine("== Tolking av versjonsfil ==");

        string json = "{\n" +
            "  \"versjon\": \"1.2.0\",\n" +
            "  \"url\": \"https://example.com/Vaktmester-Installer.exe\",\n" +
            "  \"sha256\": \"aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899\",\n" +
            "  \"storrelse\": 288768,\n" +
            "  \"notat\": \"Rettet to feil.\nLagt til diskplass-analyse.\"\n" +
            "}";

        Sjekk("versjon", Updater.Field(json, "versjon") == "1.2.0");
        Sjekk("url", Updater.Field(json, "url") == "https://example.com/Vaktmester-Installer.exe");
        Sjekk("sha256 er 64 tegn", Updater.Field(json, "sha256").Length == 64);
        Sjekk("storrelse (tall uten hermetegn)", Updater.Field(json, "storrelse") == "288768");
        Sjekk("notat med linjeskift",
              Updater.Field(json, "notat").Contains("Rettet to feil.") &&
              Updater.Field(json, "notat").Contains("diskplass"));
        Sjekk("ukjent felt gir tom streng", Updater.Field(json, "finnesikke") == "");
        Sjekk("tom inndata krasjer ikke", Updater.Field("", "versjon") == "");
        Sjekk("soppel krasjer ikke", Updater.Field("{ikke gyldig", "versjon") == "");

        Console.WriteLine();
        Console.WriteLine(feil == 0 ? "ALT GRONT." : feil + " FEILET.");
        return feil == 0 ? 0 : 1;
    }
}
