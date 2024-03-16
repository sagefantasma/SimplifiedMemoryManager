namespace Simplified_Memory_Manager.Tests;

public class SimplePatternTest
{
	[Theory]
	[ClassData(typeof(InputDataTestList))]
	public void ParseStringToByte(byte? expectedOutput, string input)
	{
		byte? actualOutput = SimplePattern.SimpleParse(input);

		Assert.Equal(expectedOutput, actualOutput);
	}

	[Theory]
	[ClassData(typeof(InputDataTestList))]
	public void CreateExact(byte? expectedOutput, string input)
	{
		Exact actualResult;

		if(SimplePattern.TrySimpleParse(input, out byte parsedByte))
		{    
			actualResult = new Exact(parsedByte);

			Assert.Equal(Operation.Exact, actualResult.Operation);
			Assert.Equal(expectedOutput, actualResult.Operand);
		}
		else
		{
			Assert.Null(SimplePattern.SimpleParse(input));
		}
	}

	[Fact]
	public void CreateSkipOne()
	{
		Operation expectedOperation = Operation.SkipOne;

		SkipOne actualResult = new SkipOne();

		Assert.Equal(expectedOperation, actualResult.Operation);
		Assert.Null(actualResult.Operand);
	}

	[Theory]
	[ClassData(typeof(InputDataTestPattern))]
	public void CreateSimplePattern(string expectedOutput, string input)
	{
		SimplePattern simplePattern = new SimplePattern(input);
		
		Assert.Equivalent(expectedOutput, simplePattern.ToString(), true);
	}
}