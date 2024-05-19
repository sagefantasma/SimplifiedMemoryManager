using SimplifiedMemoryManager;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimplifiedMemoryManager
{
    public class ScanManager
    {
        private class SimpleModule
        {
            public string ModuleName { get; set; }
            public IntPtr EntryPointAddress { get; set; }
            public IntPtr BaseAddress { get; set; }
            public byte[] Memory { get; set; }
        }
        private List<ScanThread> ScanThreads { get; set; }
        public List<IntPtr> ScanResult { get; set; }
        private double AvailableCores { get; set; }
        private long BufferSizePerThread { get; set; }
        private static SPPCancellationTokenSource MasterCancellationTokenSource { get; set; } = new SPPCancellationTokenSource();
        private const int Kilobyte = 1000;
        private const int Megabyte = 1000 * Kilobyte;
        private const int Gigabyte = 1000 * Megabyte;

        public ScanManager(int quantityToFind = 1)
        {
            ScanThreads = new List<ScanThread>();
            ScanResult = new List<IntPtr>();
            AvailableCores = Environment.ProcessorCount;
            MasterCancellationTokenSource = new SPPCancellationTokenSource(quantityToFind);
        }

        private void AddScanThread(ScanThread scanThread)
        {
            ScanThreads.Add(scanThread);
        }

        private static void PatternMatched(object sender, ScanThread.MatchFoundEventArgs index)
        {
            MasterCancellationTokenSource.Cancel();
        }

        private bool InitiateScan()
        {
            IntPtr foundPosition = new IntPtr();

            List<Task> runningThreads = new List<Task>();
            foreach (ScanThread scanThread in ScanThreads)
            {
                Task scanningTask = Task.Factory.StartNew(() => scanThread.ScanForPattern(ref foundPosition));
                runningThreads.Add(scanningTask);
            }

            WaitForScanResult(runningThreads);

            return true;
        }

        public void ByteArrayScan(byte[] memoryToScan, SimplePattern pattern)
        {
            int bufferPosition = 0;
            //this is theoretically fine, since memoryToScan is limited to be 2GB total. modern computers don't have such little memory.
            BufferSizePerThread = (long) Math.Ceiling(memoryToScan.Length / AvailableCores);

            for (int i = 0; i < AvailableCores; i++)
            {
                ScanThread scanThread = new ScanThread(pattern, MasterCancellationTokenSource.Token, PatternMatched, this);

                int realBufferSize = Math.Min((int)BufferSizePerThread, memoryToScan.Length - bufferPosition);
                scanThread.Data = new byte[realBufferSize];
                Array.Copy(memoryToScan, bufferPosition, scanThread.Data, 0, realBufferSize);
                AddScanThread(scanThread);

                bufferPosition += realBufferSize;
            }

            InitiateScan();
        }

        internal void FullProcessScan(SimplePattern pattern, Process processToProxy, Func<IntPtr, long, byte[]> GetMemory)
        {
            foreach(ProcessModule module in processToProxy.Modules)
            {
                ScanThread moduleScanThread = new ScanThread(pattern, MasterCancellationTokenSource.Token, PatternMatched, this);
                try
                {
                    moduleScanThread.Data = GetMemory(module.BaseAddress, module.ModuleMemorySize);
                    AddScanThread(moduleScanThread);
                }
                catch(Exception ex)
                {
                    try
                    {
                        moduleScanThread.Data = GetMemory(IntPtr.Add(module.BaseAddress, processToProxy.MainModule.BaseAddress.ToInt32()), module.ModuleMemorySize);
                        AddScanThread(moduleScanThread);
                    }
                    catch
                    {

                    }
                }
            }

            InitiateScan();
        }

        private static void WaitForScanResult(List<Task> runningThreads)
        {
            /*while (runningThreads.Any(thread => thread.IsAlive == true))
            {

            }*/
            while (runningThreads.Any(thread => thread.Status == TaskStatus.Running))
            {

            }
        }
    }
}
