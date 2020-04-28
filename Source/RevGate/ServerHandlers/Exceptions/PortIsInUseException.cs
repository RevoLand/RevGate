using System;

namespace RevGate.ServerHandlers.Exceptions
{
    [Serializable()]
    internal class PortIsInUseException : Exception
    {
        public PortIsInUseException()
        {
        }

        public PortIsInUseException(string message) : base(message)
        {
        }

        public PortIsInUseException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}