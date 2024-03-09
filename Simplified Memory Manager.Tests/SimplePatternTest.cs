namespace Simplified_Memory_Manager.Tests;

public class SimplePatternTest
{
    public class InputDataTestList : TheoryData<byte?, string>
    {
        public InputDataTestList()
        {
            Add(0x5A, "5A");
            Add((byte) 0x12, "12");
            Add(null, "??");
            Add(0x03, "03");
            Add(null, "?");
            Add(0x02, "2");
        } //TODO: add more cases
    }

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

    [Fact]
    public void CreateSimplePattern()
    {
        SimplePattern simplePattern = new SimplePattern("55 12 ?? 03 ?? 2");
        List<PatternExpression> patternExpressionList = new List<PatternExpression>
        {
            new Exact(0x55),
            new Exact(0x12),
            new SkipOne(),
            new Exact(03),
            new SkipOne(),
            new Exact(02)
        };

        //Assert.NotNull(simplePattern);
        Assert.Equivalent(patternExpressionList, simplePattern.ParsedPattern, true);
    }
}