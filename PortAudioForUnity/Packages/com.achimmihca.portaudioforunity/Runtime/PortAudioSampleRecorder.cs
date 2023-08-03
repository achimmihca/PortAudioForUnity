using System;
using System.Runtime.InteropServices;
using PortAudioSharp;

namespace PortAudioForUnity
{
    internal class PortAudioSampleRecorder : IDisposable
    {
        private DeviceInfo InputDeviceInfo { get; set; }
        public int GlobalInputDeviceIndex => InputDeviceInfo?.GlobalDeviceIndex ?? -1;
        public int InputChannelCount => InputDeviceInfo?.MaxInputChannels ?? 0;

        private DeviceInfo OutputDeviceInfo { get; set; }
        public int GlobalOutputDeviceIndex => OutputDeviceInfo?.GlobalDeviceIndex ?? -1;

        public float OutputAmplificationFactor { get; set; }
        public int SampleRate { get; private set; }
        public uint SamplesPerBuffer { get; private set; }
        public int SampleBufferLengthInSeconds { get; private set; }
        public bool Loop { get; private set; }

        private readonly Audio portAudioSharpAudio;
        private readonly bool playRecordedSamples;
        private readonly float[] allChannelsRecordedSamples;
        private int writeAllChannelsSampleBufferIndex;
        private int singleChannelSampleBufferLength;
        private int allChannelsSampleBufferLength;

        public bool IsRecording { get; private set; }
        private bool isDisposed;

        internal PortAudioSampleRecorder(
            DeviceInfo inputDeviceInfo,
            DeviceInfo outputDeviceInfo,
            float outputAmplificationFactor,
            int sampleRate,
            uint samplesPerBuffer,
            int sampleBufferLengthInSeconds,
            bool loop)
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

            InputDeviceInfo = inputDeviceInfo;
            OutputDeviceInfo = outputDeviceInfo;
            OutputAmplificationFactor = outputAmplificationFactor;
            SampleRate = sampleRate;
            SamplesPerBuffer = samplesPerBuffer;
            SampleBufferLengthInSeconds = sampleBufferLengthInSeconds;
            Loop = loop;
            playRecordedSamples = outputDeviceInfo != null && outputDeviceInfo.MaxOutputChannels >= 1;
            singleChannelSampleBufferLength = sampleRate * sampleBufferLengthInSeconds;
            allChannelsSampleBufferLength = singleChannelSampleBufferLength * inputDeviceInfo.MaxInputChannels;
            allChannelsRecordedSamples = new float[allChannelsSampleBufferLength];

            // Recording is always done in mono from one of the input device's channels.
            // Thus, the output is also mono (i.e., output channel count is 1).
            int outputChannelCount = outputDeviceInfo == null
                ? -1
                : 1;

            portAudioSharpAudio = new Audio(
                InputDeviceInfo?.GlobalDeviceIndex ?? -1,
                OutputDeviceInfo?.GlobalDeviceIndex ?? -1,
                InputChannelCount,
                outputChannelCount,
                sampleRate,
                samplesPerBuffer,
                RecordCallback);
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            StopRecording();
            portAudioSharpAudio?.Dispose();
        }

        private PortAudio.PaStreamCallbackResult RecordCallback(
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
                || isDisposed
                || !IsRecording)
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

        public void StartRecording()
        {
            if (isDisposed
                || IsRecording)
            {
                return;
            }

            ResetRecordedSamples();
            portAudioSharpAudio.Start();
            IsRecording = true;
        }

        private void ResetRecordedSamples()
        {
            Array.Clear(allChannelsRecordedSamples, 0, allChannelsRecordedSamples.Length);
            writeAllChannelsSampleBufferIndex = 0;
        }

        public void StopRecording()
        {
            if (isDisposed
                || !IsRecording)
            {
                return;
            }

            portAudioSharpAudio.Stop();
            IsRecording = false;
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
