using PortAudioSharp;

namespace PortAudioForUnity
{
    internal static class HostApiUtils
    {
        internal static HostApi FromPortAudioSharp(PortAudio.PaHostApiTypeId type)
        {
            return (HostApi)((int)type);
        }
    }
}
