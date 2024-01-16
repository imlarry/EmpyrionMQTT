using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace ESB.TopicHandlers
{
    /*
    public unsafe class MemorySearcher
    {
        private const int BufferSize = 1000; // Number of bytes to capture before and after
        private const long NoGCSize = 2097152;

        private const int SizeOfStarData = 24;
        private const int StarCountThreshold = 1000;
        private const long RegionSizeThreshold = SizeOfStarData * StarCountThreshold;
        private const int RegionListInitialCapacity = 293;
        private bool noResumeGC = false;
        private readonly Kernel32.SYSTEM_INFO sysInfo;
        private readonly int memInfoSize;

        public MemorySearcher()
        {
            Kernel32.GetSystemInfo(out sysInfo);
            Kernel32.MEMORY_BASIC_INFORMATION memInfo = default;
            memInfoSize = Marshal.SizeOf(memInfo);
        }

        public (byte[] MemBefore, byte[] MemAfter) Search(string searchString)
        {
            // Convert the search string to a byte array
            byte[] searchBytes = Encoding.ASCII.GetBytes(searchString);

            // Pause the Garbage Collector
            PauseGC();

            // Create a list to hold the memory regions
            var mbi = new List<Kernel32.MEMORY_BASIC_INFORMATION>(RegionListInitialCapacity);
            Kernel32.MEMORY_BASIC_INFORMATION memInfo = default;

            // Iterate over the memory regions
            while (NextRegion(ref memInfo))
            {
                if (memInfo.RegionSize >= RegionSizeThreshold)
                {
                    mbi.Add(memInfo);
                }
            }
            mbi.Reverse();

            // Iterate over each memory region in the list
            foreach (var mem in mbi)
            {
                byte* limit = mem.BaseAddress + mem.RegionSize - searchBytes.Length;
                byte* addr = mem.BaseAddress;
                while (addr < limit)
                {
                    // Check if the bytes at addr match searchBytes
                    if (BytesMatch(addr, searchBytes))
                    {
                        // If the search string is found, capture the bytes before and after it
                        byte[] memBefore = GetBytesBefore(addr, BufferSize);
                        byte[] memAfter = GetBytesAfter(addr, searchBytes.Length, BufferSize);

                        // Resume the Garbage Collector
                        ResumeGC();

                        return (memBefore, memAfter);
                    }
                    addr++;
                }
            }

            unsafe bool BytesMatch(byte* addr, byte[] sBytes)
            {
                for (int i = 0; i < sBytes.Length; i++)
                {
                    if (addr[i] != sBytes[i])
                    {
                        return false;
                    }
                }
                return true;
            }

            unsafe byte[] GetBytesBefore(byte* addr, int count)
            {
                byte[] bytes = new byte[count];
                for (int i = 0; i < count; i++)
                {
                    bytes[i] = *(addr - count + i);
                }
                return bytes;
            }

            unsafe byte[] GetBytesAfter(byte* addr, int offset, int count)
            {
                byte[] bytes = new byte[count];
                for (int i = 0; i < count; i++)
                {
                    bytes[i] = *(addr + offset + i);
                }
                return bytes;
            }


            // Resume the Garbage Collector
            ResumeGC();
            return (null, null);
        }

        // Other methods (PauseGC, ResumeGC, NextRegion, etc.) would remain the same as in the StarFinder class

        unsafe private bool NextRegion(ref Kernel32.MEMORY_BASIC_INFORMATION memInfo)
        {
            byte* baseAddress = (memInfo.BaseAddress != null)
                ? memInfo.BaseAddress
                : sysInfo.lpMinimumApplicationAddress;

            while (baseAddress < sysInfo.lpMaximumApplicationAddress)
            {
                baseAddress += memInfo.RegionSize;
                if (Kernel32.VirtualQuery(baseAddress, out memInfo, memInfoSize) == 0)
                {
                    baseAddress += sysInfo.dwPageSize;
                    continue;
                }

                if (memInfo.Protect == Kernel32.DesiredPageProtection
                    && memInfo.State == Kernel32.DesiredPageState
                    && memInfo.Type == Kernel32.DesiredPageType
                )
                {
                    return true;
                }
            }
            return false;
        }

        private void PauseGC()
        {
            try
            {
                GC.TryStartNoGCRegion(NoGCSize);
            }
            catch (InvalidOperationException)
            {
                noResumeGC = true;
            }
            catch
            {
                // must continue whether or not GC is suspended
            }
        }

        private void ResumeGC()
        {
            try
            {
                if (!noResumeGC)
                {
                    // never guaranteed not to throw
                    GC.EndNoGCRegion();
                }
            }
            catch
            {
                // nothing to do; just move on
            }
        }

    }

    static class Kernel32
    {
        
        // More info about these definitions is at:
        //      https://docs.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-memory_basic_information
         

        // PAGE_READWRITE = 0x04
        public const int DesiredPageProtection = 0x04;

        // MEM_COMMIT = 0x1000
        public const int DesiredPageState = 0x1000;

        // MEM_PRIVATE = 0x20000
        public const int DesiredPageType = 0x20000;

        [StructLayout(LayoutKind.Sequential)]
        unsafe public struct MEMORY_BASIC_INFORMATION
        {
            public byte* BaseAddress;
            public byte* AllocationBase;
            public uint AllocationProtect;
            public ulong RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        [StructLayout(LayoutKind.Sequential)]
        unsafe public struct SYSTEM_INFO
        {
            public uint dwOemId;
            public uint dwPageSize;
            public byte* lpMinimumApplicationAddress;
            public byte* lpMaximumApplicationAddress;
            public uint dwActiveProcessorMask;
            public uint dwNumberOfProcessors;
            public uint dwProcessorType;
            public uint dwAllocationGranularity;
            public ushort wProcessorLevel;
            public ushort wProcessorRevision;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        unsafe public static extern int
        VirtualQuery(
            byte* lpAddress,
            out MEMORY_BASIC_INFORMATION lpBuffer,
            int dwLength
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        unsafe public static extern void
        GetSystemInfo(
            [MarshalAs(UnmanagedType.Struct)] out SYSTEM_INFO lpSystemInfo
        );
    }
    */

}
