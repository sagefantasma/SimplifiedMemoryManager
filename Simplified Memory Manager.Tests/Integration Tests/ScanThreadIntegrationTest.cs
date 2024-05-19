using SimplifiedMemoryManager.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplified_Memory_Manager.Tests.Integration_Tests
{
    public class ScanThreadIntegrationTest : IDisposable
    {
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private static void TestPatternMatched(object? sender, ScanThread.MatchFoundEventArgs args)
        {
            if (sender is not null)
            {
                ((ScanThread)sender).ThreadRequestedCancellation = true;
            }
        }

        private static ScanThread ConstructFullScanThread(ScanManager manager, bool needValidAoB = false, string? customAoB = null)
        {
            byte[] sampleMemory = RealMemory.LoadSampleMemory();
            SimplePattern simplePattern;
            if (customAoB is not null)
                simplePattern = new SimplePattern(customAoB);
            else
            {
                if (needValidAoB)
                {
                    simplePattern = new SimplePattern(RealMemory.ValidAoBInMemory);
                }
                else
                {
                    simplePattern = new SimplePattern(RealMemory.InvalidAoBInMemory);
                }
            }
            ScanThread scanThread = new ScanThread(simplePattern, _cancellationTokenSource.Token, TestPatternMatched, manager);
            scanThread.Data = sampleMemory;

            return scanThread;
        }

        [Theory]
        [ClassData(typeof(ScanThreadTestData))]
        public void CanScanForPattern(int expectedResult, string inputString)
        {
            //Arrange
            ScanManager scanManager = new ScanManager();
            ScanThread scanThread = ConstructFullScanThread(scanManager, customAoB: inputString);
            nint result = 0;

            //Act
            scanThread.ScanForPattern(ref result);

            //Assert
            Assert.Equal(result, expectedResult);
        }

        [Fact]
        public void PatternMatchedEventIsCalledOnSuccess()
        {
            //Arrange
            ScanManager manager = new ScanManager();
            ScanThread scanThread = ConstructFullScanThread(manager, true);
            nint result = 0;

            //Act
            scanThread.ScanForPattern(ref result);

            //Assert
            Assert.True(scanThread.ThreadRequestedCancellation);
        }

        [Fact]
        public void PatternMatchedEventIsNotCalledOnFailure()
        {
            //Arrange
            ScanManager manager = new ScanManager();
            ScanThread scanThread = ConstructFullScanThread(manager, false);
            nint result = 0;

            //Act
            scanThread.ScanForPattern(ref result);

            //Assert
            Assert.False(scanThread.ThreadRequestedCancellation);
        }

        [Fact]
        public void PatternMatchedEventArgsAreNotZeroOnSuccess()
        {
            //Arrange
            ScanManager manager = new ScanManager();
            ScanThread scanThread = ConstructFullScanThread(manager, true);
            nint result = 0;

            //Act
            scanThread.ScanForPattern(ref result);

            //Assert
            Assert.NotEqual(0, result);
        }

        [Fact]
        public void PatternMatchedEventArgsAreZeroOnFailure()
        {
            //Arrange
            ScanManager manager = new ScanManager();
            ScanThread scanThread = ConstructFullScanThread(manager, false);
            nint result = 0;

            //Act
            scanThread.ScanForPattern(ref result);

            //Assert
            Assert.Equal(0, result);
        }
    }
}
