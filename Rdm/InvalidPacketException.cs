using System;

namespace VergeAero
{
    public class InvalidPacketException : InvalidOperationException
    {
        public InvalidPacketException(string message)
            : base(message)
        {
        }
    }
}
