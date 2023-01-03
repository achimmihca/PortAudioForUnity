using System;
using System.Runtime.InteropServices;
using PortAudioSharp;
using UnityEditor;
using UnityEngine;

namespace PortAudioForUnity
{
    internal class PortAudioSampleRecorder : IDisposable
    {
        public AudioClip AudioClip { get; private set; }

        public string InputDeviceName { get; private set; }
        public int InputDeviceIndex { get; private set; }
        public int OutputDeviceIndex  { get; private set; }
        public int InputChannelCount  { get; private set; }
        public int InputChannelIndex  { get; private set; }
        public int OutputChannelCount { get; private set; }
        public int SampleRate { get; private set; }
        public uint SamplesPerBuffer { get; private set; }
        public int SampleBufferLengthInSeconds { get; private set; }
        public bool Loop { get; private set; }

        private readonly Audio portAudioSharpAudio;
        private readonly bool playRecordedSamples;
        private readonly float[] recordedSamples;
        private readonly float[] allRecordedSamples;
        private int writeSingleChannelSampleBufferIndex;
        private int writeAllChannelSampleBufferIndex;
        private int recordedSampleCountSinceLastCallToGetPosition;

        public bool IsRecording { get; private set; }
        private bool isDisposed;

        internal PortAudioSampleRecorder(
            string inputDeviceName,
            int inputDeviceIndex,
            int inputChannelCount,
            int inputChannelIndex,
            int outputDeviceIndex,
            int outputChannelCount,
            int sampleRate,
            uint samplesPerBuffer,
            int sampleBufferLengthInSeconds,
            bool loop)
        {
            if (inputChannelCount < 0)
            {
                throw new ArgumentException($"{nameof(inputChannelCount)} cannot be negative");
            }
            if (inputChannelIndex < 0)
            {
                throw new ArgumentException($"{nameof(inputChannelIndex)} cannot be negative");
            }
            if (sampleRate < 0)
            {
                throw new ArgumentException($"{nameof(sampleRate)} cannot be negative");
            }
            if (sampleBufferLengthInSeconds < 0)
            {
                throw new ArgumentException($"{nameof(sampleBufferLengthInSeconds)} cannot be negative");
            }

            InputDeviceIndex = inputDeviceIndex;
            InputDeviceName = inputDeviceName;
            InputChannelCount = inputChannelCount;
            InputChannelIndex = inputChannelIndex;
            OutputDeviceIndex = outputDeviceIndex;
            OutputChannelCount = outputChannelCount;
            SampleRate = sampleRate;
            SamplesPerBuffer = samplesPerBuffer;
            SampleBufferLengthInSeconds = sampleBufferLengthInSeconds;
            Loop = loop;
            playRecordedSamples = outputDeviceIndex >= 0 && outputChannelCount >= 1;
            recordedSamples = new float[sampleRate * sampleBufferLengthInSeconds];
            allRecordedSamples = new float[sampleRate * sampleBufferLengthInSeconds * inputChannelCount];

            Debug.Log($"InputChannelCount: {InputChannelCount}, InputChannelIndex: {InputChannelIndex}, playRecordedSamples: {playRecordedSamples}");

            portAudioSharpAudio = new Audio(
                inputDeviceIndex,
                outputDeviceIndex,
                inputChannelCount,
                outputChannelCount,
                sampleRate,
                samplesPerBuffer,
                RecordCallback);

            AudioClip = AudioClip.Create("PortAudioSamplesRecorderAudioClip", allRecordedSamples.Length, InputChannelCount, sampleRate, false);
            AudioClip.SetData(allRecordedSamples, 0);
        }

        public int GetPosition()
        {
            int position = recordedSampleCountSinceLastCallToGetPosition;
            recordedSampleCountSinceLastCallToGetPosition = 0;
            return position;
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            Debug.Log($"Disposing sample recorder for device '{InputDeviceName}'");

            isDisposed = true;
            StopRecording();
            portAudioSharpAudio?.Dispose();
            if (AudioClip != null)
            {
                GameObject.Destroy(AudioClip);
            }
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
            int readSampleIndex = 0;
            for (int sampleIndex = 0; sampleIndex < samplesPerBufferAsInt; sampleIndex++)
            {
                for (int channelIndex = 0; channelIndex < InputChannelCount; channelIndex++)
                {
                    int offsetInArray = readSampleIndex * sizeof(float);
                    float sample = Marshal.PtrToStructure<float>(input + offsetInArray);

                    allRecordedSamples[writeAllChannelSampleBufferIndex] = sample;

                    if (channelIndex == InputChannelIndex)
                    {
                        recordedSamples[writeSingleChannelSampleBufferIndex] = sample;

                        // if (playRecordedSamples)
                        // {
                        //     // Write samples to output array. This will play the audio from the speaker.
                        //     Marshal.StructureToPtr<float>(sample, output + offsetInArray, false);
                        // }
                    }

                    writeAllChannelSampleBufferIndex++;
                    if (writeAllChannelSampleBufferIndex >= allRecordedSamples.Length)
                    {
                        writeAllChannelSampleBufferIndex = 0;
                    }

                    readSampleIndex++;
                }

                writeSingleChannelSampleBufferIndex++;
                if (writeSingleChannelSampleBufferIndex >= recordedSamples.Length)
                {
                    writeSingleChannelSampleBufferIndex = 0;
                    if (!Loop)
                    {
                        return PortAudio.PaStreamCallbackResult.paComplete;
                    }
                }

                recordedSampleCountSinceLastCallToGetPosition++;
                if (recordedSampleCountSinceLastCallToGetPosition >= recordedSamples.Length)
                {
                    recordedSampleCountSinceLastCallToGetPosition = recordedSamples.Length - 1;
                }
            }

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
            Array.Clear(recordedSamples, 0, recordedSamples.Length);
            Array.Clear(allRecordedSamples, 0, allRecordedSamples.Length);
            writeAllChannelSampleBufferIndex = 0;
            writeSingleChannelSampleBufferIndex = 0;
            recordedSampleCountSinceLastCallToGetPosition = 0;
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

        public void UpdateAudioClipDataWithRecordedSamples()
        {
            AudioClip.SetData(allRecordedSamples, 0);
        }

        public float[] GetRecordedSamples(int channelIndex)
        {
            float[] channelSamplesCopy = new float[recordedSamples.Length];
            int writeSampleIndex = 0;
            for (int readSampleIndex = channelIndex;
                readSampleIndex < allRecordedSamples.Length && writeSampleIndex < channelSamplesCopy.Length;
                readSampleIndex += InputChannelCount)
            {
                channelSamplesCopy[writeSampleIndex] = allRecordedSamples[readSampleIndex];
                writeSampleIndex++;
            }
            return channelSamplesCopy;
        }
    }
}
