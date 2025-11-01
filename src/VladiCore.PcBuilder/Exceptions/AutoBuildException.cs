using System;

namespace VladiCore.PcBuilder.Exceptions
{
    public class AutoBuildException : Exception
    {
        public AutoBuildException(string message)
            : base(message)
        {
        }
    }
}
