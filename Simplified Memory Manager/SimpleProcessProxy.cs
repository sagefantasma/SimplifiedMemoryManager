using SimplifiedMemoryManager;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimplifiedMemoryManager
{
	public class SimpleProcessProxy : IDisposable
	{
		#region Internals
		private bool disposedValue;

		public static Process ProcessToProxy { get; set; }
		private static string ProcessName { get; set; }
		private static IntPtr ProcessBaseAddress { get; set; }
		private static IntPtr OpenedProcessHandle { get; set; }

		public SimpleProcessProxy(Process process)
		{
			ProcessToProxy = process ?? throw new SimpleProcessProxyException("You must provide a process to modify.");
			ProcessBaseAddress = process.MainModule.BaseAddress;
			ProcessName = process.ProcessName;
		}

		private void ProxyProcess()
		{
			ValidateProcessToProxy();

			OpenedProcessHandle = NativeMethods.OpenProcess(AccessPrivileges.AllAccess | AccessPrivileges.ProcessVMOperation, false, ProcessToProxy.Id);

			if(OpenedProcessHandle == null)
			{
                var winError = Marshal.GetLastWin32Error();
				throw new SimpleProcessProxyException($"Failed to open process. LastWin32Error: {winError}");
			}
		}

		private void ValidateProcessToProxy()
		{
			if (ProcessToProxy == null || ProcessToProxy.HasExited)
			{
				try
				{
					ProcessToProxy = Process.GetProcessesByName(ProcessName).FirstOrDefault();
					if (ProcessToProxy == default)
					{
						throw new SimpleProcessProxyException($"Failed to find process {ProcessName} to proxy");
					}
				}
				catch(Exception e)
				{
					throw new SimpleProcessProxyAggregateException("Something unexpected went wrong when validating the requested process proxy!", e);
				}
			}
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					// TODO: dispose managed state (managed objects)
				}

				// TODO: free unmanaged resources (unmanaged objects) and override finalizer
				ProcessName = null;
				ProcessToProxy = null; //TODO: determine if this does what I want
				// TODO: set large fields to null
				disposedValue = true;
			}
		}

		// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		// ~SimpleProcessProxy()
		// {
		//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		//     Dispose(disposing: false);
		// }

		void IDisposable.Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
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
				ProxyProcess();
				bool success = NativeMethods.ReadProcessMemory(OpenedProcessHandle, ProcessBaseAddress, buffer, (uint)buffer.Length, out int numBytesRead);
				if (!success)
					throw new SimpleProcessProxyException("Failed to read process memory");
            }
			catch (Exception e)
			{
				var winError = Marshal.GetLastWin32Error();
				throw new SimpleProcessProxyAggregateException($"Failed to get snapshot for process `{ProcessName}`. LastWin32Error: {winError}", e);
			}

			return buffer;
		}

		private byte[] GetMemoryWithinMainModule(IntPtr offset, long bytesToRead)
		{
            try
			{
                ProxyProcess();
                long address = ProcessBaseAddress.ToInt64() + offset.ToInt64();
				IntPtr addressToRead = new IntPtr(address);
				
				byte[] bytesRead = new byte[bytesToRead];
				ReadBytesFromMemory(addressToRead, bytesRead);

				return bytesRead;
			}
			catch(SimpleProcessProxyException sppe)
			{
				throw sppe;
			}
			catch(OverflowException oe)
			{
				throw new SimpleProcessProxyAggregateException($"Failed to create valid address to read", oe);
			}
			catch(Exception e)
			{
                var winError = Marshal.GetLastWin32Error();
				throw new SimpleProcessProxyAggregateException($"Something unexpected went wrong when accessing memory within main module. LastWin32Error: {winError}", e);
			}
			finally
			{
				if (OpenedProcessHandle != default)
				{
					NativeMethods.CloseHandle(OpenedProcessHandle);
					OpenedProcessHandle = default;
				}
			}
		}

		private byte[] GetMemoryOutsideMainModule(IntPtr addressToRead, long bytesToRead)
		{
            try
            {
				ProxyProcess();

                byte[] bytesRead = new byte[bytesToRead];
                ReadBytesFromMemory(addressToRead, bytesRead);

                return bytesRead;
            }
            finally
            {
                if (OpenedProcessHandle != default)
                {
                    NativeMethods.CloseHandle(OpenedProcessHandle);
                    OpenedProcessHandle = default;
                }
            }
        }

		private void SetMemoryWithinMainModule(IntPtr desiredOffset, byte[] value, bool forceWrite)
		{
			try
			{
				ProxyProcess();

				long address = ProcessBaseAddress.ToInt64() + desiredOffset.ToInt64();
				IntPtr addressToModify = new IntPtr(address);

				if (forceWrite)
				{
					ForceReadWritePermissions(addressToModify, value.Length);
				}
				WriteBytesToMemory(addressToModify, value);
			}
            catch (SimpleProcessProxyException sppe)
            {
                throw sppe;
            }
            catch (Exception e)
			{
				throw new SimpleProcessProxyAggregateException($"Something unexpected went wrong when trying to modify the process' memory!", e);
			}
			finally
			{
				if (OpenedProcessHandle != default)
				{
					NativeMethods.CloseHandle(OpenedProcessHandle);
					OpenedProcessHandle = default;
				}
			}
		}

        private void SetMemoryOutsideMainModule(IntPtr offset, byte[] value)
		{
            try
            {
                ProxyProcess();

                WriteBytesToMemory(offset, value);
            }
            catch (SimpleProcessProxyException sppe)
            {
                throw sppe;
            }
            catch (Exception e)
            {
                throw new SimpleProcessProxyAggregateException($"Something unexpected went wrong when trying to modify memory outside main module!", e);
            }
            finally
            {
                if (OpenedProcessHandle != default)
                {
                    NativeMethods.CloseHandle(OpenedProcessHandle);
                    OpenedProcessHandle = default;
                }
            }
        }

        private static void EnableDisablePrivilege(string PrivilegeName, bool EnableDisable)
        {
            if (!NativeMethods.LookupPrivilegeValue(null, PrivilegeName, out var luid)) 
				throw new Exception($"EnableDisablePrivilege: LookupPrivilegeValue failed: {Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()).Message}");

            if (!NativeMethods.OpenProcessToken(Process.GetCurrentProcess().SafeHandle, TokenAccessLevels.AdjustPrivileges, out var tokenHandle)) 
				throw new Exception($"EnableDisablePrivilege: OpenProcessToken failed: {Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()).Message}");

            var tokenPrivileges = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Privileges = new[] { new LUID_AND_ATTRIBUTES { LUID = luid, Attributes = (uint)(EnableDisable ? 2 : 4) } } };
            if (!NativeMethods.AdjustTokenPrivileges(tokenHandle, false, ref tokenPrivileges, 0, IntPtr.Zero, out _))
            {
                tokenHandle.Dispose();
                throw new Exception($"EnableDisablePrivilege: AdjustTokenPrivileges failed: {Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()).Message}");
            }
            else tokenHandle.Dispose();
        }

        private void ForceReadWritePermissions(IntPtr objectAddress, int byteCount)
		{
			bool modificationSuccess;

			lock (ProcessToProxy) 
			{
				modificationSuccess = NativeMethods.VirtualProtectEx(OpenedProcessHandle, objectAddress, byteCount, AccessPrivileges.ExecuteReadWrite, out _);
			}

			if (!modificationSuccess)
				throw new SimpleProcessProxyException($"Failed to force read/write permissions at {objectAddress} with error code: {Marshal.GetLastWin32Error()}");
		}

        private void ForceReadWritePermissionsAdmin(IntPtr objectAddress, long byteCount)
        {
			bool modificationSuccess;

			Process.EnterDebugMode();
			EnableDisablePrivilege("SeDebugPrivilege", true);

            lock (ProcessToProxy)
			{
				modificationSuccess = NativeMethods.VirtualProtectEx(OpenedProcessHandle, objectAddress, byteCount, AccessPrivileges.ExecuteReadWrite, out _);
			}

			if (!modificationSuccess)
			{
				var errorCode = Marshal.GetLastWin32Error();
				//throw new SimpleProcessProxyException($"Failed to force read/write permissions at {OpenedProcessHandle}+{objectAddress}.");
				return;
			}
        }

        private void WriteBytesToMemory(IntPtr objectAddress, byte[] bytesToWrite)
		{
			bool modificationSuccess = NativeMethods.WriteProcessMemory(OpenedProcessHandle, objectAddress, bytesToWrite, (uint)bytesToWrite.Length, out int bytesWritten);

            if (!modificationSuccess || bytesWritten != bytesToWrite.Length)
			{
                var winError = Marshal.GetLastWin32Error();
                if (!modificationSuccess)
				{
					throw new SimpleProcessProxyException($"Failed to write to process memory. Last Win32Error code: {winError}");
				}
				if (bytesWritten != bytesToWrite.Length)
				{
					throw new SimpleProcessProxyException($"We tried to write {bytesToWrite.Length} bytes, but ended up writing {bytesWritten}. Last Win32Error code: {winError}");
				}
				throw new SimpleProcessProxyException($"Failed to write memory at {objectAddress} with value {bytesToWrite}. Last Win32Error code: {winError}");
			}
		}

		private byte[] ReadBytesFromMemory(IntPtr objectAddress, byte[] bytesToRead)
		{
			bool success = NativeMethods.ReadProcessMemory(OpenedProcessHandle, objectAddress, bytesToRead, (long)bytesToRead.Length, out long bytesRead);

			if (!success || bytesRead != bytesToRead.Length)
			{
                var winError = Marshal.GetLastWin32Error();
                if (!success)
				{
					throw new SimpleProcessProxyException($"Failed to read from process memory. Last Win32Error code: {winError}");
				}
				if (bytesRead != bytesToRead.Length)
				{
					throw new SimpleProcessProxyException($"Expected to read {bytesToRead.Length}, but we actually read {bytesRead}. Last Win32Error code: {winError}");
				}
				throw new SimpleProcessProxyException($"Failed to read value at {objectAddress}. Last Win32Error code: {winError}");
			}

			return bytesToRead;
		}

		
		#endregion

		#region Public methods
		/// <summary>
		/// Opens the proxied process, gets the current value of the designated offset, and attempts to invert its state.
		/// 
		/// If the attempt to invert the boolean fails, an exception is thrown.
		/// </summary>
		/// <param name="memoryOffset">The offset, from index 0 of the proxied process' memory, that holds the boolean you want to invert.</param>
		/// <param name="booleanSize">Specify the byte-size of the boolean, if greater than 1.</param>
		/// <param name="forceWritability">Attempt to force-write change.</param>
		/// <exception cref="SimpleProcessProxyException">An operation the SPP was responsible for failed.</exception>
		/// <exception cref="SimpleProcessProxyAggregateException">An operation outside of the SPP's responsibility failed.</exception>
		public void InvertBooleanValue(IntPtr memoryOffset, int booleanSize = 1, bool forceWritability = false)
		{
			byte[] valueToWrite = new byte[booleanSize];

            try
			{
				byte[] currentValue = GetMemoryWithinMainModule(memoryOffset, booleanSize);

				if (Enumerable.SequenceEqual(currentValue, BitConverter.GetBytes(true)))
				{
					BitConverter.GetBytes(false).CopyTo(valueToWrite, 0);
				}
				else
				{
					BitConverter.GetBytes(true).CopyTo(valueToWrite, 0);
				}
			}
			catch(Exception e)
			{
				throw new SimpleProcessProxyAggregateException($"Failed to retrieve boolean value at {ProcessBaseAddress}+{memoryOffset}", e);
			}

			try
			{
				SetMemoryWithinMainModule(memoryOffset, valueToWrite, forceWritability);
			}
			catch (Exception e)
			{
				throw new SimpleProcessProxyAggregateException($"Failed to write boolean value at {ProcessBaseAddress}+{memoryOffset}", e);
			}
		}

        #region ModifyProcessOffset methods
        /// <summary>
        /// Opens the proxied process and attempts to modify its memory at the designated offset with the provided value.
        /// 
        /// If the attempt to modify memory fails, an exception is thrown.
        /// </summary>
        /// <param name="memoryOffset">Where in the proxied process' memory to begin the modification.</param>
        /// <param name="offsetValueToWrite">Value to set in memory, beginning at the memoryOffset and extending for the byte-length of the value.</param>
        /// <param name="forceWritability">Attempt to force-write change.</param>
        /// <exception cref="SimpleProcessProxyException">An operation the SPP was responsible for failed.</exception>
        /// <exception cref="SimpleProcessProxyAggregateException">An operation outside of the SPP's responsibility failed.</exception>
        public void ModifyProcessOffset(IntPtr memoryOffset, short offsetValueToWrite, bool forceWritability = false)
		{
			try
			{
				SetMemoryWithinMainModule(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability);
			}
			catch(Exception e)
			{
				throw new SimpleProcessProxyAggregateException($"Failed to write short at {ProcessBaseAddress}+{memoryOffset}", e);
			}
		}

        /// <summary>
        /// Opens the proxied process and attempts to modify its memory at the designated offset with the provided value.
        /// 
        /// If the attempt to modify memory fails, an exception is thrown.
        /// </summary>
        /// <param name="memoryOffset">Where in the proxied process' memory to begin the modification.</param>
        /// <param name="offsetValueToWrite">Value to set in memory, beginning at the memoryOffset and extending for the byte-length of the value.</param>
        /// <param name="forceWritability">Attempt to force-write change.</param>
        /// <exception cref="SimpleProcessProxyException">An operation the SPP was responsible for failed.</exception>
        /// <exception cref="SimpleProcessProxyAggregateException">An operation outside of the SPP's responsibility failed.</exception>
        public void ModifyProcessOffset(IntPtr memoryOffset, int offsetValueToWrite, bool forceWritability = false)
		{
			try
			{
				SetMemoryWithinMainModule(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability);
			}
			catch (Exception e)
			{
				throw new SimpleProcessProxyAggregateException($"Failed to write int at {ProcessBaseAddress}+{memoryOffset}", e);
			}
		}

        /// <summary>
        /// Opens the proxied process and attempts to modify its memory at the designated offset with the provided value.
        /// 
        /// If the attempt to modify memory fails, an exception is thrown.
        /// </summary>
        /// <param name="memoryOffset">Where in the proxied process' memory to begin the modification.</param>
        /// <param name="offsetValueToWrite">Value to set in memory, beginning at the memoryOffset and extending for the byte-length of the value.</param>
        /// <param name="forceWritability">Attempt to force-write change.</param>
        /// <exception cref="SimpleProcessProxyException">An operation the SPP was responsible for failed.</exception>
        /// <exception cref="SimpleProcessProxyAggregateException">An operation outside of the SPP's responsibility failed.</exception>
        public void ModifyProcessOffset(IntPtr memoryOffset, long offsetValueToWrite, bool forceWritability = false)
		{
			try
			{
				SetMemoryWithinMainModule(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability);
			}
			catch (Exception e)
			{
				throw new SimpleProcessProxyAggregateException($"Failed to write long at {ProcessBaseAddress}+{memoryOffset}", e);
			}
		}

        /// <summary>
        /// Opens the proxied process and attempts to modify its memory at the designated offset with the provided value.
        /// 
        /// If the attempt to modify memory fails, an exception is thrown.
        /// </summary>
        /// <param name="memoryOffset">Where in the proxied process' memory to begin the modification.</param>
        /// <param name="offsetValueToWrite">Value to set in memory, beginning at the memoryOffset and extending for the byte-length of the value.</param>
        /// <param name="forceWritability">Attempt to force-write change.</param>
        /// <exception cref="SimpleProcessProxyException">An operation the SPP was responsible for failed.</exception>
        /// <exception cref="SimpleProcessProxyAggregateException">An operation outside of the SPP's responsibility failed.</exception>
        public void ModifyProcessOffset(IntPtr memoryOffset, double offsetValueToWrite, bool forceWritability = false)
		{
			try
			{
				SetMemoryWithinMainModule(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability);
			}
			catch (Exception e)
			{
				throw new SimpleProcessProxyAggregateException($"Failed to write double at {ProcessBaseAddress}+{memoryOffset}", e);
			}
		}

        /// <summary>
        /// Opens the proxied process and attempts to modify its memory at the designated offset with the provided value.
        /// 
        /// If the attempt to modify memory fails, an exception is thrown.
        /// </summary>
        /// <param name="memoryOffset">Where in the proxied process' memory to begin the modification.</param>
        /// <param name="offsetValueToWrite">Value to set in memory, beginning at the memoryOffset and extending for the byte-length of the value.</param>
        /// <param name="forceWritability">Attempt to force-write change.</param>
        /// <exception cref="SimpleProcessProxyException">An operation the SPP was responsible for failed.</exception>
        /// <exception cref="SimpleProcessProxyAggregateException">An operation outside of the SPP's responsibility failed.</exception>
        public void ModifyProcessOffset(IntPtr memoryOffset, float offsetValueToWrite, bool forceWritability = false)
		{
			try
			{
				SetMemoryWithinMainModule(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability);
			}
			catch (Exception e)
			{
				throw new SimpleProcessProxyAggregateException($"Failed to write float at {ProcessBaseAddress}+{memoryOffset}", e);
			}
		}

        /// <summary>
        /// Opens the proxied process and attempts to modify its memory at the designated offset with the provided value.
        /// 
        /// If the attempt to modify memory fails, an exception is thrown.
        /// </summary>
        /// <param name="memoryOffset">Where in the proxied process' memory to begin the modification.</param>
        /// <param name="offsetValueToWrite">Value to set in memory, beginning at the memoryOffset and extending for the byte-length of the value.</param>
        /// <param name="forceWritability">Attempt to force-write change.</param>
        /// <exception cref="SimpleProcessProxyException">An operation the SPP was responsible for failed.</exception>
        /// <exception cref="SimpleProcessProxyAggregateException">An operation outside of the SPP's responsibility failed.</exception>
        public void ModifyProcessOffset(IntPtr memoryOffset, bool offsetValueToWrite, bool forceWritability = false)
		{
			try
			{
				SetMemoryWithinMainModule(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability);
			}
			catch (Exception e)
			{
				throw new SimpleProcessProxyAggregateException($"Failed to write bool at {ProcessBaseAddress}+{memoryOffset}", e);
			}
		}

        /// <summary>
        /// Opens the proxied process and attempts to modify its memory at the designated offset with the provided value.
        /// 
        /// If the attempt to modify memory fails, an exception is thrown.
        /// </summary>
        /// <param name="memoryOffset">Where in the proxied process' memory to begin the modification.</param>
        /// <param name="offsetValueToWrite">Value to set in memory, beginning at the memoryOffset and extending for the byte-length of the value.</param>
        /// <param name="forceWritability">Attempt to force-write change.</param>
        /// <exception cref="SimpleProcessProxyException">An operation the SPP was responsible for failed.</exception>
        /// <exception cref="SimpleProcessProxyAggregateException">An operation outside of the SPP's responsibility failed.</exception>
        public void ModifyProcessOffset(IntPtr memoryOffset, char offsetValueToWrite, bool forceWritability = false)
		{
			try
			{
				SetMemoryWithinMainModule(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability);
			}
			catch (Exception e)
			{
				throw new SimpleProcessProxyAggregateException($"Failed to write char at {ProcessBaseAddress}+{memoryOffset}", e);
			}
		}

        /// <summary>
        /// Opens the proxied process and attempts to modify its memory at the designated offset with the provided value.
        /// 
        /// If the attempt to modify memory fails, an exception is thrown.
        /// </summary>
        /// <param name="memoryOffset">Where in the proxied process' memory to begin the modification.</param>
        /// <param name="offsetValueToWrite">Value to set in memory, beginning at the memoryOffset and extending for the byte-length of the value.</param>
        /// <param name="forceWritability">Attempt to force-write change.</param>
        /// <exception cref="SimpleProcessProxyException">An operation the SPP was responsible for failed.</exception>
        /// <exception cref="SimpleProcessProxyAggregateException">An operation outside of the SPP's responsibility failed.</exception>
        public void ModifyProcessOffset(IntPtr memoryOffset, byte[] offsetValueToWrite, bool forceWritability = false)
		{
			try
			{
				SetMemoryWithinMainModule(memoryOffset, offsetValueToWrite, forceWritability);
			}
			catch(Exception e)
			{
				throw new SimpleProcessProxyAggregateException($"Failed to write byte array at {ProcessBaseAddress}+{memoryOffset}", e);
			}
		}

        /// <summary>
        /// Opens the proxied process and attempts to modify its memory at the designated offset with the provided value.
        /// 
        /// If the attempt to modify memory fails, an exception is thrown.
        /// </summary>
        /// <param name="memoryOffset">Where in the proxied process' memory to begin the modification.</param>
        /// <param name="offsetValueToWrite">Value to set in memory, beginning at the memoryOffset and extending for the byte-length of the value.</param>
        /// <param name="forceWritability">Attempt to force-write change.</param>
        /// <exception cref="SimpleProcessProxyException">An operation the SPP was responsible for failed.</exception>
        /// <exception cref="SimpleProcessProxyAggregateException">An operation outside of the SPP's responsibility failed.</exception>
        public void ModifyProcessOffset(IntPtr memoryOffset, string offsetValueToWrite, bool forceWritability = false)
		{
			try
			{
				SetMemoryWithinMainModule(memoryOffset, Encoding.Default.GetBytes(offsetValueToWrite), forceWritability);
			}
			catch (Exception e)
			{
				throw new SimpleProcessProxyAggregateException($"Failed to write string at {ProcessBaseAddress}+{memoryOffset}", e);
			}
		}
        #endregion

        /// <summary>
        /// Reads the proxied process' memory at the given offset for the supplied quantity of bytes.
        /// 
        /// If the attempt to read memory fails, an exception is thrown.
        /// </summary>
        /// <param name="memoryOffset">Where in the proxied process' memory to begin reading from.</param>
        /// <param name="bytesToRead">Amount of bytes to read in and return.</param>
        /// <returns>The bytes found at the provided offset within the proxied process.</returns>
        /// <exception cref="SimpleProcessProxyException">An operation the SPP was responsible for failed.</exception>
        /// <exception cref="SimpleProcessProxyAggregateException">An operation outside of the SPP's responsibility failed.</exception>
        public byte[] ReadProcessOffset(IntPtr memoryOffset, long bytesToRead)
		{
			try
			{
				return GetMemoryWithinMainModule(memoryOffset, bytesToRead);
			}
			catch(Exception e)
			{
				throw new SimpleProcessProxyAggregateException($"Failed to read value at {ProcessBaseAddress}+{memoryOffset}", e);
			}
		}

        /// <summary>
        /// Takes a snapshot of the proxied process' memory at the current moment in time.
        /// </summary>
        /// <returns>Array of bytes containing the proxied process' current memory.</returns>
        /// <exception cref="SimpleProcessProxyException">An operation the SPP was responsible for failed.</exception>
        /// <exception cref="SimpleProcessProxyAggregateException">An operation outside of the SPP's responsibility failed.</exception>
        public byte[] GetProcessSnapshot()
		{
			try
			{                
				return GetProcessSnapshot(ProcessToProxy.MainModule.ModuleMemorySize);
			}
			catch(Exception e)
			{
				throw new SimpleProcessProxyAggregateException($"Failed to get process snapshot", e);
			}
		}

        /// <summary>
        /// Takes a snapshot of the proxied process' memory at the current moment in time.
        /// </summary>
        /// <returns>Array of bytes containing the proxied process' current memory.</returns>
        /// <exception cref="SimpleProcessProxyException">An operation the SPP was responsible for failed.</exception>
        /// <exception cref="SimpleProcessProxyAggregateException">An operation outside of the SPP's responsibility failed.</exception>
        public byte[] GetFullProcessMemorySnapshot()
        {
			//TODO: implement
			throw new NotImplementedException();
            try
            {
                return GetProcessSnapshot(ProcessToProxy.MainModule.ModuleMemorySize);
            }
            catch (Exception e)
            {
                throw new SimpleProcessProxyAggregateException($"Failed to get full-state of process", e);
            }
        }

		/// <summary>
		/// Scans either all of the process' related memory, or a specific array of memory if provided, for a
		/// specified pattern of memory.
		/// </summary>
		/// <param name="pattern">A user-friendly representation of memory to scan for.</param>
		/// <param name="memoryToScan">Optional array of bytes that represents a block of memory to scan.</param>
		/// <returns>An IntPtr representing the exact location of the first instance of the pattern in memory.</returns>
		/// <exception cref="SimpleProcessProxyException">Thrown if the provided pattern was not found in memory.</exception>
        public IntPtr ScanMemoryForUniquePattern(SimplePattern pattern, byte[] memoryToScan = null)
        {
            List<IntPtr> results = new List<IntPtr>();

            ScanManager scanManager = new ScanManager();
            
			if (OpenedProcessHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(OpenedProcessHandle);
                OpenedProcessHandle = default;
            }

			lock (ProcessToProxy)
			{
				if (memoryToScan != null)
				{
					scanManager.ByteArrayScan(memoryToScan, pattern);
				}
				else
				{
					scanManager.FullProcessScan(pattern, ProcessToProxy, GetMemoryOutsideMainModule);
				}
			}

            if (scanManager.ScanResult.Count == 0)
            {
                throw new SimpleProcessProxyException("Pattern not found in process memory.");
            }

            return scanManager.ScanResult.First();
        }

		/// <summary>
		/// Interprets the provided IntPtr as a pointer(size specified by sizeOfPointer, defaulting to 8 bytes),
		/// and returns an IntPtr that represents the location of memory pointed to(NOT the value pointed to).
		/// </summary>
		/// <param name="pointer">Offset from ProcessBaseAddress where the desired pointer begins.</param>
		/// <param name="bigEndian">Endianness of the process being proxied.</param>
		/// <param name="sizeOfPointer">Size of the pointer being provided, defaulting to 8 bytes.</param>
		/// <returns>The memory address being pointed to by the provided offset pointer.</returns>
		public IntPtr FollowPointer(IntPtr pointer, bool bigEndian, int sizeOfPointer = 8)
		{
			try
			{
				ProxyProcess();

				byte[] memoryPointedTo = new byte[sizeOfPointer];
				ReadBytesFromMemory(new IntPtr(ProcessBaseAddress.ToInt64() + pointer.ToInt64()), memoryPointedTo);

				if (bigEndian)
				{
					memoryPointedTo = memoryPointedTo.Reverse().ToArray();
				}
				
				if (!Environment.Is64BitOperatingSystem)
				{
					return new IntPtr(BitConverter.ToInt32(memoryPointedTo, 0));
				}
				else
				{
					if(sizeOfPointer < 8)
					{
						//realistically, if it isn't 8, its 4. but who knows.
						List<byte> listPadder = new List<byte>();
                        listPadder.AddRange(memoryPointedTo);
                        for (int i = 0; i < sizeOfPointer; i++)
						{
							listPadder.Add(0);
						}
						memoryPointedTo = listPadder.ToArray();
					}
					return new IntPtr(BitConverter.ToInt64(memoryPointedTo, 0));
				}
			}
            finally
            {
                if (OpenedProcessHandle != default)
                {
                    NativeMethods.CloseHandle(OpenedProcessHandle);
                    OpenedProcessHandle = default;
                }
            }
        }

		/// <summary>
		/// Gets the content of the addressed memory, if the process being proxied has permission to access it.
		/// </summary>
		/// <param name="memoryLocation">Memory address you wish to read.</param>
		/// <param name="size">Amount of bytes you want to read from provided memory address.</param>
		/// <returns>Memory contents stored at the provided location and size.</returns>
		public byte[] GetMemoryFromAbsoluteLocation(IntPtr memoryLocation, int size)
		{
			return GetMemoryOutsideMainModule(memoryLocation, size);
		}

		/// <summary>
		/// Sets the content of the addressed memory, if the process being proxied has permission to access it.
		/// </summary>
		/// <param name="memoryLocation">Memory address you wish to modify.</param>
		/// <param name="data">byte-representation of the data you wish to set at the provided address.</param>
		public void SetMemoryAtPointer(IntPtr memoryLocation, byte[] data)
		{
			SetMemoryOutsideMainModule(memoryLocation, data);
		}

        /// <summary>
        /// Kicks off a series of tasks(one for each logical processor available on your machine)
        /// to begin asynchronously scanning memory for a hexadecimal pattern(also known as an
        /// array of bytes).
        /// </summary>
        /// <param name="pattern">The SimplePattern representation of an AoB to scan for</param>
        /// <param name="memoryToScan">Optional - provide this to scan this specific array of memory.
        /// If this is not provided, this method will automatically scan the memory
        /// of the process associated with the SimpleProcessProxy.</param>
        /// <param name="quantityToFind">Optional - provide an integer to provide a specific quantity
        /// of matches in memory. By default, this is set to -1 to catch all pattern matches.</param>
        /// <returns>The index of the starting position of the provided pattern, or -1 if not found.</returns>
        public List<IntPtr> ScanMemoryForPattern(SimplePattern pattern, byte[] memoryToScan = null, int quantityToFind = -1)
		{
			if(quantityToFind == 0)
				throw new SimpleProcessProxyException("Invalid quantity to find. Provide a positive integer.");
			if (quantityToFind == 1)
				throw new SimpleProcessProxyException("Invalid quantity to find. Use ScanMemoryForUniquePattern to find only one result.");
			List<IntPtr> results = new List<IntPtr>();

			ScanManager scanManager = new ScanManager(quantityToFind);

			if (memoryToScan != null)
			{
				scanManager.ByteArrayScan(memoryToScan, pattern);
			}
            else
            {
				scanManager.FullProcessScan(pattern, ProcessToProxy, GetMemoryOutsideMainModule);
			}
			
			if(scanManager.ScanResult.Count == 0)
			{
				throw new SimpleProcessProxyException("Pattern not found in process memory.");
			}

			return scanManager.ScanResult;
		}
		#endregion
	}
}
