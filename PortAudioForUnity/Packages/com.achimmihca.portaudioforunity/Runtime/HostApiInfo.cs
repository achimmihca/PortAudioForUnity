using PortAudioSharp;

namespace PortAudioForUnity
{
    public class HostApiInfo
    {
        public HostApi HostApi { get; private set; }
        public string Name { get; private set; }
        public int DeviceCount { get; private set; }
        public int DefaultInputDevice { get; private set; }
        public int DefaultOutputDevice { get; private set; }

        internal HostApiInfo(PortAudio.PaHostApiInfo paHostApiInfo)
        {
            HostApi = HostApiUtils.FromPortAudioSharp(paHostApiInfo.type);
            Name = paHostApiInfo.name;
            DeviceCount = paHostApiInfo.deviceCount;
            DefaultInputDevice = paHostApiInfo.defaultInputDevice;
            DefaultOutputDevice = paHostApiInfo.defaultOutputDevice;
        }

        public override string ToString() {
            return $"{GetType().Name}(name: {Name}, hostApi: {HostApi}, defaultInputDevice: {DefaultInputDevice}, defaultOutputDevice: {DefaultOutputDevice}, deviceCount: {DeviceCount})";
        }
    }
}
