using System;

namespace PortAudioForUnity
{
    public class PortAudioException : Exception
    {
        public PortAudioException(string message)
            : base(message)
        {
        }

        public PortAudioException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
