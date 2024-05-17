using System.Diagnostics;

namespace SimplifiedMemoryManager.Tests;

public class SimpleProcessProxyTest
{
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
        using (SimpleProcessProxy simpleProcessProxy = new SimpleProcessProxy(Process.GetCurrentProcess()))
        {
            byte[] data;

            //Act
            data = simpleProcessProxy.GetProcessSnapshot();

            //Assert
            Assert.NotNull(data);
        }
    }

    [Fact]
    public void TryingToReadInvalidValuesThrowsExpectedException()
    {
        //Arrange
        using (SimpleProcessProxy simpleProcessProxy = new SimpleProcessProxy(Process.GetCurrentProcess()))
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
}