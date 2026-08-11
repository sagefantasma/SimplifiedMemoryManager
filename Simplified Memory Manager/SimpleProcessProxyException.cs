using System;

namespace SimplifiedMemoryManager
{
    public class SimpleProcessProxyException : Exception
    {
        public SimpleProcessProxyException(string message) : base(message)
        {
        }
    }

    public class SimpleProcessProxyAggregateException : AggregateException
    {
        public SimpleProcessProxyAggregateException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
