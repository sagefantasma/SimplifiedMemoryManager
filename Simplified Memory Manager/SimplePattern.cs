using System.Collections.Generic;

namespace Simplified_Memory_Manager
{
    enum Operator
    {
        SkipNext,
        SkipIndefinitely,
        AnyInGroup,
        AnyNotInGroup,
        Exact
    }

    abstract class Operation
    {
        Operator @operator;
        List<char> operand;

        public Operation(Operator @operator, List<char> operand)
        {
            this.@operator = @operator;
            this.operand = operand;
        }
    }

    class SkipIndefinitely : Operation
    {

        
        private SkipIndefinitely(Operator @operator, List<char> operand) : base(@operator, operand)
        {
        }
        public SkipIndefinitely(List<char> operand) : this(Operator.SkipIndefinitely, operand) { }
    }

    public class SimplePattern
    {
        List<Operation> pattern;

        public SimplePattern(string pattern) //TODO: is this the best way to input a scanning pattern?
        {
            foreach (char c in pattern)
            {
                switch (c)
                {
                    case '%':
                    case '*':
                        //skip infinitely until next declared byte is found
                        break;
                    case '_':
                    case '?':
                        //skip next byte
                        break;
                    case '[':
                        //byte can be any within the list until ]
                        //! denotes the byte can be any BUT those in the list until ]
                        break;
                    default:

                        break;
                }
            }
        }
    }
}
