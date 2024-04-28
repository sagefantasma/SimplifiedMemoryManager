﻿using System.Diagnostics;

namespace SimplifiedMemoryManager.Tests;

public class SimpleProcessProxyTest : IDisposable
{
    Process _testProcess = new Process();
    public void Dispose()
    {
        try
        {
            _testProcess?.Kill();
        }
        catch { }
        GC.SuppressFinalize(this);
    }

    private SimpleProcessProxy CreateTestProxy()
    {
        _testProcess.StartInfo = new ProcessStartInfo("notepad.exe") { CreateNoWindow = true };
        _testProcess.Start();
        Thread.Sleep(50);
        return new SimpleProcessProxy(_testProcess);
    }

    [Fact]
    public void CanCreateProcessProxy()
    {
        //Arrange
        SimpleProcessProxy simpleProcessProxy;
        Process process = Process.GetCurrentProcess();

        //Act
        simpleProcessProxy = new SimpleProcessProxy(process);

        //Assert
        Assert.NotNull(simpleProcessProxy);
    }

    [Fact]
    public void ThrowsExceptionIfNullProcess()
    {
        //Arrange
        SimpleProcessProxy simpleProcessProxy;

        //Act
        void act()
        {
            simpleProcessProxy = new SimpleProcessProxy(null);
        }

        //Assert
        Assert.Throws<SimpleProcessProxyException>(act);
    }

    [Fact]
    public void CanGetProcessSnapshot()
    {
        //Arrange
        using (SimpleProcessProxy simpleProcessProxy = CreateTestProxy())
        {
            byte[] data;

            //Act
            data = simpleProcessProxy.GetProcessSnapshot();

            //Assert
            Assert.NotNull(data);
        }
    }

    [Fact]
    public void CannotGetProcessSnapshotOfInvalidProcess()
    {
        //Arrange
        using (SimpleProcessProxy simpleProcessProxy = CreateTestProxy())
        {
            byte[] data;

            //Act
            _testProcess.Kill();
            _testProcess.WaitForExit();
            void readFromDeadProcess()
            {
                data = simpleProcessProxy.GetProcessSnapshot();
            }

            //Assert
            Assert.Throws<SimpleProcessProxyException>(readFromDeadProcess);
        }
    }

    [Fact]
    public void CanReadValuesFromProcess()
    {
        //Arrange
        using (SimpleProcessProxy simpleProcessProxy = CreateTestProxy())
        {
            //Act
            byte[] result = simpleProcessProxy.ReadProcessOffset(0, 10);

            //Assert
            Assert.Equal(10, result.Length);
        }
    }

    [Fact]
    public void TryingToReadInvalidValuesThrowsExpectedException()
    {
        //Arrange
        using (SimpleProcessProxy simpleProcessProxy = CreateTestProxy())
        {
            //Act
            void impossibleRead()
            {
                byte[] result = simpleProcessProxy.ReadProcessOffset(-10, 10);
            }

            //Assert
            Assert.Throws<SimpleProcessProxyException>(impossibleRead);
        }
    }

    [Fact]
    public void CannotInvertSpecificBooleans()
    {
        //Arrange
        using (SimpleProcessProxy simpleProcessProxy = CreateTestProxy())
        {
            byte[] currentValue = simpleProcessProxy.ReadProcessOffset(0, 1000);
            int indexToInvert = currentValue.ToList().IndexOf(0);

            //Act & Assert
            Assert.Throws<SimpleProcessProxyException>(() => simpleProcessProxy.InvertBooleanValue(indexToInvert));
        }
    }

    [Fact]
    public void CanForceInvertBooleanValue()
    {
        //Arrange
        using (SimpleProcessProxy simpleProcessProxy = CreateTestProxy())
        {
            byte[] currentValue = simpleProcessProxy.ReadProcessOffset(0, 1000);
            int indexToInvert = currentValue.ToList().IndexOf(0);

            //Act
            simpleProcessProxy.InvertBooleanValue(indexToInvert, forceWritability: true);

            //Assert
            Assert.True(simpleProcessProxy.ReadProcessOffset(0, 1000)[indexToInvert] == 1);
        }
    }

    [Theory]
    [ClassData(typeof(ProxyTestDataTypes))]
    public void CanForceWriteValues(dynamic inputData)
    {
        //Arrange
        using (SimpleProcessProxy simpleProcessProxy = CreateTestProxy())
        {
            byte[] currentValue = simpleProcessProxy.ReadProcessOffset(0, 1000);
            int indexToModify = currentValue.ToList().IndexOf(0);

            //Act
            simpleProcessProxy.ModifyProcessOffset(indexToModify, inputData, forceWritability: true);

            //Assert
            Assert.NotEqual(currentValue, simpleProcessProxy.ReadProcessOffset(0,1000));
        }
    }

    [Theory]
    [ClassData(typeof(ProxyTestDataTypes))]
    public void NotForceWriteThrowsExpectedException(dynamic inputData)
    {
        //Arrange
        using (SimpleProcessProxy simpleProcessProxy = CreateTestProxy())
        {
            byte[] currentValue = simpleProcessProxy.ReadProcessOffset(0, 1000);
            int indexToModify = currentValue.ToList().IndexOf(0);            

            //Act
            void safeModification()
            {
                simpleProcessProxy.ModifyProcessOffset(indexToModify, inputData);
            }

            Assert.Throws<SimpleProcessProxyException>(safeModification);
        }
    }

    [Fact]
    public void ScanForValidAoBReturnsPositiveIndex()
    {
        //Arrange
        using(SimpleProcessProxy simpleProcessProxy = CreateTestProxy())
        {
            byte[] fakedMemory = RealMemory.LoadSampleMemory();

            //Act
            int result = simpleProcessProxy.ScanMemoryForPattern(new SimplePattern(RealMemory.ValidAoBInMemory), fakedMemory);

            //Assert
            Assert.True(result > 0);
        }
    }

    [Fact]
    public void ScanForInvalidAoBReturnsNegativeOne()
    {
        //Arrange
        using (SimpleProcessProxy simpleProcessProxy = CreateTestProxy())
        {
            byte[] fakedMemory = RealMemory.LoadSampleMemory();

            //Act
            int result = simpleProcessProxy.ScanMemoryForPattern(new SimplePattern(RealMemory.InvalidAoBInMemory), fakedMemory);

            //Assert
            Assert.Equal(-1, result);
        }
    }
}