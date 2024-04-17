namespace SimplifiedMemoryManager.Tests;

public class SimplePatternTest
{
	[Theory]
	[ClassData(typeof(InputDataTestOperatorList))]
	public void ParseStringToByte(byte? expectedOutput, string input)
	{
		byte? actualOutput = SimplePattern.SimpleParse(input);

		Assert.Equal(expectedOutput, actualOutput);
	}

	[Theory]
	[ClassData(typeof(InputDataTestOperandList))]
	public void CreateExactSetsExpectedOperation(Operation expectedOutput, string input)
	{
		Exact actualResult;

		if(SimplePattern.TrySimpleParse(input, out byte parsedByte))
		{    
			actualResult = new Exact(parsedByte);

			Assert.Equal(expectedOutput, actualResult.Operation);
		}
		else
		{
			Assert.Null(SimplePattern.SimpleParse(input));
		}
	}

	[Theory]
	[ClassData(typeof(InputDataTestOperatorList))]
	public void CreateExactSetsExpectedOperand(byte? expectedOutput, string input)
	{
        Exact actualResult;

        if (SimplePattern.TrySimpleParse(input, out byte parsedByte))
        {
            actualResult = new Exact(parsedByte);

            Assert.Equal(expectedOutput, actualResult.Operand);
        }
        else
        {
            Assert.Null(SimplePattern.SimpleParse(input));
        }
    }

    [Fact]
	public void CreateSkipOneSetsCorrectOperation()
	{
		Operation expectedOperation = Operation.SkipOne;

		SkipOne actualResult = new SkipOne();

		Assert.Equal(expectedOperation, actualResult.Operation);
	}

	[Fact]
	public void CreateSkipOneSetsCorrectOperand()
	{
		SkipOne actualResult = new SkipOne();

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