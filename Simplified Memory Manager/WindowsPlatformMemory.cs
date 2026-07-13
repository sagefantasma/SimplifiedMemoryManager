using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SimplifiedMemoryManager
{
    /// <summary>
    /// Windows implementation — delegates to kernel32.dll exactly as before.
    /// Nothing in the original NativeMethods calls has changed; they've just
    /// been moved behind the IPlatformMemory interface.
    /// </summary>
    internal class WindowsPlatformMemory : IPlatformMemory
    {
        private IntPtr _handle = IntPtr.Zero;

        public void OpenProcess(Process process)
        {
            _handle = NativeMethods.OpenProcess(
                AccessPrivileges.AllAccess | AccessPrivileges.ProcessVMOperation,
                false,
                process.Id);
        }

        public void ReadBytes(IntPtr address, byte[] buffer, int pid)
        {
            bool success = NativeMethods.ReadProcessMemory(
                _handle, address, buffer, (long)buffer.Length, out long bytesRead);

            if (!success || bytesRead != buffer.Length)
                throw new SimpleProcessProxyException(
                    $"ReadProcessMemory failed at {address}. Read {bytesRead}/{buffer.Length} bytes.");
        }

        public void WriteBytes(IntPtr address, byte[] data)
        {
            bool success = NativeMethods.WriteProcessMemory(
                _handle, address, data, (uint)data.Length, out int bytesWritten);

            if (!success || bytesWritten != data.Length)
                throw new SimpleProcessProxyException(
                    $"WriteProcessMemory failed at {address}. Wrote {bytesWritten}/{data.Length} bytes.");
        }

        public void ForceReadWritePermissions(IntPtr address, long byteCount)
        {
            bool success;
            lock (this)
            {
                success = NativeMethods.VirtualProtectEx(
                    _handle, address, byteCount,
                    AccessPrivileges.ExecuteReadWrite, out _);
            }

            if (!success)
                throw new SimpleProcessProxyException(
                    $"VirtualProtectEx failed at {address}: {Marshal.GetLastWin32Error()}");
        }
        
        public IEnumerable<(long start, int size, string name)> GetReadableRegionsWithName(int pid) => null;
        
        public int[] ReadMultipleRegions((IntPtr address, byte[] buffer)[] regions, int pid)
        {
            // Windows doesn't batch — just read individually
            var results = new int[regions.Length];
            for (int i = 0; i < regions.Length; i++)
            {
                try
                {
                    ReadBytes(regions[i].address, regions[i].buffer, pid);
                    results[i] = regions[i].buffer.Length;
                }
                catch { results[i] = 0; }
            }
            return results;
        }

        public IntPtr FindPeBaseAddress(int pid, string processName) => IntPtr.Zero;

        public void Close()
        {
            if (_handle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(_handle);
                _handle = IntPtr.Zero;
            }
        }

        public void Dispose() => Close();
    }
}
