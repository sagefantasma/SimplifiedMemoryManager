namespace Simplified_Memory_Manager
{
    enum PartType
    {
        Byte,
        Wildcard
    }

    class PatternPart
    {
        PartType PartType {get;set;}
        char[] Content {get;set;}
    }

}