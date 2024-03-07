namespace Simplified_Memory_Manager.Tests;

public class SimplePatternTest
{
    public static IEnumerable<object[]> TestData =>
    new List<object[]>
    {
        new object[] { 0m, "0" },
        new object[] { 1m, "1" }
    }; //TODO: use this(modify to be proper format) and Theory
       //instead of Facts going forward to cut down on duplication

    [Fact]
    public void CreateExact()
    {
        Operation expectedOperation = Operation.Exact;
        byte expectedOperand = (byte) 12;

        Exact actual = new Exact(12);

        Assert.Equal(expectedOperation, actual.Operation);
        Assert.Equal(expectedOperand, actual.Operand);
    }

    [Fact]
    public void CreateSkipOne()
    {
        Operation expectedOperation = Operation.SkipOne;

        SkipOne actual = new SkipOne();

        Assert.Equal(expectedOperation, actual.Operation);
        Assert.Equal(null, actual.Operand);
    }

    [Fact]
    public void CreateSimplePattern()
    {
        SimplePattern simplePattern = new SimplePattern("55 12 ?? 03 ?? 2");
        List<PatternExpression> patternExpressionList = new List<PatternExpression>
        {
            new Exact(55),
            new Exact(12),
            new SkipOne(),
            new Exact(03),
            new SkipOne(),
            new Exact(02)
        };

        //Assert.NotNull(simplePattern);
        Assert.Equivalent(patternExpressionList, simplePattern.ParsedPattern, true);
    }
}