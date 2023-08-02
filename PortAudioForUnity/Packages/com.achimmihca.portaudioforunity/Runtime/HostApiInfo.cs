using PortAudioSharp;

namespace PortAudioForUnity
{
    public class HostApiInfo
    {
        public HostApi HostApi { get; private set; }
        public string Name { get; private set; }
        public int DeviceCount { get; private set; }
        public int DefaultInputDeviceGlobalIndex { get; private set; }
        public int DefaultOutputDeviceGlobalIndex { get; private set; }

        internal HostApiInfo(PortAudio.PaHostApiInfo paHostApiInfo)
        {
            HostApi = HostApiUtils.FromPortAudioSharp(paHostApiInfo.type);
            Name = paHostApiInfo.name;
            DeviceCount = paHostApiInfo.deviceCount;
            DefaultInputDeviceGlobalIndex = paHostApiInfo.defaultInputDevice;
            DefaultOutputDeviceGlobalIndex = paHostApiInfo.defaultOutputDevice;
        }

        public override string ToString() {
            return $"{GetType().Name}(name: {Name}, hostApi: {HostApi}, defaultInputDevice: {DefaultInputDeviceGlobalIndex}, defaultOutputDevice: {DefaultOutputDeviceGlobalIndex}, deviceCount: {DeviceCount})";
        }
    }
}
