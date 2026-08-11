using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SimplifiedMemoryManager
{
    /// <summary>
    /// Abstracts OS-specific process memory operations.
    /// Windows uses kernel32.dll via P/Invoke.
    /// Linux uses /proc/{pid}/mem and /proc/{pid}/maps.
    /// </summary>
    internal interface IPlatformMemory : IDisposable
    {
        /// <summary>Opens a handle/stream to the target process.</summary>
        void OpenProcess(Process process);

        /// <summary>Reads bytes from an absolute memory address.</summary>
        void ReadBytes(IntPtr address, byte[] buffer, int pid);

        /// <summary>Writes bytes to an absolute memory address.</summary>
        void WriteBytes(IntPtr address, byte[] data);

        /// <summary>
        /// Forces read/write permissions at the given address (Windows: VirtualProtectEx).
        /// No-op on Linux — /proc/mem already allows writing to writable regions.
        /// </summary>
        void ForceReadWritePermissions(IntPtr address, long byteCount);
        
        /// <summary>
        /// Returns readable memory regions for a full process scan.
        /// Returns null if the platform handles this via process.Modules instead.
        /// </summary>
        IEnumerable<(long start, int size, string name)> GetReadableRegionsWithName(int pid);
        
        int[] ReadMultipleRegions((IntPtr address, byte[] buffer)[] regions, int pid);

        IntPtr FindPeBaseAddress(int pid, string processName);

        /// <summary>Closes the handle/stream to the target process.</summary>
        void Close();
    }
}
