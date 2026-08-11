using System.Diagnostics;

namespace SimplifiedMemoryManager.Tests;

public class SimpleProcessProxyIntegrationTest : IDisposable
{
	//TODO: This entire class is busted on Linux :(
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
			Assert.Throws<SimpleProcessProxyAggregateException>(readFromDeadProcess);
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
    public void CannotInvertSpecificBooleans()
    {
        //Arrange
        using (SimpleProcessProxy simpleProcessProxy = CreateTestProxy())
        {
            byte[] currentValue = simpleProcessProxy.ReadProcessOffset(0, 1000);
            int indexToInvert = currentValue.ToList().IndexOf(0);

            //Act & Assert
            Assert.Throws<SimpleProcessProxyAggregateException>(() => simpleProcessProxy.InvertBooleanValue(indexToInvert));
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
            nint indexToModify = currentValue.ToList().IndexOf(0);

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
            nint indexToModify = currentValue.ToList().IndexOf(0);            

            //Act
            void safeModification()
            {
                simpleProcessProxy.ModifyProcessOffset(indexToModify, inputData);
            }

            Assert.Throws<SimpleProcessProxyAggregateException>(safeModification);
        }
    }

    [Fact]
    public void ScanForValidAoBReturnsPositiveIndex()
    {
        //NOTE: for some reason, this test always fails when run with all the other tests.
        //      idk why. but if you run it by itself, it passes.

        //Arrange
        using(SimpleProcessProxy simpleProcessProxy = CreateTestProxy())
        {
            byte[] fakedMemory = RealMemory.LoadSampleMemory();

            //Act
            List<SimpleProcessProxy.SimpleMemory> result =
	            simpleProcessProxy.ScanMemoryForPatternAsync(new SimplePattern(RealMemory.ValidAoBInMemory),
		            fakedMemory).Result;

            //Assert
            Assert.True(result.Count > 0);
        }
    }

    [Fact]
    public void ScanForInvalidAoBThrowsError()
    {
        //Arrange
        using (SimpleProcessProxy simpleProcessProxy = CreateTestProxy())
        {
            byte[] fakedMemory = RealMemory.LoadSampleMemory();

            //Act
            void badAoBScan()
            {
                List<SimpleProcessProxy.SimpleMemory> result = simpleProcessProxy.ScanMemoryForPatternAsync(new SimplePattern(RealMemory.InvalidAoBInMemory), fakedMemory).Result;
            }

            //Assert
            Assert.Throws<AggregateException>(badAoBScan);
        }
    }

    [Fact]
    public void CanScanForUniquePattern()
    {
        //Arrange
        using (SimpleProcessProxy proxy = CreateTestProxy())
        {
            byte[] fakedMemory = RealMemory.LoadSampleMemory();

            //Act
            SimpleProcessProxy.SimpleMemory result = proxy.ScanMemoryForUniquePatternAsync(new SimplePattern(RealMemory.ValidAoBInMemory), fakedMemory).Result;

            //Assert
            Assert.True(result.Offset > 0);
        }
    }
}