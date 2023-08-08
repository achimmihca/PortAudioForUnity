using System;
using System.Runtime.InteropServices;
using PortAudioSharp;

namespace PortAudioForUnity
{
    internal class InputDeviceControl : AbstractInputOutputDeviceControl
    {
        public float OutputAmplificationFactor { get; set; }
        public bool Loop { get; private set; }

        private readonly bool playRecordedSamples;
        private readonly float[] allChannelsRecordedSamples;
        private int writeAllChannelsSampleBufferIndex;

        public bool IsRecording => IsAudioStreamStarted;

        internal InputDeviceControl(
            DeviceInfo inputDeviceInfo,
            DeviceInfo outputDeviceInfo,
            float outputAmplificationFactor,
            int sampleRate,
            uint samplesPerBuffer,
            int sampleBufferLengthInSeconds,
            bool loop)
            : base(inputDeviceInfo,
                inputDeviceInfo.MaxInputChannels,
                outputDeviceInfo,
                // Recording is always done in mono from one of the input device's channels.
                // Thus, the output is also mono (i.e., output channel count is 1).
                outputDeviceInfo != null ? 1 : 0,
                sampleRate,
                samplesPerBuffer,
                sampleBufferLengthInSeconds)
        {
            if (inputDeviceInfo == null)
            {
                throw new NullReferenceException(nameof(inputDeviceInfo));
            }
            if (inputDeviceInfo.MaxInputChannels <= 0)
            {
                throw new ArgumentException($"No input channels in {inputDeviceInfo} cannot be negative or zero");
            }
            if (sampleRate <= 0)
            {
                throw new ArgumentException($"{nameof(sampleRate)} cannot be negative or zero");
            }
            if (sampleBufferLengthInSeconds <= 0)
            {
                throw new ArgumentException($"{nameof(sampleBufferLengthInSeconds)} cannot be negative or zero");
            }

            OutputAmplificationFactor = outputAmplificationFactor;
            Loop = loop;
            playRecordedSamples = outputDeviceInfo != null && outputDeviceInfo.MaxOutputChannels >= 1;
            int singleChannelSampleBufferLength = sampleRate * sampleBufferLengthInSeconds;
            int allChannelsSampleBufferLength = singleChannelSampleBufferLength * inputDeviceInfo.MaxInputChannels;
            allChannelsRecordedSamples = new float[allChannelsSampleBufferLength];
        }

        public override void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            Stop();
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
            // Debug.Log($"InputDeviceControl.AudioStreamCallback - samplesPerBuffer: {samplesPerBuffer}");

            // Because this callback is called from unsafe code in a background thread,
            // this can be null when Unity has already destroyed the instance.
            if (this == null
                || IsDisposed
                || !IsAudioStreamStarted)
            {
                return PortAudio.PaStreamCallbackResult.paAbort;
            }

            int samplesPerBufferAsInt = (int)samplesPerBuffer;

            // Read samples from pointer to array.
            // The samples are written to the array in the form
            // [SAMPLE_OF_CHANNEL_0, SAMPLE_OF_CHANNEL_1, ..., SAMPLE_OF_CHANNEL_N, SAMPLE_OF_CHANNEL_0, ...]
            int writeAllChannelsSampleBufferIndexCopy = writeAllChannelsSampleBufferIndex;
            int inputSampleIndex = 0;
            int outputSampleCount = 0;
            float monoSampleValue = 0;
            for (int sampleIndex = 0; sampleIndex < samplesPerBufferAsInt; sampleIndex++)
            {
                for (int channelIndex = 0; channelIndex < InputChannelCount; channelIndex++)
                {
                    int offsetInArray = inputSampleIndex * sizeof(float);
                    float sampleValue = Marshal.PtrToStructure<float>(input + offsetInArray);

                    allChannelsRecordedSamples[writeAllChannelsSampleBufferIndexCopy] = sampleValue;

                    if (playRecordedSamples)
                    {
                        // Playing back the samples is done in mono. Thus, combine the samples to be mono.
                        monoSampleValue += sampleValue;
                        if (channelIndex == InputChannelCount - 1)
                        {
                            monoSampleValue /= InputChannelCount;

                            // Write samples to output array. This will play the audio from the speaker.
                            // Here, the output is assumed to be mono (OutputChannelCount should be 1).
                            int outputOffsetInArray = outputSampleCount * sizeof(float);
                            float amplifiedMonoSampleValue = monoSampleValue * OutputAmplificationFactor;
                            Marshal.StructureToPtr<float>(amplifiedMonoSampleValue, output + outputOffsetInArray, false);
                            outputSampleCount++;

                            monoSampleValue = 0;
                        }
                    }

                    writeAllChannelsSampleBufferIndexCopy++;
                    if (writeAllChannelsSampleBufferIndexCopy >= allChannelsRecordedSamples.Length)
                    {
                        writeAllChannelsSampleBufferIndexCopy = 0;
                        if (!Loop)
                        {
                            return PortAudio.PaStreamCallbackResult.paComplete;
                        }
                    }

                    inputSampleIndex++;
                }
            }

            writeAllChannelsSampleBufferIndex = writeAllChannelsSampleBufferIndexCopy;

            return PortAudio.PaStreamCallbackResult.paContinue;
        }

        public void Start()
        {
            if (IsDisposed
                || IsAudioStreamStarted)
            {
                return;
            }

            ResetRecordedSamples();
            StartAudioStream();
        }

        private void ResetRecordedSamples()
        {
            Array.Clear(allChannelsRecordedSamples, 0, allChannelsRecordedSamples.Length);
            writeAllChannelsSampleBufferIndex = 0;
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

        public void GetRecordedSamples(int channelIndex, float[] bufferToBeFilled)
        {
            // Start with the last written sample of the channel.
            int readAllChannelsSampleIndex = writeAllChannelsSampleBufferIndex - InputChannelCount + channelIndex;
            // Write newer samples to higher index in array, respectively older samples to lower index in array.
            for (int writeSingleChannelSampleIndex = bufferToBeFilled.Length - 1; writeSingleChannelSampleIndex >= 0; writeSingleChannelSampleIndex--)
            {
                if (readAllChannelsSampleIndex < 0)
                {
                    readAllChannelsSampleIndex += allChannelsRecordedSamples.Length;
                }

                bufferToBeFilled[writeSingleChannelSampleIndex] = allChannelsRecordedSamples[readAllChannelsSampleIndex];
                readAllChannelsSampleIndex -= InputChannelCount;
            }
        }

        public void GetAllRecordedSamples(float[] bufferToBeFilled)
        {
            // Start with the last written sample.
            int readAllChannelsSampleIndex = writeAllChannelsSampleBufferIndex - 1;
            // Write newer samples to higher index in array, respectively older samples to lower index in array.
            for (int writeAllChannelsSampleIndex = bufferToBeFilled.Length - 1; writeAllChannelsSampleIndex >= 0; writeAllChannelsSampleIndex--)
            {
                if (readAllChannelsSampleIndex < 0)
                {
                    readAllChannelsSampleIndex += allChannelsRecordedSamples.Length;
                }

                bufferToBeFilled[writeAllChannelsSampleIndex] = allChannelsRecordedSamples[readAllChannelsSampleIndex];
                readAllChannelsSampleIndex--;
            }
        }

        public int GetSingleChannelRecordingPosition()
        {
            return writeAllChannelsSampleBufferIndex / InputChannelCount;
        }
    }
}
