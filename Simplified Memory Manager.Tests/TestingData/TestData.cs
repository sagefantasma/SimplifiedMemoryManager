using System.Data;

namespace SimplifiedMemoryManager.Tests;

public static class RealMemory
{
    public readonly static string ValidAoBInMemory = "6D 61 70 2E 63";
	public readonly static int ValidAoBLocation = 7443691;
    public readonly static string InvalidAoBInMemory = "6D 61 50 2E 63";

    public static byte[] LoadSampleMemory()
    {
        using (FileStream fileStream = File.Open("TestingData/sampleMemory.mem", FileMode.Open))
        {
            using (BinaryReader reader = new BinaryReader(fileStream, Encoding.UTF8, false))
            {
                return reader.ReadBytes((int)fileStream.Length);
            }
        }
    }
}


public class InputDataTestOperatorList : TheoryData<byte?, string>
{
	public InputDataTestOperatorList()
	{
		Add(0x5A, "5A");
		Add((byte) 0x12, "12");
		Add(null, "??");
		Add(0x03, "03");
		Add(null, "?");
		Add(0x02, "2");
	} //TODO: add more cases
}

public class InputDataTestOperandList : TheoryData<Operation, string>
{
    public InputDataTestOperandList()
    {
        Add(Operation.Exact, "5A");
        Add(Operation.Exact, "12");
        Add(Operation.SkipOne, "??");
        Add(Operation.Exact, "03");
        Add(Operation.SkipOne, "?");
        Add(Operation.Exact, "2");
    } //TODO: add more cases
}

public class InputDataTestPattern : TheoryData<string, string>
{
	public InputDataTestPattern()
	{
		Add("5A 12 ?? 03 ?? 02", "5A 12 ? 03 ?? 2");
	} //TODO: add more cases
}

public class ScanThreadTestData : TheoryData<int, string>
{
	public ScanThreadTestData()
	{
		//(indexOfDesiredPattern, desiredPatternToFind)
		Add(RealMemory.ValidAoBLocation, RealMemory.ValidAoBInMemory);
	} //TODO: add more cases
}

public class ProxyTestDataTypes : TheoryData<object>
{
	public ProxyTestDataTypes()
	{
		Add((short)5);
		Add((int)4);
		Add((long)67);
		Add((double)40);
		Add((float)21);
		Add(true);
		Add('a');
		Add((byte)0x1D);
		Add("banana");
	}
}