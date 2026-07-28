using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SimplifiedMemoryManager
{
    public class SimpleProcessProxy : IDisposable
    {
        /// <summary>
        /// This object is handled slightly differently for different platforms(Windows/Linux). For Linux, the Offset
        /// should already be the absolute location within system memory, and the BaseAddress is included as a sanity
        /// check if you need it. For Windows, the included BaseAddress is *pre-emptively* added to the Offset where
        /// found in system memory. This is due to the difference in nature between memory management between platforms.
        /// BaseAddress is once again included for sanity, and so you may more simply *subtract* it from the Offset if
        /// this default implementation does not work for you. The intention with this is to allow you to use the Offset
        /// value in conjunction with the "GetMemoryFromPointer" method to access the memory represented by SimpleMemory
        /// on both Windows and Linux platforms without needing to include platform-specific logic.
        /// </summary>
        public class SimpleMemory
        {
            public IntPtr Offset { get; set; }
            public IntPtr ModuleBaseAddress { get; set; }

            public SimpleMemory(IntPtr offset, IntPtr baseAddress)        
            {
                Offset = offset;
                ModuleBaseAddress = baseAddress;
            }
        }

        #region Internals
        private bool _disposedValue;

        public static Process ProcessToProxy { get; set; }
        private static string ProcessName { get; set; }
        private static IntPtr ProcessBaseAddress { get; set; }

        // ── Cross-platform memory backend ──────────────────────────────────────
        // Chosen once at construction time based on the OS we're running on.
        // All private methods below call _platform instead of NativeMethods directly.
        private readonly IPlatformMemory _platform;

        public SimpleProcessProxy(Process process)
        {
            ProcessToProxy = process ?? throw new SimpleProcessProxyException("You must provide a process to modify.");
            ProcessBaseAddress = process.MainModule.BaseAddress;
            ProcessName = process.ProcessName;

            // Select the right backend for the current OS
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                _platform = new WindowsPlatformMemory();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _platform = new LinuxPlatformMemory();
                IntPtr peBase = ((LinuxPlatformMemory)_platform).FindPeBaseAddress(process.Id, process.ProcessName);
                
                if (peBase != IntPtr.Zero)
                {
                    Console.WriteLine($"[SMM] Overriding base address: {ProcessBaseAddress} -> {peBase}");
                    ProcessBaseAddress = peBase;
                }
                else
                {
                    Console.WriteLine($"[SMM] Warning: Could not find PE base in /proc/maps, " +
                                      $"using MainModule.BaseAddress: {ProcessBaseAddress}");
                }
            }
            else
                throw new PlatformNotSupportedException(
                    "SimplifiedMemoryManager only supports Windows and Linux.");
        }

        private void OpenProcess()
        {
            ValidateProcessToProxy();
            _platform.OpenProcess(ProcessToProxy);
        }

        private void ValidateProcessToProxy()
        {
            if (ProcessToProxy == null || ProcessToProxy.HasExited)
            {
                ProcessToProxy = Process.GetProcessesByName(ProcessName).FirstOrDefault();
                if (ProcessToProxy == default)
                    throw new SimpleProcessProxyException($"Failed to find process {ProcessName} to proxy");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _platform?.Dispose();
                }

                ProcessName = null;
                ProcessToProxy = null;
                _disposedValue = true;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region Private (support) methods

        private byte[] GetProcessSnapshot(long processSize)
        {
            byte[] buffer = new byte[processSize];
            try
            {
                OpenProcess();
                _platform.ReadBytes(ProcessBaseAddress, buffer, ProcessToProxy.Id);
            }
            catch (Exception e)
            {
                throw new SimpleProcessProxyAggregateException($"Failed to read process `{ProcessName}`. Is it running?", e);
            }
            finally
            {
                _platform.Close();
            }
            return buffer;
        }

        private byte[] GetMemory(IntPtr offset, long valueSize)
        {
            try
            {
                OpenProcess();

                long address = ProcessBaseAddress.ToInt64() + offset.ToInt64();
                IntPtr addressToRead = new IntPtr(address);

                byte[] bytesRead = new byte[valueSize];
                ReadBytesFromMemory(addressToRead, bytesRead);

                return bytesRead;
            }
            finally
            {
                _platform.Close();
            }
        }

        private byte[] GetMemoryOutsideMainModule(IntPtr offset, long valueSize)
        {
            try
            {
                OpenProcess();

                byte[] bytesRead = new byte[valueSize];
                ReadBytesFromMemory(offset, bytesRead);

                return bytesRead;
            }
            finally
            {
                _platform.Close();
            }
        }

        private void SetMemory(IntPtr desiredOffset, byte[] value, bool forceWrite)
        {
            try
            {
                OpenProcess();

                long address = ProcessBaseAddress.ToInt64() + desiredOffset.ToInt64();
                IntPtr addressToModify = new IntPtr(address);

                if (forceWrite)
                {
                    _platform.ForceReadWritePermissions(addressToModify, value.Length);
                }

                WriteBytesToMemory(addressToModify, value);
            }
            catch (Exception e)
            {
                throw new SimpleProcessProxyException(
                    $"Something unexpected went wrong when trying to modify the process' memory! {e}");
            }
            finally
            {
                _platform.Close();
            }
        }

        private void SetMemoryOutsideMainModule(IntPtr offset, byte[] value)
        {
            try
            {
                OpenProcess();
                WriteBytesToMemory(offset, value);
            }
            catch (Exception e)
            {
                throw new SimpleProcessProxyException(
                    $"Something unexpected went wrong when trying to modify the process' memory! {e}");
            }
            finally
            {
                _platform.Close();
            }
        }

        private void WriteBytesToMemory(IntPtr objectAddress, byte[] bytesToWrite)
        {
            // Delegates entirely to the platform backend — no OS checks needed here.
            _platform.WriteBytes(objectAddress, bytesToWrite);
        }

        private byte[] ReadBytesFromMemory(IntPtr objectAddress, byte[] bytesToRead)
        {
            // Delegates entirely to the platform backend — no OS checks needed here.
            _platform.ReadBytes(objectAddress, bytesToRead, ProcessToProxy.Id);
            return bytesToRead;
        }

        // EnableDisablePrivilege and ForceReadWritePermissionsAdmin are Windows-only
        // concepts (SeDebugPrivilege / VirtualProtectEx). They are preserved below
        // but guarded so they cannot be called on Linux.
        private static void EnableDisablePrivilege(string PrivilegeName, bool EnableDisable)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("EnableDisablePrivilege is Windows-only.");

            if (!NativeMethods.LookupPrivilegeValue(null, PrivilegeName, out var luid))
                throw new Exception($"EnableDisablePrivilege: LookupPrivilegeValue failed: {Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()).Message}");

            if (!NativeMethods.OpenProcessToken(Process.GetCurrentProcess().SafeHandle, TokenAccessLevels.AdjustPrivileges, out var tokenHandle))
                throw new Exception($"EnableDisablePrivilege: OpenProcessToken failed: {Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()).Message}");

            var tokenPrivileges = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privileges = new[] { new LUID_AND_ATTRIBUTES { LUID = luid, Attributes = (uint)(EnableDisable ? 2 : 4) } }
            };

            if (!NativeMethods.AdjustTokenPrivileges(tokenHandle, false, ref tokenPrivileges, 0, IntPtr.Zero, out _))
            {
                tokenHandle.Dispose();
                throw new Exception($"EnableDisablePrivilege: AdjustTokenPrivileges failed: {Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()).Message}");
            }
            else tokenHandle.Dispose();
        }

        #endregion

        #region Public methods — UNCHANGED from original

        public void InvertBooleanValue(IntPtr memoryOffset, int booleanSize = 1, bool forceWritability = false)
        {
            byte[] currentValue = GetMemory(memoryOffset, booleanSize);
            byte[] valueToWrite = new byte[booleanSize];

            if (Enumerable.SequenceEqual(currentValue, BitConverter.GetBytes(true)))
                BitConverter.GetBytes(false).CopyTo(valueToWrite, 0);
            else
                BitConverter.GetBytes(true).CopyTo(valueToWrite, 0);

            try
            {
                SetMemory(memoryOffset, valueToWrite, forceWritability);
            }
            catch (Exception e)
            {
                throw new SimpleProcessProxyAggregateException($"Failed to write boolean value at {memoryOffset}", e);
            }
        }

        #region ModifyProcessOffset overloads
        public void ModifyProcessOffset(IntPtr memoryOffset, short offsetValueToWrite, bool forceWritability = false)
        {
            try { SetMemory(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability); }
            catch (Exception e) { throw new SimpleProcessProxyAggregateException($"Failed to write short value at {memoryOffset}", e); }
        }

        public void ModifyProcessOffset(IntPtr memoryOffset, int offsetValueToWrite, bool forceWritability = false)
        {
            try { SetMemory(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability); }
            catch (Exception e) { throw new SimpleProcessProxyAggregateException($"Failed to write int value at {memoryOffset}", e); }
        }

        public void ModifyProcessOffset(IntPtr memoryOffset, long offsetValueToWrite, bool forceWritability = false)
        {
            try { SetMemory(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability); }
            catch (Exception e) { throw new SimpleProcessProxyAggregateException($"Failed to write long value at {memoryOffset}", e); }
        }

        public void ModifyProcessOffset(IntPtr memoryOffset, double offsetValueToWrite, bool forceWritability = false)
        {
            try { SetMemory(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability); }
            catch (Exception e) { throw new SimpleProcessProxyAggregateException($"Failed to write double value at {memoryOffset}", e); }
        }

        public void ModifyProcessOffset(IntPtr memoryOffset, float offsetValueToWrite, bool forceWritability = false)
        {
            try { SetMemory(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability); }
            catch (Exception e) { throw new SimpleProcessProxyAggregateException($"Failed to write float value at {memoryOffset}", e); }
        }

        public void ModifyProcessOffset(IntPtr memoryOffset, bool offsetValueToWrite, bool forceWritability = false)
        {
            try { SetMemory(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability); }
            catch (Exception e) { throw new SimpleProcessProxyAggregateException($"Failed to write bool value at {memoryOffset}", e); }
        }

        public void ModifyProcessOffset(IntPtr memoryOffset, char offsetValueToWrite, bool forceWritability = false)
        {
            try { SetMemory(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability); }
            catch (Exception e) { throw new SimpleProcessProxyAggregateException($"Failed to write char value at {memoryOffset}", e); }
        }

        public void ModifyProcessOffset(IntPtr memoryOffset, byte[] offsetValueToWrite, bool forceWritability = false)
        {
            try { SetMemory(memoryOffset, offsetValueToWrite, forceWritability); }
            catch (Exception e) { throw new SimpleProcessProxyAggregateException($"Failed to write byte[] at {memoryOffset}", e); }
        }

        public void ModifyProcessOffset(IntPtr memoryOffset, string offsetValueToWrite, bool forceWritability = false)
        {
            try { SetMemory(memoryOffset, Encoding.Default.GetBytes(offsetValueToWrite), forceWritability); }
            catch (Exception e) { throw new SimpleProcessProxyAggregateException($"Failed to write string value at {memoryOffset}", e); }
        }
        #endregion

        public byte[] ReadProcessOffset(IntPtr memoryOffset, long bytesToRead)
        {
            try
            {
                return GetMemory(memoryOffset, bytesToRead);
            }
            catch (Exception e)
            {
                throw new SimpleProcessProxyAggregateException($"Failed to read value at {memoryOffset}", e);
            }
        }

        public byte[] GetProcessSnapshot()
        {
            try { return GetProcessSnapshot(ProcessToProxy.PeakPagedMemorySize64); }
            catch (Exception e) { throw new SimpleProcessProxyAggregateException("Failed to get full-state of process", e); }
        }

        /// <summary>
        /// Scan memory related to the process you are proxying for a specified SimplePattern. Provide only
        /// the pattern to scan ALL available memory until the first result is found, optionally provide a byte array
        /// to scan just that array for the SimplePattern.
        ///
        /// WARNING: This operation is very slow on Linux systems. Use sparingly if you want cross-platform functionality.
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="memoryToScan"></param>
        /// <returns>SimpleMemory obj representing the memory locations of the data, if found.</returns>
        /// <exception cref="SimpleProcessProxyException"></exception>
        public SimpleMemory ScanMemoryForUniquePattern(SimplePattern pattern, byte[] memoryToScan = null)
        {
            ScanManager scanManager = new ScanManager();
            //_platform.Close();

            lock (ProcessToProxy)
            {
                if (memoryToScan != null)
                {
                    scanManager.ByteArrayScan(memoryToScan, pattern);
                }
                else
                {
                    if (_platform is WindowsPlatformMemory)
                    {
                        // Windows path — scan by process modules
                        scanManager.FullProcessScan(pattern, ProcessToProxy, GetMemoryOutsideMainModule);
                    }
                    else
                    {
                        List<(long, int, string)> regions = _platform.GetReadableRegionsWithName(ProcessToProxy.Id)?.ToList();
                        if (regions != null)
                        {
                            // Linux path — scan by memory region from /proc/{pid}/maps
                            List<IntPtr> found = LinuxRegionScan(pattern, regions);
                            return found.Count == 0
                                ? throw new SimpleProcessProxyException("Pattern not found in process memory. (LINUX)")
                                : new SimpleMemory(found[0], ProcessBaseAddress);
                        }
                    }
                }
            }

            return scanManager.ScanResult.Count == 0 
                ? throw new SimpleProcessProxyException("Pattern not found in process memory. (WINDOWS)") 
                : new SimpleMemory(scanManager.ScanResult.First().Item1, scanManager.ScanResult.First().Item2.BaseAddress);
        }
        
        public IntPtr FollowPointer(IntPtr pointer, bool bigEndian, int sizeOfPointer = 8)
        {
            try
            {
                OpenProcess();

                byte[] memoryPointedTo = new byte[sizeOfPointer];
                ReadBytesFromMemory(
                    new IntPtr(ProcessBaseAddress.ToInt64() + pointer.ToInt64()),
                    memoryPointedTo);

                if (bigEndian)
                    memoryPointedTo = memoryPointedTo.Reverse().ToArray();

                if (!Environment.Is64BitOperatingSystem)
                    return new IntPtr(BitConverter.ToInt32(memoryPointedTo, 0));

                if (sizeOfPointer < 8)
                {
                    List<byte> padded = new List<byte>(memoryPointedTo);
                    for (int i = 0; i < sizeOfPointer; i++) padded.Add(0);
                    memoryPointedTo = padded.ToArray();
                }

                return new IntPtr(BitConverter.ToInt64(memoryPointedTo, 0));
            }
            finally
            {
                _platform.Close();
            }
        }

        public byte[] GetMemoryFromPointer(IntPtr pointer, int size)
            => GetMemoryOutsideMainModule(pointer, size);

        public void SetMemoryAtPointer(IntPtr pointer, byte[] data)
            => SetMemoryOutsideMainModule(pointer, data);

        /// <summary>
        /// Scan memory related to the process you are proxying for a specified SimplePattern. Provide only
        /// the pattern to scan ALL available memory top-to-bottom and get all results, optionally provide a byte array
        /// to scan just that array for the SimplePattern, and/or optionally provide a quantity to restrict your results
        /// to the first however many results you've specified.
        ///
        /// WARNING: This operation is very slow on Linux systems. Use sparingly if you want cross-platform functionality.
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="memoryToScan"></param>
        /// <param name="quantityToFind"></param>
        /// <returns>SimpleMemory obj representing the memory locations of the data, if found.</returns>
        /// <exception cref="SimpleProcessProxyException"></exception>
        public List<SimpleMemory> ScanMemoryForPattern(SimplePattern pattern, byte[] memoryToScan = null, int quantityToFind = -1)
        {
            switch (quantityToFind)
            {
                case 0:
                    throw new SimpleProcessProxyException("Invalid quantity to find. Provide a positive integer.");
                case 1:
                    throw new SimpleProcessProxyException("Invalid quantity to find. Use ScanMemoryForUniquePattern to find only one result.");
            }

            ScanManager scanManager = new ScanManager(quantityToFind);
            List<SimpleMemory> results;

            if (memoryToScan != null)
            {
                scanManager.ByteArrayScan(memoryToScan, pattern);
            }
            else
            {
                if (_platform is WindowsPlatformMemory)
                {
                    // Windows path — scan by process modules
                    scanManager.FullProcessScan(pattern, ProcessToProxy, GetMemoryOutsideMainModule);
                }
                else
                {
                    var regions = _platform.GetReadableRegionsWithName(ProcessToProxy.Id)?.ToList();
                    //Console.WriteLine($"Direct GetReadableRegions count: {regions?.Count ?? -1}");
                    
                    if (regions == null)
                        throw new SimpleProcessProxyException("No regions to read on this process, cannot scan memory");
                    
                    List<IntPtr> found = LinuxRegionScan(pattern, regions, quantityToFind);

                    if (found.Count == 0)
                        throw new SimpleProcessProxyException("Pattern not found in process memory.");
                    
                    results = new List<SimpleMemory>();
                    foreach (IntPtr result in found)
                    {
                        results.Add(new SimpleMemory(result, ProcessBaseAddress));
                    }

                    return results;
                }
            }
            scanManager.InitiateScan();

            if (scanManager.ScanResult.Count == 0)
                throw new SimpleProcessProxyException("Pattern not found in process memory.");

            results = new List<SimpleMemory>();
            foreach(var result in scanManager.ScanResult)
                results.Add(new SimpleMemory(result.Item1, result.Item2.BaseAddress));
            
            return results;
        }
        
        private List<IntPtr> LinuxRegionScan(SimplePattern pattern,
            List<(long start, int size, string name)> regionList, int quantityToFind = -1)
        {
            List<IntPtr> results = new List<IntPtr>();
            List<PatternExpression> parsed = pattern.ParsedPattern;
            int patternLength = parsed.Count;
            int pid = ProcessToProxy.Id;

            for (int r = 0; r < regionList.Count; r++)
            {
                var (start, size, name) = regionList[r];
                long nextStart = (r + 1 < regionList.Count)
                    ? regionList[r + 1].start
                    : start + size;

                long rawReadSize = nextStart - start;
                int readSize = (rawReadSize <= 0 || rawReadSize > size * 2 || rawReadSize > 256 * 1024 * 1024)
                    ? size
                    : (int)rawReadSize;

                try
                {
                    byte[] buffer = new byte[readSize];
                    _platform.ReadBytes(new IntPtr(start), buffer, pid);

                    for (int i = 0; i <= buffer.Length - patternLength; i++)
                    {
                        bool match = true;
                        for (int j = 0; j < patternLength; j++)
                        {
                            PatternExpression expr = parsed[j];
                            if (expr.Operation == Operation.SkipOne) continue;
                            if (expr.Operation == Operation.Exact && expr.Operand != buffer[i + j])
                            {
                                match = false;
                                break;
                            }
                        }

                        if (match)
                        {
                            results.Add(new IntPtr(start + i));
                            if (quantityToFind > 0 && results.Count >= quantityToFind)
                                return results;
                        }
                    }
                }
                catch { continue; }
            }

            return results;
        }
        #endregion
    }
}
