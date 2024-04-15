namespace SimplifiedMemoryManager.Tests;

public class ScanThreadTest
{	
	private byte[] LoadSampleMemory()
	{
		using(FileStream fileStream = File.Open("sampleMemory.mem", FileMode.Open))
		{
			using(BinaryReader reader = new BinaryReader(fileStream, Encoding.UTF8, false))
			{
				return reader.ReadBytes((int)fileStream.Length);
			}
		}
	}
	
	[Fact]
	public void CanSpawnThread()
	{
		//Arrange
		ScanThread scanThread;
		CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		SimplePattern simplePattern = new SimplePattern("5A 12 03 02 7E");
		
		//Act
		scanThread = new ScanThread(simplePattern, cancellationTokenSource.Token, SimplifiedMemoryManager.SimpleProcessProxy.PatternMatched);
		
		//Assert
		Assert.NotNull(scanThread);
	}
	
	[Theory]
	[ClassData(typeof(ScanThreadTestData))]
	public void CanScanForPattern(int expectedResult, string inputString)
	{
		//Arrange
		byte[] sampleMemory = LoadSampleMemory();
		CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		SimplePattern simplePattern = new SimplePattern("");
		ScanThread scanThread = new ScanThread(simplePattern, cancellationTokenSource.Token, SimplifiedMemoryManager.SimpleProcessProxy.PatternMatched);
		int result = 0;
		
		//Act
		scanThread.ScanForPattern(ref result);
		
		//Assert
		Assert.Equal(result, expectedResult);
	}
}