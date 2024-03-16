namespace Simplified_Memory_Manager.Tests;

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

public class InputDataTestPattern : TheoryData<string, string>
{
	public InputDataTestPattern()
	{
		Add("5A 12 ?? 03 ?? 02", "5A 12 ? 03 ?? 2");
	}
}