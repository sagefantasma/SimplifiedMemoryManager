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

        public bool InitiateScan()
        {
            IntPtr foundPosition = new IntPtr();
            /*List<Thread> runningThreads = new List<Thread>();
            foreach(ScanThread scanThread in ScanThreads)
            {
                ThreadStart threadStart = new ThreadStart(() => scanThread.ScanForPattern(ref foundPosition));
                ThreadPool.SetMaxThreads((int) AvailableCores, 1);
                Thread scanningThread = new Thread(() => scanThread.ScanForPattern(ref foundPosition));
                scanningThread.Start();
                runningThreads.Add(scanningThread);
            }*/
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
        }

        public void FullProcessScan(SimplePattern pattern, Process processToProxy, Func<IntPtr, long, byte[]> GetMemory)
        {
            /* First attempt, doesn't really work well at all.
            //so this is, almost immediately, creating an OutOfMemoryException. Which means we need to take even smaller arrays per thread, and just
            //repeat until we've scanned all the memory... Maybe each thread gets 100MB at a time?

            //get the process' virtual memory size, and divide it equally among the threads.
            long virtualMemorySize = processToProxy.PeakVirtualMemorySize64;

            //divide virtual memory by processes to get ideal array size
            BufferSizePerThread = 100 * Kilobyte;
            //BufferSizePerThread = (long)Math.Ceiling(virtualMemorySize / AvailableCores);
            List<byte[]> memoryArrays = new List<byte[]>();
            //long bufferPosition = 0;
            for(long virtualMemoryProgression = 0; virtualMemoryProgression < virtualMemorySize; virtualMemoryProgression += 0)
            {
                bool canScanForwards = true;
                bool canScanBackwards = true;
                for (int i = 0; ScanThreads.Count < AvailableCores; i++)
                {
                    if(!canScanBackwards && !canScanForwards)
                    {
                        return;
                    }
                    long bufferPosition = virtualMemoryProgression;
                    int realBufferSize = (int)Math.Min(BufferSizePerThread, virtualMemorySize - virtualMemoryProgression);
                    if (canScanForwards)
                    {
                        ScanThread scanThread = new ScanThread(pattern, MasterCancellationTokenSource.Token, PatternMatched, this, new IntPtr(bufferPosition));
                        //int realBufferSize = (int)Math.Min(Math.Min(BufferSizePerThread, virtualMemorySize - bufferPosition), int.MaxValue);
                        
                        if (realBufferSize <= 0)
                        {
                            throw new SimpleProcessProxyException("Uhoh.");
                        }
                        scanThread.Data = new byte[realBufferSize];
                        try
                        {
                            Array.Copy(GetMemory(new IntPtr(bufferPosition), realBufferSize), 0, scanThread.Data, 0, realBufferSize);
                        }
                        catch
                        {
                            canScanForwards = false;
                            Array.Clear(scanThread.Data, 0, scanThread.Data.Length);
                            scanThread.Data = null;
                            scanThread = null;
                        }
                        AddScanThread(scanThread);
                    }

                    if (canScanBackwards)
                    {
                        ScanThread backwardsScanThread = new ScanThread(pattern, MasterCancellationTokenSource.Token, PatternMatched, this, new IntPtr(bufferPosition));

                        //int realBufferSize = (int)Math.Min(Math.Min(BufferSizePerThread, virtualMemorySize - bufferPosition), int.MaxValue);
                        realBufferSize = (int)Math.Min(BufferSizePerThread, virtualMemorySize - virtualMemoryProgression);
                        if (realBufferSize <= 0)
                        {
                            throw new SimpleProcessProxyException("Uhoh.");
                        }
                        backwardsScanThread.Data = new byte[realBufferSize];
                        try
                        {
                            Array.Copy(GetMemory(new IntPtr(-bufferPosition), realBufferSize), 0, backwardsScanThread.Data, 0, realBufferSize);
                        }
                        catch
                        {
                            canScanBackwards = false;
                            Array.Clear(backwardsScanThread.Data, 0, backwardsScanThread.Data.Length);
                            backwardsScanThread.Data = null;
                            backwardsScanThread = null;
                            //BufferSizePerThread = (long)Math.Min(Math.Ceiling(virtualMemorySize - bufferPosition / AvailableCores), int.MaxValue);
                            //BufferSizePerThread = (long) Math.Ceiling(virtualMemorySize - bufferPosition / AvailableCores);
                            continue;
                        }
                        AddScanThread(backwardsScanThread);
                    }

                    virtualMemoryProgression += realBufferSize;
                }

                InitiateScan();

                if (MasterCancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                ScanThreads.Clear();
            
            }*/

            List<SimpleModule> moduleMemoryBlocks = new List<SimpleModule>();
            foreach(ProcessModule module in processToProxy.Modules)
            {
                moduleMemoryBlocks.Add(new SimpleModule()
                {
                    ModuleName = module.ModuleName,
                    EntryPointAddress = module.EntryPointAddress,
                    BaseAddress = module.BaseAddress,
                    Memory = new byte[module.ModuleMemorySize]
                });
            }

            foreach(SimpleModule memoryBlock in moduleMemoryBlocks)
            {
                try
                {
                    int blockSize = memoryBlock.Memory.Length;
                    ScanThread moduleScanThread = new ScanThread(pattern, MasterCancellationTokenSource.Token, PatternMatched, this);
                    moduleScanThread.Data = new byte[blockSize];
                    Array.Copy(GetMemory(memoryBlock.BaseAddress, blockSize), 0, moduleScanThread.Data, 0, blockSize);
                    AddScanThread(moduleScanThread);
                }
                catch
                {
                    continue;
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
