using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Simplified_Memory_Manager
{
    public enum Operation
    {
        SkipOne,
        SkipIndefinitely,
        AnyInGroup,
        AnyNotInGroup,
        Exact
    }

    public abstract class PatternExpression
    {
        public Operation Operation {get; set;}
        public byte? Operand {get;set;}

        public PatternExpression(Operation operation, byte? operand)
        {
            this.Operation = operation;
            this.Operand = operand;
        }
    }

    public class AnyInGroup : PatternExpression
    {
        // TODO: implement logic
        private AnyInGroup(Operation operation, byte operand) : base(operation, operand){}

        /*
        public AnyInGroup(byte operand) : this(Operation.AnyInGroup, operand){}
        */
    }
    public class AnyNotInGroup : PatternExpression
    {
        //TODO: implement logic
        private AnyNotInGroup(Operation operation, byte operand) : base(operation, operand){}

        /*
        public AnyNotInGroup(byte[] operand) : this(Operation.AnyNotInGroup, operand){}
        */
    }
    public class SkipIndefinitely : PatternExpression
    {   
        //TODO: implement logic
        private SkipIndefinitely(Operation operation) : base(operation, null)
        {
        }
        /*
        public SkipIndefinitely() : this(Operation.SkipIndefinitely, null) { }
        */
    }
    public class SkipOne : PatternExpression
    {
        private SkipOne(Operation operation) : base(operation, null){}

        public SkipOne() : this(Operation.SkipOne){}
    }
    public class Exact : PatternExpression
    {
        private Exact(Operation operation, byte operand) : base(operation, operand){}

        public Exact(byte operand) : this(Operation.Exact, operand){}
    }
    

    public class SimplePattern
    {
        public List<PatternExpression> ParsedPattern {get;set;}

        private List<PatternExpression> ParsePattern(string[] patternElements)
        {
            List<PatternExpression> patternExpressions = new List<PatternExpression>();

            foreach(string patternElement in patternElements)
            {
                // TODO: improve
                //as of now, we are only supporting exact match and skip 1,
                //so this is just a simple/hacky solution to get to MVP.
                
                string sanitizedElement = patternElement.Trim();
                if(int.TryParse(sanitizedElement, out int operand))
                {
                    //Exact parse.
                    patternExpressions.Add(new Exact(byte.Parse(sanitizedElement)));
                }
                else
                {
                    //Skip one.
                    patternExpressions.Add(new SkipOne());
                }
            }

            return patternExpressions;
        }

        public SimplePattern(string patternInput) //TODO: is this the best way to input a scanning ParsedPattern?
        {
            patternInput = patternInput.TrimStart(); //sanitizing the input a bit
            string[] patternElements = patternInput.Split(' ');

            ParsedPattern = ParsePattern(patternElements);
        }
    }
}
