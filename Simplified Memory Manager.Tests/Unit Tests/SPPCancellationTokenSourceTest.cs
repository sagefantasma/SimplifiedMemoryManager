namespace SimplifiedMemoryManager.Tests;

public class SPPCancellationTokenSourceTest : IDisposable
{
    #region Internals
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
    #endregion

    [Fact]
    public void CanCreateMasterToken()
    {
        //Arrange
        SPPCancellationTokenSource tokenSource;

        //Act
        tokenSource = new SPPCancellationTokenSource();

        //Assert
        Assert.NotNull(tokenSource);
    }

    [Fact]
    public void CanCreateMasterTokenWithCancellationQuantity()
    {
        //Arrange
        SPPCancellationTokenSource tokenSource;
        int cancellationsRequired = 2;

        //Act
        tokenSource = new SPPCancellationTokenSource(cancellationsRequired);

        //Assert
        Assert.NotNull(tokenSource);
    }

    [Fact]
    public void CancellationQuantityIsAssigned()
    {
        //Arrange
        SPPCancellationTokenSource tokenSource;
        int cancellationsRequired = 2;

        //Act
        tokenSource = new SPPCancellationTokenSource(cancellationsRequired);

        //Assert
        Assert.Equal(cancellationsRequired, tokenSource.CancellationThreshold);
    }

    [Fact]
    public void MasterTokenIsNotCancelledWithoutAppropriateCancellationRequests()
    {
        //Arrange
        int cancellationsRequired = 2;
        SPPCancellationTokenSource tokenSource = new SPPCancellationTokenSource(cancellationsRequired);

        //Act
        tokenSource.Cancel();

        //Assert
        Assert.False(tokenSource.IsCancellationRequested);
    }

    [Fact]
    public void MasterTokenIsCancelledWithAppropriateCancellationRequests()
    {
        //Arrange
        SPPCancellationTokenSource tokenSource = new SPPCancellationTokenSource();

        //Act
        tokenSource.Cancel();

        //Assert
        Assert.True(tokenSource.IsCancellationRequested);
    }

    //since this method simply extends CancellationToken, I am not going to implement
    //any tests that test the basic behavior of the CancellationToken
}