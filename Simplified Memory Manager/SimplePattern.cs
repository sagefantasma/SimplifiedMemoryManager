using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Simplified_Memory_Manager
{
    enum Operation
    {
        SkipOne,
        SkipIndefinitely,
        AnyInGroup,
        AnyNotInGroup,
        Exact
    }

    abstract class PatternExpression
    {
        Operation operation {get; set;}
        byte[] operand {get;set;}

        public PatternExpression(Operation operation, byte[] operand)
        {
            this.operation = operation;
            this.operand = operand;
        }
    }

    class AnyInGroup : PatternExpression
    {
        // TODO: implement logic
        private AnyInGroup(Operation operation, byte[] operand) : base(operation, operand){}

        public AnyInGroup(byte[] operand) : this(Operation.AnyInGroup, operand){}
    }
    class AnyNotInGroup : PatternExpression
    {
        //TODO: implement logic
        private AnyNotInGroup(Operation operation, byte[] operand) : base(operation, operand){}

        public AnyNotInGroup(byte[] operand) : this(Operation.AnyNotInGroup, operand){}
    }
    class SkipIndefinitely : PatternExpression
    {   
        //TODO: implement logic
        private SkipIndefinitely(Operation operation, byte[] operand) : base(operation, operand)
        {
        }
        public SkipIndefinitely() : this(Operation.SkipIndefinitely, null) { }
    }
    class SkipOne : PatternExpression
    {
        private SkipOne(Operation operation, byte[] operand) : base(operation, operand){}

        public SkipOne() : this(Operation.SkipOne, null){}
    }
    class Exact : PatternExpression
    {
        private Exact(Operation operation, byte[] operand) : base(operation, operand){}

        public Exact(byte[] operand) : this(Operation.Exact, operand){}
    }
    

    public class SimplePattern
    {
        List<PatternExpression> ParsedPattern {get;set;}

        public SimplePattern(string patternInput) //TODO: is this the best way to input a scanning ParsedPattern?
        {
            patternInput = patternInput.TrimStart(); //sanitizing the input a bit
            ParsedPattern = new List<PatternExpression>();
            //maybe it makes more sense to trim start, then create substrings based on space?
            string[] patternElements = patternInput.Split(' ');

            PatternExpression lastExpression = null;
            foreach(string patternElement in patternElements)
            {
                string sanitizedElement = patternElement.Trim();
                if(int.TryParse(sanitizedElement, out int operand))
                {
                    if(lastExpression?.GetType() != typeof(SkipOne))
                    {
                        //Exact parse.
                    }
                }
            }

            for(int i = 0; i < patternInput.Length; i += 2)
            {
                char first = patternInput[i];
                char second = patternInput[i + 1];
                switch (first)
                {
                    case '%':
                    case '*':
                        throw new NotImplementedException("Infinite skip until next declared byte not yet supported");
                        //skip infinitely until next declared byte is found
                        ParsedPattern.Add(new SkipIndefinitely());
                        break;
                    case '_':
                    case '?':
                        //skip next byte
                        ParsedPattern.Add(new SkipOne());
                        break;
                    case '[':
                        throw new NotImplementedException("Limited choice byte selection not yet supported");
                        //byte can be any within the list until ]
                        if(patternInput[i+1] == '!')
                        {
                            //! denotes the byte can be any BUT those in the list until ]
                        }
                        break;
                    case ']':
                        throw new NotImplementedException("Limited choice byte selection not yet supported");
                        //end the byte grouping
                        break;
                    default:
                        if(patternInput[i+2] != ' ')
                        {
                            throw new Exception("Invalid pattern format!");
                        }
                        if(!new [] {'%', '*', '_', '?', '[', ']', '!'}.Any(x=> x == second))
                        {
                            //good value, we can use it for comparison
                        }
                        break;
                }
            }
        }
    }
}
