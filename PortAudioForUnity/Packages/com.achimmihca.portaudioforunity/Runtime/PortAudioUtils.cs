using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PortAudioSharp;
using Unity.VisualScripting;
using UnityEngine;

namespace PortAudioForUnity
{
    public static class PortAudioUtils
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void StaticInit()
        {
            isInitialized = false;
            failedToInitialize = false;
            SamplesPerBuffer = 1024;

            if (sampleRecorders != null)
            {
                sampleRecorders.ForEach(sampleRecorder => sampleRecorder.Dispose());
            }
            sampleRecorders = new();

            // Create GameObject and set "don't destroy on load" to be notified
            // when the app terminates.
            if (!Application.isEditor || Application.isPlaying)
            {
                portAudioDisposeOnDestroyGameObject = new GameObject("PortAudioDisposeOnDestroy");
                GameObject.DontDestroyOnLoad(portAudioDisposeOnDestroyGameObject);
                portAudioDisposeOnDestroyGameObject.AddComponent<PortAudioDisposeOnDestroy>();
            }
        }

        public static uint SamplesPerBuffer { get; set; } = 1024;

        private static bool isInitialized;
        private static bool failedToInitialize;
        private static List<PortAudioSampleRecorder> sampleRecorders = new();

        private static GameObject portAudioDisposeOnDestroyGameObject;

        public static string GetDefaultInputDeviceName()
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            int hostApi = GetHostApi();
            PortAudio.PaHostApiInfo hostApiInfo = PortAudio.Pa_GetHostApiInfo(hostApi);
            PortAudio.PaDeviceInfo deviceInfo = PortAudio.Pa_GetDeviceInfo(hostApiInfo.defaultInputDevice);
            return deviceInfo.name;
        }

        public static string GetDefaultOutputDeviceName()
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            int hostApi = GetHostApi();
            PortAudio.PaHostApiInfo hostApiInfo = PortAudio.Pa_GetHostApiInfo(hostApi);
            PortAudio.PaDeviceInfo deviceInfo = PortAudio.Pa_GetDeviceInfo(hostApiInfo.defaultOutputDevice);
            return deviceInfo.name;
        }

        public static string[] GetInputDeviceNames()
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            List<string> inputDeviceNames = new List<string>();
            int deviceCount = PortAudio.Pa_GetDeviceCount();
            for (int deviceIndex = 0; deviceIndex < deviceCount; deviceIndex++)
            {
                PortAudio.PaDeviceInfo deviceInfo = PortAudio.Pa_GetDeviceInfo(deviceIndex);
                if (deviceInfo.maxInputChannels > 0)
                {
                    // This is an input device (aka. a microphone).
                    inputDeviceNames.Add(deviceInfo.name);
                }
                else if (deviceInfo.maxOutputChannels > 0)
                {
                    // This is an output device (aka. a speaker)
                }
            }

            return inputDeviceNames.ToArray();
        }

        public static void GetInputDeviceCapabilities(
            string deviceName,
            out int minSampleRate,
            out int maxSampleRate,
            out int maxChannelCount)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            int deviceCount = PortAudio.Pa_GetDeviceCount();
            for (int deviceIndex = 0; deviceIndex < deviceCount; deviceIndex++)
            {
                PortAudio.PaDeviceInfo deviceInfo = PortAudio.Pa_GetDeviceInfo(deviceIndex);
                if (deviceInfo.maxInputChannels > 0
                    && deviceInfo.name == deviceName)
                {
                    minSampleRate = (int)deviceInfo.defaultSampleRate;
                    maxSampleRate = (int)deviceInfo.defaultSampleRate;
                    maxChannelCount = deviceInfo.maxInputChannels;
                    return;
                }
            }

            throw new PortAudioException($"No input device found with name {deviceName}");
        }

        public static void GetOutputDeviceCapabilities(string outputDeviceName, out int maxChannelCount)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            int deviceCount = PortAudio.Pa_GetDeviceCount();
            for (int deviceIndex = 0; deviceIndex < deviceCount; deviceIndex++)
            {
                PortAudio.PaDeviceInfo deviceInfo = PortAudio.Pa_GetDeviceInfo(deviceIndex);
                if (deviceInfo.maxOutputChannels > 0
                    && deviceInfo.name == outputDeviceName)
                {
                    maxChannelCount = deviceInfo.maxOutputChannels;
                    return;
                }
            }

            throw new PortAudioException($"No output device found with name '{outputDeviceName}'");
        }

        public static AudioClip StartRecording(
            string inputDeviceName,
            bool loop,
            int bufferLengthInSeconds,
            int sampleRate,
            int inputChannelIndex,
            string outputDeviceName = null)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            int inputDeviceIndex = GetInputDeviceIndex(inputDeviceName);
            int outputDeviceIndex = string.IsNullOrEmpty(outputDeviceName)
                ? -1
                : GetOutputDeviceIndex(outputDeviceName);

            PortAudioSampleRecorder existingSampleRecorder = GetSampleRecorderByInputDeviceName(inputDeviceName);
            if (existingSampleRecorder != null)
            {
                if (existingSampleRecorder.InputDeviceIndex != inputDeviceIndex
                    || existingSampleRecorder.Loop != loop
                    || existingSampleRecorder.SampleBufferLengthInSeconds != bufferLengthInSeconds
                    || existingSampleRecorder.SampleRate != sampleRate
                    || existingSampleRecorder.InputChannelIndex != inputChannelIndex
                    || existingSampleRecorder.OutputDeviceIndex != outputDeviceIndex)
                {
                    // Cannot reuse existing sample recorder. Dispose the old one and create a new one.
                    existingSampleRecorder.Dispose();
                    sampleRecorders.Remove(existingSampleRecorder);
                }
                else
                {
                    // Reuse existing sample recorder.
                    existingSampleRecorder.StartRecording();
                    return existingSampleRecorder.AudioClip;
                }
            }

            GetInputDeviceCapabilities(inputDeviceName, out int minSampleRate, out int maxSampleRate, out int maxInputChannelCount);

            // Recording is always done in mono from one of the input device's channels.
            // Thus, the output is also mono (i.e., output channel count is 1).
            int outputChannelCount = string.IsNullOrEmpty(outputDeviceName)
                ? -1
                : 1;

            PortAudioSampleRecorder newSampleRecorder = new PortAudioSampleRecorder(
                inputDeviceName,
                inputDeviceIndex,
                maxInputChannelCount,
                inputChannelIndex,
                outputDeviceIndex,
                outputChannelCount,
                sampleRate,
                SamplesPerBuffer,
                bufferLengthInSeconds,
                loop);
            sampleRecorders.Add(newSampleRecorder);

            newSampleRecorder.StartRecording();

            return newSampleRecorder.AudioClip;
        }

        public static void StopRecording(string deviceName)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            PortAudioSampleRecorder sampleRecorder = GetSampleRecorderByInputDeviceName(deviceName);
            if (sampleRecorder != null)
            {
                sampleRecorder.StopRecording();
            }
        }

        public static bool IsRecording(string deviceName)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            PortAudioSampleRecorder sampleRecorder = GetSampleRecorderByInputDeviceName(deviceName);
            if (sampleRecorder != null)
            {
                return sampleRecorder.IsRecording;
            }

            return false;
        }

        public static int GetSampleRecorderPosition(string deviceName)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            PortAudioSampleRecorder sampleRecorder = GetSampleRecorderByInputDeviceName(deviceName);
            if (sampleRecorder != null)
            {
                return sampleRecorder.GetPosition();
            }

            return 0;
        }

        private static int GetHostApi() {
            int selectedHostApi = PortAudio.Pa_GetDefaultHostApi();
            int apiCount = PortAudio.Pa_GetHostApiCount();
            for (int i = 0; i<apiCount; i++) {
                PortAudio.PaHostApiInfo apiInfo = PortAudio.Pa_GetHostApiInfo(i);
                if ((apiInfo.type == PortAudio.PaHostApiTypeId.paDirectSound)
                    || (apiInfo.type == PortAudio.PaHostApiTypeId.paALSA))
                {
                    selectedHostApi = i;
                }
            }
            return selectedHostApi;
        }

        public static void Dispose()
        {
            Log("Dispose");
            sampleRecorders.ForEach(sampleRecorder => sampleRecorder.Dispose());
            sampleRecorders = new();

            // Wait a short duration such that PortAudio and running recording callbacks have time to shut down properly.
            Thread.Sleep(100);
        }

        private static PortAudioSampleRecorder GetSampleRecorderByInputDeviceName(string inputDeviceName)
        {
            int inputDeviceIndex = GetInputDeviceIndex(inputDeviceName);
            PortAudioSampleRecorder sampleRecorder = sampleRecorders.FirstOrDefault(sampleRecorder => sampleRecorder.InputDeviceIndex == inputDeviceIndex);
            return sampleRecorder;
        }

        private static int GetInputDeviceIndex(string deviceName)
        {
            int deviceCount = PortAudio.Pa_GetDeviceCount();
            for (int deviceIndex = 0; deviceIndex < deviceCount; deviceIndex++)
            {
                PortAudio.PaDeviceInfo deviceInfo = PortAudio.Pa_GetDeviceInfo(deviceIndex);
                if (deviceInfo.maxInputChannels > 0
                    && deviceInfo.name == deviceName)
                {
                    return deviceIndex;
                }
            }

            throw new ArgumentException($"No input device found with name {deviceName}");
        }

        public static void UpdateAudioClipDataWithRecordedSamples(string inputDeviceName)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            PortAudioSampleRecorder sampleRecorder = GetSampleRecorderByInputDeviceName(inputDeviceName);
            if (sampleRecorder != null)
            {
                sampleRecorder.UpdateAudioClipDataWithRecordedSamples();
            }
        }

        public static float[] GetRecordedSamples(string inputDeviceName, int channelIndex)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            PortAudioSampleRecorder sampleRecorder = GetSampleRecorderByInputDeviceName(inputDeviceName);
            if (sampleRecorder != null)
            {
                return sampleRecorder.GetRecordedSamples(channelIndex);
            }

            return Array.Empty<float>();
        }

        private static int GetOutputDeviceIndex(string deviceName)
        {
            int deviceCount = PortAudio.Pa_GetDeviceCount();
            for (int deviceIndex = 0; deviceIndex < deviceCount; deviceIndex++)
            {
                PortAudio.PaDeviceInfo deviceInfo = PortAudio.Pa_GetDeviceInfo(deviceIndex);
                if (deviceInfo.maxOutputChannels > 0
                    && deviceInfo.name == deviceName)
                {
                    return deviceIndex;
                }
            }

            throw new ArgumentException($"No output device found with name {deviceName}");
        }

        private static void InitializeIfNotDoneYet()
        {
            if (failedToInitialize)
            {
                // Already failed to initialize. Do not attempt to try again.
                throw new PortAudioException("Failed to initialize PortAudio");
            }

            if (isInitialized)
            {
                // Already initialized
                return;
            }

            if (IsError("Initialize", PortAudio.Pa_Initialize()))
            {
                failedToInitialize = true;
                // if Pa_Initialize() returns an error code,
                // Pa_Terminate() should NOT be called.
                throw new PortAudioException("Failed to initialize PortAudio");
            }

            Audio.LoggingEnabled = true;

            isInitialized = true;
        }

        private static bool IsError(string actionName, PortAudio.PaError errorCode)
        {
            if (errorCode != PortAudio.PaError.paNoError)
            {
                Log($"{actionName} error: {PortAudio.Pa_GetErrorText(errorCode)}");
                if (errorCode == PortAudio.PaError.paUnanticipatedHostError)
                {
                    PortAudio.PaHostErrorInfo errorInfo = PortAudio.Pa_GetLastHostErrorInfo();
                    Log($"Host error API type: {errorInfo.hostApiType}");
                    Log($"Host error code: {errorInfo.errorCode}");
                    Log($"Host error text: {errorInfo.errorText}");
                }

                return true;
            }
            else
            {
                Log($"{actionName}: OK");
                return false;
            }
        }

        private static void Log(string message)
        {
            Debug.Log($"PortAudioForUnity - {message}");
        }

        private static void ThrowIfNotOnMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != 1)
            {
                throw new PortAudioException($"Must be called from the main thread.");
            }
        }
    }
}
