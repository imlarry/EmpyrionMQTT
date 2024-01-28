/*
 * Copyright (C) [year]  [name of author]
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 * 
 * 1/27/2024 - Modified by:  imlarry .. added ability to return all star data
 */
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Eleon.Modding;
using static StarScraper.StarFinder;

namespace StarScraper
{
    /// <summary>
    /// Scans application memory to find star position data residing within.
    /// </summary>
    public unsafe class StarFinder : IStarFinder
    {
        // for the call to TryStartNoGCRegion. 2MB should be plenty of room for this op
        private const long NoGCSize = 2097152;

        private const int SizeOfStarData = 24;
        private const int StarCountThreshold = 1000;
        private const long RegionSizeThreshold = SizeOfStarData * StarCountThreshold;
        // this value was chosen after several runs and should provide enough capacity
        // that we don't lose time to resizing
        // this is a constant race against the GC after all
        private const int RegionListInitialCapacity = 293;

        private bool noResumeGC = false;

        private readonly Kernel32.SYSTEM_INFO sysInfo;
        private readonly int memInfoSize;

        public StarFinder()
        {
            Kernel32.GetSystemInfo(out sysInfo);
            Kernel32.MEMORY_BASIC_INFORMATION memInfo = default;
            memInfoSize = Marshal.SizeOf(memInfo);
        }

        unsafe public VectorInt3[] Search(VectorInt3 knownPosition)
        {
            PauseGC();

            var mbi = new List<Kernel32.MEMORY_BASIC_INFORMATION>(RegionListInitialCapacity);
            Kernel32.MEMORY_BASIC_INFORMATION memInfo = default;
            // have to go bottom to top to get the regions
            // bc VirtualQuery only rounds down to the page boundary, not the region boundary
            // so counting backward is a nightmare
            while (NextRegion(ref memInfo))
            {
                if (memInfo.RegionSize >= RegionSizeThreshold)
                {
                    mbi.Add(memInfo);
                }
            }
            mbi.Reverse();

            var arr = default(StarDataArray);
            foreach (var mem in mbi)
            {
                void* limit = mem.BaseAddress + mem.RegionSize - SizeOfStarData;

                // assumption: the star data are 32-bit aligned
                int* addr = (int*)mem.BaseAddress;

                while (addr < limit)
                {
                    if (CouldBeStarData((StarData*)addr, knownPosition)
                        && TryVerifyArrayFound(mem, (StarData*)addr, ref arr))
                    {
                        ResumeGC();
                        return ExtractStarPositions(arr);
                    }
                    addr++;
                }
            }

            ResumeGC();
            return null;
        }

        // NEW .. added method to return all star data
        unsafe public StarData[] SearchStarData(VectorInt3 knownPosition)
        {
            PauseGC();

            var mbi = new List<Kernel32.MEMORY_BASIC_INFORMATION>(RegionListInitialCapacity);
            Kernel32.MEMORY_BASIC_INFORMATION memInfo = default;
            while (NextRegion(ref memInfo))
            {
                if (memInfo.RegionSize >= RegionSizeThreshold)
                {
                    mbi.Add(memInfo);
                }
            }
            mbi.Reverse();

            var arr = default(StarDataArray);
            foreach (var mem in mbi)
            {
                void* limit = mem.BaseAddress + mem.RegionSize - SizeOfStarData;
                int* addr = (int*)mem.BaseAddress;

                while (addr < limit)
                {
                    if (CouldBeStarData((StarData*)addr, knownPosition)
                        && TryVerifyArrayFound(mem, (StarData*)addr, ref arr))
                    {
                        ResumeGC();
                        return ExtractStarData(arr);
                    }
                    addr++;
                }
            }

            ResumeGC();
            return null;
        }

        unsafe private bool CouldBeStarData(StarData* sd, VectorInt3 sc)
        {
            return (sd->x == sc.x)
                && (sd->y == sc.y)
                && (sd->z == sc.z);
        }


        unsafe private bool TryVerifyArrayFound(Kernel32.MEMORY_BASIC_INFORMATION memInfo,
            StarData* sd, ref StarDataArray arr)
        {
            if (sd->id < 0)
                return false;

            sd -= sd->id;
            if (sd < memInfo.BaseAddress)
                return false;

            StarData* begin = sd;
            void* limit = memInfo.BaseAddress + memInfo.RegionSize - SizeOfStarData;
            int expected = 0;
            while (sd <= limit && sd->id == expected)
            {
                expected++;
                sd++;
            }
            if (expected > StarCountThreshold)
            {
                arr = new StarDataArray(begin, expected);
                return true;
            }
            return false;
        }

        /**
         * Returns true if memInfo describes a valid region for searching.
         */
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
                    // ignore the error and move on to the next page
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
                bool success = GC.TryStartNoGCRegion(NoGCSize); // NEW .. check for success
                if (!success)
                {
                    // Handle failure to enter no GC region
                    throw new InvalidOperationException("Failed to enter no GC region.");
                }
            }
            catch (InvalidOperationException)
            {
                // TODO: provide a way to log this
                // if this happens, someone else already put us in NoGC mode
                // so we must NOT be the one to change it back
                noResumeGC = true;
            }
            catch
            {
                // StarFinder must continue whether or not GC is suspended
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
                // TODO: provide a way to log this?
                // nothing to do; just move on
            }
        }

        unsafe private VectorInt3[] ExtractStarPositions(StarDataArray starDataArray)
        {
            var starPosition = new VectorInt3[starDataArray.count];
            for (int i = 0; i < starPosition.Length; i++)
            {
                starPosition[i] = new VectorInt3(
                    starDataArray[i].x,
                    starDataArray[i].y,
                    starDataArray[i].z
                );
            }
            return starPosition;
        }

        // NEW .. added method to extract raw star data
        unsafe private StarData[] ExtractStarData(StarDataArray starDataArray)
        {
            var starData = new StarData[starDataArray.count];
            for (int i = 0; i < starData.Length; i++)
            {
                starData[i] = starDataArray[i];
            }
            return starData;
        }

        // NEW .. hoisted constant declaration
        public const int SectorsPerLY = 100000; 
        
        private bool LooksLikeStarPosition(int x, int y, int z)
        {
            return x % SectorsPerLY == 0
                && y % SectorsPerLY == 0
                && z % SectorsPerLY == 0;
        }

        /*
         * This struct represents the star data as found in the game's memory.
         * intended for extracting position data via pointer casting.
         */
        [StructLayout(LayoutKind.Sequential)]
        public readonly struct StarData
        {
            // i don't know what the first 8 bytes are for
            public readonly int a; // NEW .. exposed field
            public readonly int b; // NEW .. exposed field
            public readonly int x;
            public readonly int y;
            public readonly int z;
            public readonly int id;

            public StarData(int a, int b, int x, int y, int z, int id)
            {
                this.a = a;
                this.b = b;
                this.x = x;
                this.y = y;
                this.z = z;
                this.id = id;
            }
        }
        // if desired these sorts of calls can be used to convert the ints into
        // byte[] aBytes = BitConverter.GetBytes(starData.a);
        // byte[] bBytes = BitConverter.GetBytes(starData.b);  

        private readonly unsafe struct StarDataArray
        {
            public readonly StarData* baseAddress;
            public readonly int count;

            public StarDataArray(StarData* baseAddress, int count)
            {
                this.baseAddress = baseAddress;
                this.count = count;
            }

            public StarData this[int i] { get => baseAddress[i]; }
        }
    }

    static class Kernel32
    {
        /* 
         * More info about these definitions is at:
         * https://docs.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-memory_basic_information
         */

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
}
