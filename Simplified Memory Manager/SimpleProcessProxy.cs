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

		private void OpenProcess()
		{
			ValidateProcessToProxy();

			OpenedProcessHandle = NativeMethods.OpenProcess(AccessPrivileges.AllAccess | AccessPrivileges.ProcessVMOperation, false, ProcessToProxy.Id);
		}

		private void ValidateProcessToProxy()
		{
			if (ProcessToProxy == null || ProcessToProxy.HasExited)
			{
				ProcessToProxy = Process.GetProcessesByName(ProcessName).FirstOrDefault();
				if (ProcessToProxy == default)
				{
					throw new SimpleProcessProxyException($"Failed to find process {ProcessName} to proxy");
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
				OpenProcess();
				NativeMethods.ReadProcessMemory(OpenedProcessHandle, ProcessBaseAddress, buffer, (uint)buffer.Length, out int numBytesRead);
              //NativeMethods.ReadProcessMemory(OpenedProcessHandle, objectAddress, bytesToRead, (uint)bytesToRead.Length, out int bytesRead);
            }
			catch (Exception e)
			{
				throw new SimpleProcessProxyException($"Failed to read process `{ProcessName}`. Is it running?", e);
			}

			return buffer;
		}

		private byte[] GetMemory(IntPtr offset, long valueSize)
		{
			try
			{
				OpenProcess();

				//IntPtr addressToRead = IntPtr.Add(ProcessBaseAddress, offset);
				long address = ProcessBaseAddress.ToInt64() + offset.ToInt64();
				IntPtr addressToRead = new IntPtr(address);

				byte[] bytesRead = new byte[valueSize];
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
                if (OpenedProcessHandle != default)
                {
                    NativeMethods.CloseHandle(OpenedProcessHandle);
                    OpenedProcessHandle = default;
                }
            }
        }

		private void SetMemory(IntPtr desiredOffset, byte[] value, bool forceWrite)
		{
			try
			{
				OpenProcess();

				//IntPtr addressToModify = IntPtr.Add(ProcessBaseAddress, desiredOffset);
				long address = ProcessBaseAddress.ToInt64() + desiredOffset.ToInt64();
				IntPtr addressToModify = new IntPtr(address);

				if (forceWrite)
				{
					ForceReadWritePermissions(addressToModify, value.Length);
				}
				WriteBytesToMemory(addressToModify, value);
			}
			catch (Exception e)
			{
				throw new SimpleProcessProxyException($"Something unexpected went wrong when trying to modify the process' memory! {e}");
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
                OpenProcess();

                WriteBytesToMemory(offset, value);
            }
            catch (Exception e)
            {
                throw new SimpleProcessProxyException($"Something unexpected went wrong when trying to modify the process' memory! {e}");
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
				throw new SimpleProcessProxyException($"Failed to force read/write permissions at {OpenedProcessHandle}+{objectAddress} with error {Marshal.GetLastWin32Error()}");
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
				if (!modificationSuccess)
				{
					throw new SimpleProcessProxyException("Failed to write to process memory.");
				}
				if (bytesWritten != bytesToWrite.Length)
				{
					throw new SimpleProcessProxyException($"We tried to write {bytesToWrite.Length} bytes, but ended up writing {bytesWritten}");
				}
				throw new SimpleProcessProxyException($"Failed to write memory at {OpenedProcessHandle}+{objectAddress} with value {bytesToWrite}.");
			}
		}

		private byte[] ReadBytesFromMemory(IntPtr objectAddress, byte[] bytesToRead)
		{
			bool success = NativeMethods.ReadProcessMemory(OpenedProcessHandle, objectAddress, bytesToRead, (long)bytesToRead.Length, out long bytesRead);

			if (!success || bytesRead != bytesToRead.Length)
			{
				if (!success)
				{
					throw new SimpleProcessProxyException("Failed to read from process memory.");
				}
				if (bytesRead != bytesToRead.Length)
				{
					throw new SimpleProcessProxyException($"Expected to read {bytesToRead.Length}, but we actually read {bytesRead}");
				}
				throw new SimpleProcessProxyException($"Failed to read value at {OpenedProcessHandle}+{objectAddress}.");
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
		/// <param name="booleanSize">If your process stores booleans with more than 1 byte, specify the byte-size here.</param>
		/// <exception cref="SimpleProcessProxyException"></exception>
		public void InvertBooleanValue(IntPtr memoryOffset, int booleanSize = 1, bool forceWritability = false)
		{
			byte[] currentValue = GetMemory(memoryOffset, booleanSize);

			byte[] valueToWrite = new byte[booleanSize];

			if (Enumerable.SequenceEqual(currentValue, BitConverter.GetBytes(true)))
			{
				BitConverter.GetBytes(false).CopyTo(valueToWrite, 0);
			}
			else
			{
				BitConverter.GetBytes(true).CopyTo(valueToWrite, 0);
			}

			try
			{
				SetMemory(memoryOffset, valueToWrite, forceWritability);
			}
			catch (Exception e)
			{
				throw new SimpleProcessProxyException($"Failed to write boolean value at {OpenedProcessHandle}+{memoryOffset}", e);
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
		/// <exception cref="SimpleProcessProxyException"></exception>
		public void ModifyProcessOffset(IntPtr memoryOffset, short offsetValueToWrite, bool forceWritability = false)
		{
			try
			{
				SetMemory(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability);
			}
			catch(Exception e)
			{
				throw new SimpleProcessProxyException($"Failed to write int value at {OpenedProcessHandle}+{memoryOffset}", e);
			}
		}

		/// <summary>
		/// Opens the proxied process and attempts to modify its memory at the designated offset with the provided value.
		/// 
		/// If the attempt to modify memory fails, an exception is thrown.
		/// </summary>
		/// <param name="memoryOffset">Where in the proxied process' memory to begin the modification.</param>
		/// <param name="offsetValueToWrite">Value to set in memory, beginning at the memoryOffset and extending for the byte-length of the value.</param>
		/// <exception cref="SimpleProcessProxyException"></exception>
		public void ModifyProcessOffset(IntPtr memoryOffset, int offsetValueToWrite, bool forceWritability = false)
		{
			try
			{
				SetMemory(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability);
			}
			catch (Exception e)
			{
				throw new SimpleProcessProxyException($"Failed to write int value at {OpenedProcessHandle}+{memoryOffset}", e);
			}
		}

		/// <summary>
		/// Opens the proxied process and attempts to modify its memory at the designated offset with the provided value.
		/// 
		/// If the attempt to modify memory fails, an exception is thrown.
		/// </summary>
		/// <param name="memoryOffset">Where in the proxied process' memory to begin the modification.</param>
		/// <param name="offsetValueToWrite">Value to set in memory, beginning at the memoryOffset and extending for the byte-length of the value.</param>
		/// <exception cref="SimpleProcessProxyException"></exception>
		public void ModifyProcessOffset(IntPtr memoryOffset, long offsetValueToWrite, bool forceWritability = false)
		{
			try
			{
				SetMemory(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability);
			}
			catch (Exception e)
			{
				throw new SimpleProcessProxyException($"Failed to write int value at {OpenedProcessHandle}+{memoryOffset}", e);
			}
		}

		/// <summary>
		/// Opens the proxied process and attempts to modify its memory at the designated offset with the provided value.
		/// 
		/// If the attempt to modify memory fails, an exception is thrown.
		/// </summary>
		/// <param name="memoryOffset">Where in the proxied process' memory to begin the modification.</param>
		/// <param name="offsetValueToWrite">Value to set in memory, beginning at the memoryOffset and extending for the byte-length of the value.</param>
		/// <exception cref="SimpleProcessProxyException"></exception>
		public void ModifyProcessOffset(IntPtr memoryOffset, double offsetValueToWrite, bool forceWritability = false)
		{
			try
			{
				SetMemory(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability);
			}
			catch (Exception e)
			{
				throw new SimpleProcessProxyException($"Failed to write int value at {OpenedProcessHandle}+{memoryOffset}", e);
			}
		}

		/// <summary>
		/// Opens the proxied process and attempts to modify its memory at the designated offset with the provided value.
		/// 
		/// If the attempt to modify memory fails, an exception is thrown.
		/// </summary>
		/// <param name="memoryOffset">Where in the proxied process' memory to begin the modification.</param>
		/// <param name="offsetValueToWrite">Value to set in memory, beginning at the memoryOffset and extending for the byte-length of the value.</param>
		/// <exception cref="SimpleProcessProxyException"></exception>
		public void ModifyProcessOffset(IntPtr memoryOffset, float offsetValueToWrite, bool forceWritability = false)
		{
			try
			{
				SetMemory(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability);
			}
			catch (Exception e)
			{
				throw new SimpleProcessProxyException($"Failed to write int value at {OpenedProcessHandle}+{memoryOffset}", e);
			}
		}

		/// <summary>
		/// Opens the proxied process and attempts to modify its memory at the designated offset with the provided value.
		/// 
		/// If the attempt to modify memory fails, an exception is thrown.
		/// </summary>
		/// <param name="memoryOffset">Where in the proxied process' memory to begin the modification.</param>
		/// <param name="offsetValueToWrite">Value to set in memory, beginning at the memoryOffset and extending for the byte-length of the value.</param>
		/// <exception cref="SimpleProcessProxyException"></exception>
		public void ModifyProcessOffset(IntPtr memoryOffset, bool offsetValueToWrite, bool forceWritability = false)
		{
			try
			{
				SetMemory(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability);
			}
			catch (Exception e)
			{
				throw new SimpleProcessProxyException($"Failed to write int value at {OpenedProcessHandle}+{memoryOffset}", e);
			}
		}

		/// <summary>
		/// Opens the proxied process and attempts to modify its memory at the designated offset with the provided value.
		/// 
		/// If the attempt to modify memory fails, an exception is thrown.
		/// </summary>
		/// <param name="memoryOffset">Where in the proxied process' memory to begin the modification.</param>
		/// <param name="offsetValueToWrite">Value to set in memory, beginning at the memoryOffset and extending for the byte-length of the value.</param>
		/// <exception cref="SimpleProcessProxyException"></exception>
		public void ModifyProcessOffset(IntPtr memoryOffset, char offsetValueToWrite, bool forceWritability = false)
		{
			try
			{
				SetMemory(memoryOffset, BitConverter.GetBytes(offsetValueToWrite), forceWritability);
			}
			catch (Exception e)
			{
				throw new SimpleProcessProxyException($"Failed to write int value at {OpenedProcessHandle}+{memoryOffset}", e);
			}
		}

		/// <summary>
		/// Opens the proxied process and attempts to modify its memory at the designated offset with the provided value.
		/// 
		/// If the attempt to modify memory fails, an exception is thrown.
		/// </summary>
		/// <param name="memoryOffset">Where in the proxied process' memory to begin the modification.</param>
		/// <param name="offsetValueToWrite">Value to set in memory, beginning at the memoryOffset and extending for the byte-length of the value.</param>
		/// <exception cref="SimpleProcessProxyException"></exception>
		public void ModifyProcessOffset(IntPtr memoryOffset, byte[] offsetValueToWrite, bool forceWritability = false)
		{
			try
			{
				SetMemory(memoryOffset, offsetValueToWrite, forceWritability);
			}
			catch(Exception e)
			{
				throw new SimpleProcessProxyException($"Failed to write int value at {OpenedProcessHandle}+{memoryOffset}", e);
			}
		}

		/// <summary>
		/// Opens the proxied process and attempts to modify its memory at the designated offset with the provided value.
		/// 
		/// If the attempt to modify memory fails, an exception is thrown.
		/// </summary>
		/// <param name="memoryOffset">Where in the proxied process' memory to begin the modification.</param>
		/// <param name="offsetValueToWrite">Value to set in memory, beginning at the memoryOffset and extending for the byte-length of the value.</param>
		/// <exception cref="SimpleProcessProxyException"></exception>
		public void ModifyProcessOffset(IntPtr memoryOffset, string offsetValueToWrite, bool forceWritability = false)
		{
			try
			{
				SetMemory(memoryOffset, Encoding.Default.GetBytes(offsetValueToWrite), forceWritability);
			}
			catch (Exception e)
			{
				throw new SimpleProcessProxyException($"Failed to write int value at {OpenedProcessHandle}+{memoryOffset}", e);
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
		/// <exception cref="SimpleProcessProxyException"></exception>
		public byte[] ReadProcessOffset(IntPtr memoryOffset, long bytesToRead)
		{
			try
			{
				return GetMemory(memoryOffset, bytesToRead);
			}
			catch(Exception e)
			{
				throw new SimpleProcessProxyException($"Failed to read value at {OpenedProcessHandle}+{memoryOffset}", e);
			}
		}

		/// <summary>
		/// Takes a snapshot of the proxied process' memory at the current moment in time.
		/// </summary>
		/// <returns>Array of bytes containing the proxied process' current memory.</returns>
		/// <exception cref="SimpleProcessProxyException"></exception>
		public byte[] GetProcessSnapshot()
		{
			try
			{                
				return GetProcessSnapshot(ProcessToProxy.PeakPagedMemorySize64);
			}
			catch(Exception e)
			{
				throw new SimpleProcessProxyException($"Failed to get full-state of process", e);
			}
		}

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

		public IntPtr FollowPointer(IntPtr pointer, bool bigEndian, int sizeOfPointer = 8)
		{
			try
			{
				OpenProcess();

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

		public byte[] GetMemoryFromPointer(IntPtr pointer, int size)
		{
			return GetMemoryOutsideMainModule(pointer, size);
		}

		public void SetMemoryAtPointer(IntPtr pointer, byte[] data)
		{
			SetMemoryOutsideMainModule(pointer, data);
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
			
			scanManager.InitiateScan();
			
			if(scanManager.ScanResult.Count == 0)
			{
				throw new SimpleProcessProxyException("Pattern not found in process memory.");
			}

			return scanManager.ScanResult;
		}
		#endregion
	}
}
