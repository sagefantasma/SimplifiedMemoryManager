using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SimplifiedMemoryManager
{
    
    /// <summary>
    /// Linux implementation — reads and writes process memory via the
    /// virtual filesystem exposed by the kernel at /proc/{pid}/mem.
    ///
    /// IMPORTANT: Linux restricts cross-process memory access via ptrace.
    /// Your tool managing memory must either:
    ///   (a) run as root (sudo), or
    ///   (b) set /proc/sys/kernel/yama/ptrace_scope to 0
    ///       (echo 0 | sudo tee /proc/sys/kernel/yama/ptrace_scope)
    /// Without one of these, opening /proc/{pid}/mem on another process
    /// will throw UnauthorizedAccessException.
    /// </summary>
    public class LinuxPlatformMemory : IPlatformMemory
    {
        private int _pid;
        private FileStream _memStream;
        
        [StructLayout(LayoutKind.Sequential)]
        private struct iovec
        {
            public IntPtr iov_base; // pointer to buffer
            public UIntPtr iov_len; // buffer length
        }

        [DllImport("libc", SetLastError = true)]
        private static extern nint process_vm_readv(
            int pid,
            iovec[] local_iov,   // where to write data (our buffer)
            ulong liovcnt,       // number of local iovecs
            iovec[] remote_iov,  // where to read from (target process)
            ulong riovcnt,       // number of remote iovecs
            ulong flags);        // always 0
        
        public void OpenProcess(Process process)
        {
            // Close any previously opened stream first
            Close();

            _pid = process.Id;

            // /proc/{pid}/mem gives raw access to the process address space.
            // FileShare.ReadWrite is required because the kernel keeps the
            // file "open" on behalf of the target process.
            _memStream = new FileStream(
                $"/proc/{_pid}/mem",
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.ReadWrite);
        }
        
        public void ReadBytes(IntPtr address, byte[] buffer, int pid)
        {
            //EnsureOpen();
    
            // Pin the buffer so the GC doesn't move it during the syscall
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var localIov = new iovec[]
                {
                    new iovec
                    {
                        iov_base = handle.AddrOfPinnedObject(),
                        iov_len = (UIntPtr)buffer.Length
                    }
                };

                var remoteIov = new iovec[]
                {
                    new iovec
                    {
                        iov_base = address,
                        iov_len = (UIntPtr)buffer.Length
                    }
                };

                nint bytesRead = process_vm_readv(pid, localIov, 1, remoteIov, 1, 0);

                if (bytesRead < 0)
                {
                    int errno = Marshal.GetLastWin32Error();
                    throw new SimpleProcessProxyException(
                        $"process_vm_readv failed at {address}: errno {errno}. " +
                        "Check ptrace_scope permissions.");
                }

                // Zero out anything unread
                if (bytesRead < buffer.Length)
                    Array.Clear(buffer, (int)bytesRead, buffer.Length - (int)bytesRead);
            }
            finally
            {
                handle.Free();
            }
        }
        
        /// <summary>
        /// Reads multiple non-contiguous regions in a single syscall.
        /// Returns how many bytes were successfully read into each buffer.
        /// </summary>
        public int[] ReadMultipleRegions(
            (IntPtr address, byte[] buffer)[] regions, int pid)
        {
            Console.WriteLine($"ReadMultipleRegions pid: {pid}");
            var handles = new GCHandle[regions.Length];
            var results = new int[regions.Length];

            try
            {
                // Pin all buffers
                for (int i = 0; i < regions.Length; i++)
                    handles[i] = GCHandle.Alloc(regions[i].buffer, GCHandleType.Pinned);

                var localIovs = regions.Select((r, i) => new iovec
                {
                    iov_base = handles[i].AddrOfPinnedObject(),
                    iov_len = (UIntPtr)r.buffer.Length
                }).ToArray();

                var remoteIovs = regions.Select(r => new iovec
                {
                    iov_base = r.address,
                    iov_len = (UIntPtr)r.buffer.Length
                }).ToArray();

                // Single syscall reads all regions at once
                nint totalRead = process_vm_readv(
                    pid,
                    localIovs, (ulong)localIovs.Length,
                    remoteIovs, (ulong)remoteIovs.Length,
                    0);
                
                Console.WriteLine($"process_vm_readv returned {totalRead}, errno={Marshal.GetLastWin32Error()}");

                // Distribute bytes read across results
                // process_vm_readv fills iovecs sequentially
                long remaining = totalRead;
                for (int i = 0; i < regions.Length; i++)
                {
                    int regionSize = regions[i].buffer.Length;
                    results[i] = (int)Math.Min(remaining, regionSize);
                    remaining -= results[i];
                    if (remaining <= 0) break;
                }
            }
            finally
            {
                foreach (var h in handles)
                    if (h.IsAllocated) h.Free();
            }

            return results;
        }

        public void ReadBytesSeeker(IntPtr address, byte[] buffer)
        {
            EnsureOpen();
            try
            {
                _memStream.Seek(address.ToInt64(), SeekOrigin.Begin);
        
                int totalRead = 0;
                while (totalRead < buffer.Length)
                {
                    int bytesRead = _memStream.Read(buffer, totalRead, buffer.Length - totalRead);
                    if (bytesRead == 0) break; // kernel stopped giving us data
                    totalRead += bytesRead;
                }

                if (totalRead == 0)
                    throw new SimpleProcessProxyException(
                        $"Failed to read any bytes from /proc/{_pid}/mem at {address}.");

                // Zero out anything we couldn't read rather than leaving garbage
                if (totalRead < buffer.Length)
                    Array.Clear(buffer, totalRead, buffer.Length - totalRead);
            }
            catch (SimpleProcessProxyException) { throw; }
            catch (IOException ex)
            {
                throw new SimpleProcessProxyAggregateException(
                    $"Failed to read /proc/{_pid}/mem at {address}. " +
                    "Check ptrace_scope permissions.", ex);
            }
        }

        public void WriteBytes(IntPtr address, byte[] data)
        {
            EnsureOpen();
            try
            {
                _memStream.Seek(address.ToInt64(), SeekOrigin.Begin);
                _memStream.Write(data, 0, data.Length);
                _memStream.Flush();
            }
            catch (IOException ex)
            {
                throw new SimpleProcessProxyAggregateException(
                    $"Failed to write /proc/{_pid}/mem at {address}. " +
                    "Check ptrace_scope permissions.", ex);
            }
        }

        /// <summary>
        /// No-op on Linux. Memory permissions are managed by the kernel's
        /// page table and cannot be changed from outside the process via
        /// /proc/mem. If a region is writable from inside the process it
        /// is writable here too; read-only pages (e.g. code segments) will
        /// throw on WriteBytes regardless.
        /// </summary>
        public void ForceReadWritePermissions(IntPtr address, long byteCount)
        {
            // Intentionally left empty on Linux.
            // If you need to patch read-only memory on Linux, look into
            // ptrace PTRACE_POKEDATA, which is significantly more complex.
        }

        public void Close()
        {
            _memStream?.Dispose();
            _memStream = null;
        }

        public void Dispose() => Close();

        // ── Helpers ────────────────────────────────────────────────────────────

        private void EnsureOpen()
        {
            if (_memStream == null)
                throw new SimpleProcessProxyException(
                    "LinuxPlatformMemory: OpenProcess must be called before reading or writing.");
        }

        /// <summary>
        /// Returns all readable memory regions for the current process by
        /// parsing /proc/{pid}/maps. Used by FullProcessScan in ScanManager.
        /// </summary>
        public IEnumerable<(long start, int size, string name)> GetReadableRegionsWithName(int pid)
        {
            string mapsPath = $"/proc/{pid}/maps";
            if (!File.Exists(mapsPath))
                yield break;
        
            foreach (string line in File.ReadLines(mapsPath))
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                if (!parts[1].Contains("r")) continue;
                if (parts[1].Contains("x")) continue; // skip executable-only regions

                var range = parts[0].Split('-');
                if (range.Length != 2) continue;
                if (!long.TryParse(range[0], System.Globalization.NumberStyles.HexNumber, null, out long start)) continue;
                if (!long.TryParse(range[1], System.Globalization.NumberStyles.HexNumber, null, out long end)) continue;

                long size = end - start;
                if (size <= 0) continue;

                // Map name is the last field if present, empty for anonymous regions
                string name = parts.Length >= 6 ? parts[parts.Length - 1] : string.Empty;
                yield return (start, (int)size, name);
            }
        }
        
        public IntPtr FindPeBaseAddress(int pid, string processName)
        {
            try
            {
                foreach (string line in File.ReadLines($"/proc/{pid}/maps"))
                {
                    // Look for the main executable mapping — it will contain the process name
                    // and have execute permissions (r-xp)
                    if (!line.ToLower().Contains(processName.ToLower()))
                        continue;
                    if (!line.Contains("r--p") && !line.Contains("r-xp"))
                        continue;

                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    var range = parts[0].Split('-');
                    if (range.Length != 2) continue;

                    if (long.TryParse(range[0], System.Globalization.NumberStyles.HexNumber,
                            null, out long start))
                        return new IntPtr(start);
                }
            }
            catch { }

            return IntPtr.Zero;
        }
    }
}
