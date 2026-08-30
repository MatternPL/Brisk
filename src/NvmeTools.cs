using System;
using System.Runtime.InteropServices;

namespace Brisk
{
    // Leser NVMe-helseloggen rett fra disken.
    //
    // Grunnen til at dette finnes: Windows sin MSFT_StorageReliabilityCounter
    // rapporterer Wear = 0 og PowerOnHours = null for mange NVMe-disker, ogsaa
    // naar disken selv vet bedre. Maalt paa to Kingston-disker: WMI sa 0 % paa
    // begge, mens loggen sa 1 % og 5 %, med 7091 og 16199 timer paaslaatt.
    //
    // Loggen aapnes uten GENERIC_READ/WRITE, saa dette virker uten administrator.
    public static class NvmeTools
    {
        public class Health
        {
            public int PercentUsed = -1;     // hvor mye av forventet levetid som er brukt
            public int Temperature = -1;     // grader celsius
            public int SpareLeft = -1;       // ledig reservekapasitet i prosent
            public long PowerOnHours = -1;
            public long UnsafeShutdowns = -1;
            public long MediaErrors = -1;
            public byte CriticalWarning;     // bitmaske fra disken, 0 = alt bra
        }

        const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x2D1400;
        const int StorageDeviceProtocolSpecificProperty = 49;
        const int PropertyStandardQuery = 0;
        const int ProtocolTypeNvme = 3;
        const int NVMeDataTypeLogPage = 2;
        const int SmartHealthLogPage = 2;

        [StructLayout(LayoutKind.Sequential)]
        struct ProtocolData
        {
            public int ProtocolType;
            public uint DataType;
            public uint RequestValue;
            public uint RequestSubValue;
            public uint DataOffset;
            public uint DataLength;
            public uint FixedReturnData;
            public uint Sub2, Sub3, Sub4;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Query
        {
            public int PropertyId;
            public int QueryType;
            public ProtocolData Protocol;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr sec,
                                        uint disp, uint flags, IntPtr templ);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(IntPtr h, uint code, IntPtr inBuf, int inSize,
                                           IntPtr outBuf, int outSize, out int returned, IntPtr ov);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr h);

        // Leser helseloggen for én fysisk disk. Returnerer null hvis disken ikke
        // er NVMe, ikke svarer, eller ikke finnes.
        public static Health Read(int physicalDrive)
        {
            // Access 0: vi spor bare om egenskaper, og trenger ingen rettigheter
            // til selve dataene. Med GENERIC_READ ville dette krevd administrator.
            IntPtr h = CreateFile(@"\\.\PhysicalDrive" + physicalDrive,
                                  0u, 3u, IntPtr.Zero, 3u, 0u, IntPtr.Zero);
            if (h == (IntPtr)(-1)) return null;

            IntPtr buf = IntPtr.Zero;
            try
            {
                int headerSize = Marshal.SizeOf(typeof(Query));
                int total = headerSize + 512;
                buf = Marshal.AllocHGlobal(total);
                for (int i = 0; i < total; i++) Marshal.WriteByte(buf, i, 0);

                Query q = new Query();
                q.PropertyId = StorageDeviceProtocolSpecificProperty;
                q.QueryType = PropertyStandardQuery;
                q.Protocol.ProtocolType = ProtocolTypeNvme;
                q.Protocol.DataType = NVMeDataTypeLogPage;
                q.Protocol.RequestValue = SmartHealthLogPage;
                q.Protocol.RequestSubValue = 0;
                q.Protocol.DataOffset = (uint)Marshal.SizeOf(typeof(ProtocolData));
                q.Protocol.DataLength = 512;
                Marshal.StructureToPtr(q, buf, false);

                int got;
                if (!DeviceIoControl(h, IOCTL_STORAGE_QUERY_PROPERTY, buf, total, buf, total,
                                     out got, IntPtr.Zero))
                    return null;

                // Svaret er et 8 byte descriptor-hode, saa protokollstrukturen,
                // saa selve loggen paa 512 byte.
                int dataOff = 8 + (int)q.Protocol.DataOffset;
                if (dataOff + 512 > total) return null;

                byte[] log = new byte[512];
                Marshal.Copy(new IntPtr(buf.ToInt64() + dataOff), log, 0, 512);

                Health r = new Health();
                r.CriticalWarning = log[0];
                int kelvin = (int)Le(log, 1, 2);
                r.Temperature = kelvin > 0 ? kelvin - 273 : -1;
                r.SpareLeft = log[3];
                r.PercentUsed = log[5];
                r.PowerOnHours = (long)Le(log, 128, 8);
                r.UnsafeShutdowns = (long)Le(log, 144, 8);
                r.MediaErrors = (long)Le(log, 160, 8);

                // En logg full av nuller betyr som regel at disken ikke svarte
                // ordentlig. Da er det aerligere aa si ingenting.
                if (r.Temperature <= 0 && r.PowerOnHours == 0 && r.PercentUsed == 0
                    && r.SpareLeft == 0) return null;

                return r;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf);
                CloseHandle(h);
            }
        }

        // NVMe lagrer tellerne som 128-bits tall, men bare de laveste byte-ene
        // brukes i praksis. Vi leser like mange byte som vi trenger, little endian.
        static ulong Le(byte[] b, int off, int len)
        {
            ulong v = 0;
            for (int i = len - 1; i >= 0; i--) v = (v << 8) | b[off + i];
            return v;
        }
    }
}
