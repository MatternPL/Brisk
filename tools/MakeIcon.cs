// Lager brisk.ico i flere oppløsninger fra det felles logo-merket.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

static class MakeIcon
{
    static int Main(string[] args)
    {
        string outPath = args.Length > 0 ? args[0] : "brisk.ico";
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        byte[][] pngs = new byte[sizes.Length][];

        for (int i = 0; i < sizes.Length; i++)
        {
            using (Bitmap b = Brisk.Logo.Bitmap(sizes[i], true))
            using (MemoryStream ms = new MemoryStream())
            {
                b.Save(ms, ImageFormat.Png);
                pngs[i] = ms.ToArray();
            }
        }

        using (FileStream fs = new FileStream(outPath, FileMode.Create))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            w.Write((short)0);                      // reservert
            w.Write((short)1);                      // type: ikon
            w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0);                   // palett
                w.Write((byte)0);                   // reservert
                w.Write((short)1);                  // plan
                w.Write((short)32);                 // bits per piksel
                w.Write(pngs[i].Length);
                w.Write(offset);
                offset += pngs[i].Length;
            }
            for (int i = 0; i < sizes.Length; i++) w.Write(pngs[i]);
        }

        // PNG-forhåndsvisning så logoen kan sjekkes uten å åpne ikonet.
        using (Bitmap b = Brisk.Logo.Bitmap(512, true))
            b.Save(Path.ChangeExtension(outPath, ".png"), ImageFormat.Png);

        Console.WriteLine("Skrev " + outPath);
        return 0;
    }
}
