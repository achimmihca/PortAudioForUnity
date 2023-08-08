using System;
using System.Runtime.InteropServices;
using PortAudioSharp;
using UnityEngine;

namespace PortAudioForUnity
{
    internal class OutputDeviceControl : AbstractInputOutputDeviceControl
    {
        private readonly float[] sampleBuffer;

        private readonly PortAudioUtils.PcmReaderCallback pcmReaderCallback;

        internal OutputDeviceControl(
            DeviceInfo outputDeviceInfo,
            int outputChannelCount,
            int sampleRate,
            uint samplesPerBuffer,
            int sampleBufferLengthInSeconds,
            PortAudioUtils.PcmReaderCallback pcmReaderCallback)
            : base(null,
                0,
                outputDeviceInfo,
                outputChannelCount,
                sampleRate,
                samplesPerBuffer,
                sampleBufferLengthInSeconds)
        {
            if (sampleRate <= 0)
            {
                throw new ArgumentException($"{nameof(sampleRate)} cannot be negative or zero");
            }
            if (sampleBufferLengthInSeconds <= 0)
            {
                throw new ArgumentException($"{nameof(sampleBufferLengthInSeconds)} cannot be negative or zero");
            }

            this.sampleBuffer = new float[samplesPerBuffer * outputChannelCount];
            this.pcmReaderCallback = pcmReaderCallback;
        }

        public override void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            base.Dispose();
        }

        protected override PortAudio.PaStreamCallbackResult AudioStreamCallback(
            IntPtr input,
            IntPtr output,
            uint samplesPerBuffer,
            ref PortAudio.PaStreamCallbackTimeInfo timeInfo,
            PortAudio.PaStreamCallbackFlags statusFlags,
            IntPtr localUserData)
        {
            // Debug.Log($"RecordCallback - samplesPerBuffer: {samplesPerBuffer}");

            // Because this callback is called from unsafe code in a background thread,
            // this can be null when Unity has already destroyed the instance.
            if (this == null
                || IsDisposed
                || !IsAudioStreamStarted)
            {
                return PortAudio.PaStreamCallbackResult.paAbort;
            }

            int samplesPerBufferAsInt = (int)samplesPerBuffer;

            pcmReaderCallback(sampleBuffer);

            // Write samples from array to pointer.
            // The samples are written to the array in the form
            // [SAMPLE_OF_CHANNEL_0, SAMPLE_OF_CHANNEL_1, ..., SAMPLE_OF_CHANNEL_N, SAMPLE_OF_CHANNEL_0, ...]
            int outputSampleCount = 0;
            int sampleBufferIndex = 0;
            float sampleValue = 0;
            for (int sampleIndex = 0; sampleIndex < samplesPerBufferAsInt; sampleIndex++)
            {
                for (int channelIndex = 0; channelIndex < OutputChannelCount; channelIndex++)
                {
                    sampleValue = sampleBuffer[sampleBufferIndex];
                    sampleBufferIndex++;

                    int outputOffsetInArray = outputSampleCount * sizeof(float);
                    Marshal.StructureToPtr<float>(sampleValue, output + outputOffsetInArray, false);
                    outputSampleCount++;
                }
            }

            return PortAudio.PaStreamCallbackResult.paContinue;
        }

        public void Start()
        {
            if (IsDisposed
                || IsAudioStreamStarted)
            {
                return;
            }

            StartAudioStream();
        }

        public void Stop()
        {
            if (IsDisposed
                || !IsAudioStreamStarted)
            {
                return;
            }

            StopAudioStream();
        }
    }
}
