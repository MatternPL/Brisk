using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Brisk
{
    // P/Invoke-lag: minne, papirkurv og privilegier.
    static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        public static MEMORYSTATUSEX GetMemory()
        {
            MEMORYSTATUSEX m = new MEMORYSTATUSEX();
            m.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            GlobalMemoryStatusEx(ref m);
            return m;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        static extern int EmptyWorkingSet(IntPtr hProcess);

        public static bool TrimProcess(Process p)
        {
            try { return EmptyWorkingSet(p.Handle) != 0; }
            catch { return false; }
        }

        // ---- Standby-liste (RAMMap-metoden) ----
        [DllImport("ntdll.dll")]
        static extern int NtSetSystemInformation(int InfoClass, ref int Info, int Length);

        const int SystemMemoryListInformation = 80;
        const int MemoryPurgeStandbyList = 4;
        const int MemoryEmptyWorkingSets = 2;
        const int MemoryFlushModifiedList = 3;

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool OpenProcessToken(IntPtr h, uint acc, out IntPtr tok);
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool LookupPrivilegeValue(string host, string name, out long luid);
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool AdjustTokenPrivileges(IntPtr tok, bool disall, ref TOKEN_PRIVILEGES np,
            int len, IntPtr prev, IntPtr rl);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr h);

        [StructLayout(LayoutKind.Sequential)]
        struct TOKEN_PRIVILEGES
        {
            public int PrivilegeCount;
            public long Luid;
            public int Attributes;
        }

        static bool EnablePrivilege(string name)
        {
            IntPtr tok;
            if (!OpenProcessToken(GetCurrentProcess(), 0x0020 | 0x0008, out tok)) return false;
            try
            {
                long luid;
                if (!LookupPrivilegeValue(null, name, out luid)) return false;
                TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
                tp.PrivilegeCount = 1;
                tp.Luid = luid;
                tp.Attributes = 0x00000002; // SE_PRIVILEGE_ENABLED
                if (!AdjustTokenPrivileges(tok, false, ref tp, Marshal.SizeOf(tp), IntPtr.Zero, IntPtr.Zero))
                    return false;
                return Marshal.GetLastWin32Error() == 0;
            }
            finally { CloseHandle(tok); }
        }

        public static bool PurgeStandbyList()
        {
            if (!EnablePrivilege("SeProfileSingleProcessPrivilege")) return false;
            int cmd = MemoryPurgeStandbyList;
            return NtSetSystemInformation(SystemMemoryListInformation, ref cmd, sizeof(int)) == 0;
        }

        public static bool EmptyAllWorkingSets()
        {
            if (!EnablePrivilege("SeProfileSingleProcessPrivilege")) return false;
            int cmd = MemoryEmptyWorkingSets;
            return NtSetSystemInformation(SystemMemoryListInformation, ref cmd, sizeof(int)) == 0;
        }

        // ---- Papirkurv ----
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        public static long RecycleBinSize(out long items)
        {
            items = 0;
            try
            {
                SHQUERYRBINFO info = new SHQUERYRBINFO();
                info.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
                if (SHQueryRecycleBin(null, ref info) == 0)
                {
                    items = info.i64NumItems;
                    return info.i64Size;
                }
            }
            catch { }
            return 0;
        }

        public static bool EmptyRecycleBin()
        {
            try
            {
                // 0x1 NOCONFIRMATION | 0x2 NOPROGRESSUI | 0x4 NOSOUND
                return SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 | 0x2 | 0x4) == 0;
            }
            catch { return false; }
        }
    }
}
