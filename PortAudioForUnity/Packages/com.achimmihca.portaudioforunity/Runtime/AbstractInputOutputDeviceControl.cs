using System;
using PortAudioSharp;

namespace PortAudioForUnity
{
    internal abstract class AbstractInputOutputDeviceControl : IDisposable
    {
        private DeviceInfo InputDeviceInfo { get; set; }
        public int GlobalInputDeviceIndex => InputDeviceInfo?.GlobalDeviceIndex ?? -1;
        public int InputChannelCount { get; private set; }

        private DeviceInfo OutputDeviceInfo { get; set; }
        public int GlobalOutputDeviceIndex => OutputDeviceInfo?.GlobalDeviceIndex ?? -1;
        public int OutputChannelCount { get; private set; }

        public int SampleRate { get; private set; }
        public uint SamplesPerBuffer { get; private set; }
        public int SampleBufferLengthInSeconds { get; private set; }

        private readonly Audio portAudioSharpAudio;
        protected bool IsAudioStreamStarted { get; private set; }

        protected bool IsDisposed { get; private set; }

        protected AbstractInputOutputDeviceControl(
            DeviceInfo inputDeviceInfo,
            int inputChannelCount,
            DeviceInfo outputDeviceInfo,
            int outputChannelCount,
            int sampleRate,
            uint samplesPerBuffer,
            int sampleBufferLengthInSeconds)
        {
            InputDeviceInfo = inputDeviceInfo;
            InputChannelCount = inputChannelCount;
            OutputDeviceInfo = outputDeviceInfo;
            OutputChannelCount = outputChannelCount;
            SampleRate = sampleRate;
            SamplesPerBuffer = samplesPerBuffer;
            SampleBufferLengthInSeconds = sampleBufferLengthInSeconds;

            portAudioSharpAudio = new Audio(
                InputDeviceInfo?.GlobalDeviceIndex ?? -1,
                OutputDeviceInfo?.GlobalDeviceIndex ?? -1,
                InputChannelCount,
                OutputChannelCount,
                SampleRate,
                SamplesPerBuffer,
                AudioStreamCallback);
        }

        public virtual void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            portAudioSharpAudio?.Dispose();
        }

        protected void StartAudioStream()
        {
            portAudioSharpAudio.Start();
            IsAudioStreamStarted = true;
        }

        protected void StopAudioStream()
        {
            portAudioSharpAudio.Stop();
            IsAudioStreamStarted = false;
        }

        protected abstract PortAudio.PaStreamCallbackResult AudioStreamCallback(
            IntPtr input,
            IntPtr output,
            uint samplesPerBuffer,
            ref PortAudio.PaStreamCallbackTimeInfo timeInfo,
            PortAudio.PaStreamCallbackFlags statusFlags,
            IntPtr localUserData);
    }
}
