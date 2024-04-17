namespace SimplifiedMemoryManager.Tests;

public class ScanThreadTest : IDisposable
{
    #region Internals
    public void Dispose()
    {
        _matchedIndex = 0;
        GC.SuppressFinalize(this);
    }

    private static int _matchedIndex = 0;
    private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    private static void TestPatternMatched(object? sender, ScanThread.MatchFoundEventArgs args)
    {
        if (sender is not null)
        {
            ((ScanThread)sender).ThreadRequestedCancellation = true;
        }
        _matchedIndex = args.Index;
    }

    private static ScanThread ConstructFullScanThread(out int result, bool needValidAoB = false, string? customAoB = null)
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
        ScanThread scanThread = new ScanThread(simplePattern, _cancellationTokenSource.Token, TestPatternMatched);
        scanThread.Data = sampleMemory;
        result = 0;

        return scanThread;
    }
    #endregion

    [Fact]
	public void CanSpawnThread()
	{
		//Arrange
		SimplePattern simplePattern = new SimplePattern("");

        //Act
        ScanThread scanThread = new ScanThread(simplePattern, _cancellationTokenSource.Token, TestPatternMatched);
		
		//Assert
		Assert.NotNull(scanThread);
	}

    [Fact]
    public void PatternMatchedEventIsAssignedOnCreation()
    {
        //Arrange
        SimplePattern simplePattern = new SimplePattern("");

        //Act
        ScanThread scanThread = new ScanThread(simplePattern, _cancellationTokenSource.Token, TestPatternMatched);

        //Assert
        Assert.StrictEqual(scanThread.PatternMatched, TestPatternMatched);
    }

    [Theory]
	[ClassData(typeof(ScanThreadTestData))]
	public void CanScanForPattern(int expectedResult, string inputString)
	{
        //Arrange
        ScanThread scanThread = ConstructFullScanThread(out int result, customAoB: inputString);
		
		//Act
		scanThread.ScanForPattern(ref result);
		
		//Assert
		Assert.Equal(result, expectedResult);
	}

    [Fact]
    public void PatternMatchedEventIsCalledOnSuccess()
    {
        //Arrange
        ScanThread scanThread = ConstructFullScanThread(out int result, true);

        //Act
        scanThread.ScanForPattern(ref result);

        //Assert
        Assert.True(scanThread.ThreadRequestedCancellation);
    }

    [Fact]
    public void PatternMatchedEventIsNotCalledOnFailure()
    {
        //Arrange
        ScanThread scanThread = ConstructFullScanThread(out int result, false);

        //Act
        scanThread.ScanForPattern(ref result);

        //Assert
        Assert.False(scanThread.ThreadRequestedCancellation);
    }

    [Fact]
    public void PatternMatchedEventArgsAreNotZeroOnSuccess()
    {
        //Arrange
        ScanThread scanThread = ConstructFullScanThread(out int result, true);

        //Act
        scanThread.ScanForPattern(ref result);

        //Assert
        Assert.NotEqual(0, _matchedIndex);
    }

    [Fact]
    public void PatternMatchedEventArgsAreZeroOnFailure()
    {
        //Arrange
        ScanThread scanThread = ConstructFullScanThread(out int result, false);

        //Act
        scanThread.ScanForPattern(ref result);

        //Assert
        Assert.Equal(0, _matchedIndex);
    }
}