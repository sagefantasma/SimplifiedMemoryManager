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
    }
    #endregion

    [Fact]
	public void CanSpawnThread()
	{
		//Arrange
		SimplePattern simplePattern = new SimplePattern("");
        ScanManager scanManager = new ScanManager();

        //Act
        ScanThread scanThread = new ScanThread(simplePattern, _cancellationTokenSource.Token, TestPatternMatched, scanManager);
		
		//Assert
		Assert.NotNull(scanThread);
	}

    [Fact]
    public void PatternMatchedEventIsAssignedOnCreation()
    {
        //Arrange
        SimplePattern simplePattern = new SimplePattern("");
        ScanManager scanManager = new ScanManager();

        //Act
        ScanThread scanThread = new ScanThread(simplePattern, _cancellationTokenSource.Token, TestPatternMatched, scanManager);

        //Assert
        Assert.StrictEqual(scanThread.PatternMatched, TestPatternMatched);
    }
}