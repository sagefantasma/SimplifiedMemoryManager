namespace Simplified_Memory_Manager.Tests;

public class SimplePatternTest
{
    public class TestData
    {
        public byte? ExpectedOperand;
        public string ActualOperand;

        public TestData(byte? expected, string actualResult)
        {
            ExpectedOperand = expected;
            ActualOperand = actualResult;
        }
    }
    //"55 12 ?? 03 ?? 2"
    public static List<TestData> InputDataTestList =
    new List<TestData>
    {
        new TestData(0x5A, "5A"),
        new TestData((byte) 0x12, "12"),
        new TestData(null, "??"),
        new TestData(0x03, "03"),
        new TestData(null, "?"),
        new TestData(0x03, "2")
    }; //TODO: use this(add more cases) and Theory
       //instead of Facts going forward to cut down on duplication

    [Fact]
    public void CreateExact()
    {
        Operation expectedOperation = Operation.Exact;
        byte? expectedOperand = InputDataTestList[0].ExpectedOperand;

        Exact actualResult = new Exact(byte.Parse(InputDataTestList[0].ActualOperand, System.Globalization.NumberStyles.AllowHexSpecifier));

        Assert.Equal(expectedOperation, actualResult.Operation);
        Assert.Equal(expectedOperand, actualResult.Operand);
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