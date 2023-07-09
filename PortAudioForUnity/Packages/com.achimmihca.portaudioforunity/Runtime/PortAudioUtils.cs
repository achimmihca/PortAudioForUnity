using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PortAudioSharp;
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

        private static int selectedHostApiAsInt = -1;
        
        private static bool isInitialized;
        private static bool failedToInitialize;
        private static List<PortAudioSampleRecorder> sampleRecorders = new();

        private static GameObject portAudioDisposeOnDestroyGameObject;

        public static string GetDefaultInputDeviceName()
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            int hostApi = GetHostApiIndexOrThrow();
            PortAudio.PaHostApiInfo hostApiInfo = PortAudio.Pa_GetHostApiInfo(hostApi);
            PortAudio.PaDeviceInfo deviceInfo = PortAudio.Pa_GetDeviceInfo(hostApiInfo.defaultInputDevice);
            return deviceInfo.name;
        }

        public static string GetDefaultOutputDeviceName()
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            int hostApi = GetHostApiIndexOrThrow();
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

            int deviceIndex = GetGlobalInputDeviceIndexOnCurrentHostApiOrThrow(deviceName);
            PortAudio.PaDeviceInfo deviceInfo = PortAudio.Pa_GetDeviceInfo(deviceIndex);
            minSampleRate = (int)deviceInfo.defaultSampleRate;
            maxSampleRate = (int)deviceInfo.defaultSampleRate;
            maxChannelCount = deviceInfo.maxInputChannels;
        }

        public static void GetOutputDeviceCapabilities(string outputDeviceName, out int maxChannelCount)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            int deviceIndex = GetGlobalOutputDeviceIndexOnCurrentHostApiOrThrow(outputDeviceName);
            PortAudio.PaDeviceInfo deviceInfo = PortAudio.Pa_GetDeviceInfo(deviceIndex);
            maxChannelCount = deviceInfo.maxOutputChannels;
        }

        public static void StartRecording(
            string inputDeviceName,
            bool loop,
            int bufferLengthInSeconds,
            int sampleRate,
            string outputDeviceName = null,
            float outputAmplificationFactor = 1)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            PortAudio.PaHostApiTypeId hostApi = GetHostApi();
            
            int inputDeviceIndex = GetGlobalInputDeviceIndexOnCurrentHostApiOrThrow(inputDeviceName);
            int outputDeviceIndex = string.IsNullOrEmpty(outputDeviceName)
                ? -1
                : GetGlobalOutputDeviceIndexOnCurrentHostApiOrThrow(outputDeviceName);

            if (inputDeviceIndex < 0)
            {
                throw new PortAudioException($"Could not determine device index for input device name '{inputDeviceName}' on host API {hostApi}");
            }
            
            if (outputDeviceIndex < 0 && !string.IsNullOrEmpty(outputDeviceName))
            {
                throw new PortAudioException($"Could not determine device index for output device name '{outputDeviceName}' on host API {hostApi}");
            }
            
            Log($"Starting recording with global input device index: {inputDeviceIndex} and global output device index: {outputDeviceIndex} on host API {hostApi}");
            
            PortAudioSampleRecorder existingSampleRecorder = GetSampleRecorderByInputDeviceName(inputDeviceName);
            if (existingSampleRecorder != null)
            {
                if (existingSampleRecorder.InputDeviceIndex != inputDeviceIndex
                    || existingSampleRecorder.Loop != loop
                    || existingSampleRecorder.SampleBufferLengthInSeconds != bufferLengthInSeconds
                    || existingSampleRecorder.SampleRate != sampleRate
                    || Math.Abs(existingSampleRecorder.OutputAmplificationFactor - outputAmplificationFactor) > 0.001f
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
                    return;
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
                outputDeviceIndex,
                outputChannelCount,
                outputAmplificationFactor,
                sampleRate,
                SamplesPerBuffer,
                bufferLengthInSeconds,
                loop);
            sampleRecorders.Add(newSampleRecorder);

            newSampleRecorder.StartRecording();
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

        public static void SetHostApi(PortAudio.PaHostApiTypeId hostApiTypeId)
        {
            if (GetHostApiIndex((int)hostApiTypeId) < 0)
            {
                throw new PortAudioException($"Host API {hostApiTypeId} not available.");
            }
            
            selectedHostApiAsInt = (int)hostApiTypeId;
        }

        public static int GetAvailableHostApiCount()
        {
            return PortAudio.Pa_GetHostApiCount();
        }

        public static int GetDefaultHostApiIndex()
        {
            return PortAudio.Pa_GetDefaultHostApi();
        }
        
        public static List<PortAudio.PaHostApiTypeId> GetAvailableHostApis()
        {
            List<PortAudio.PaHostApiTypeId> result = new List<PortAudio.PaHostApiTypeId>();
            int apiCount = PortAudio.Pa_GetHostApiCount();
            for (int i = 0; i < apiCount; i++) {
                PortAudio.PaHostApiInfo apiInfo = PortAudio.Pa_GetHostApiInfo(i);
                result.Add(apiInfo.type);
            }
            return result;
        }

        public static Dictionary<PortAudio.PaHostApiTypeId, Dictionary<int, int>> GetHostApiDevices()
        {
            Dictionary<PortAudio.PaHostApiTypeId, Dictionary<int, int>> result = new();
            int apiCount = PortAudio.Pa_GetHostApiCount();
            for (int hostApiIndex = 0; hostApiIndex < apiCount; hostApiIndex++) 
            {
                PortAudio.PaHostApiInfo apiInfo = PortAudio.Pa_GetHostApiInfo(hostApiIndex);
                
                Dictionary<int, int> hostApiDeviceIndexToDeviceIndex = new();
                result[apiInfo.type] = hostApiDeviceIndexToDeviceIndex;
                
                for (int hostApiDeviceIndex = 0; hostApiDeviceIndex < apiInfo.deviceCount; hostApiDeviceIndex++)
                {
                    int deviceIndex = PortAudio.Pa_HostApiDeviceIndexToDeviceIndex(hostApiIndex, hostApiDeviceIndex);
                    hostApiDeviceIndexToDeviceIndex[hostApiDeviceIndex] = deviceIndex;
                }
            }

            return result;
        }

        private static PortAudio.PaHostApiTypeId GetHostApi()
        {
            int hostApiIndex = GetHostApiIndexOrThrow();
            PortAudio.PaHostApiInfo apiInfo = PortAudio.Pa_GetHostApiInfo(hostApiIndex);
            return apiInfo.type;
        }

        private static int GetHostApiIndex(int hostApi)
        {
            if (hostApi < 0)
            {
                return PortAudio.Pa_GetDefaultHostApi();
            }
            
            // Iterate through all available host APIs
            // and return the first one that matches the preferred host API.
            int apiCount = PortAudio.Pa_GetHostApiCount();
            for (int i = 0; i < apiCount; i++) {
                PortAudio.PaHostApiInfo apiInfo = PortAudio.Pa_GetHostApiInfo(i);
                if ((int)apiInfo.type == hostApi) {
                    return i;
                }
            }

            return -1;
        }

        private static int GetHostApiIndexOrThrow()
        {
            int hostApiIndex = GetHostApiIndex(selectedHostApiAsInt);
            if (hostApiIndex < 0)
            {
                throw new PortAudioException($"Host API {selectedHostApiAsInt} not found.");
            }
            return hostApiIndex;
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
            int inputDeviceIndex = GetGlobalInputDeviceIndexOnCurrentHostApiOrThrow(inputDeviceName);
            PortAudioSampleRecorder sampleRecorder = sampleRecorders.FirstOrDefault(sampleRecorder => sampleRecorder.InputDeviceIndex == inputDeviceIndex);
            return sampleRecorder;
        }

        private static int GetGlobalInputDeviceIndexOnCurrentHostApi(string deviceName)
        {
            Dictionary<PortAudio.PaHostApiTypeId, Dictionary<int,int>> hostApiToDevices = GetHostApiDevices();
            if (!hostApiToDevices.TryGetValue(GetHostApi(), out Dictionary<int, int> hostApiDevices))
            {
                return -1;
            }
            
            foreach (KeyValuePair<int,int> hostApiDevice in hostApiDevices)
            {
                int globalDeviceIndex = hostApiDevice.Value;
                PortAudio.PaDeviceInfo deviceInfo = PortAudio.Pa_GetDeviceInfo(globalDeviceIndex);
                if (deviceInfo.maxInputChannels > 0
                    && deviceInfo.name == deviceName)
                {
                    return globalDeviceIndex;
                }
            }

            return -1;
        }
        
        private static int GetGlobalOutputDeviceIndexOnCurrentHostApi(string deviceName)
        {
            Dictionary<PortAudio.PaHostApiTypeId, Dictionary<int,int>> hostApiToDevices = GetHostApiDevices();
            if (!hostApiToDevices.TryGetValue(GetHostApi(), out Dictionary<int, int> hostApiDevices))
            {
                return -1;
            }
            
            foreach (KeyValuePair<int,int> hostApiDevice in hostApiDevices)
            {
                int globalDeviceIndex = hostApiDevice.Value;
                PortAudio.PaDeviceInfo deviceInfo = PortAudio.Pa_GetDeviceInfo(globalDeviceIndex);
                if (deviceInfo.maxOutputChannels > 0
                    && deviceInfo.name == deviceName)
                {
                    return globalDeviceIndex;
                }
            }

            return -1;
        }
        
        private static int GetGlobalInputDeviceIndexOnCurrentHostApiOrThrow(string deviceName)
        {
            int deviceIndex = GetGlobalInputDeviceIndexOnCurrentHostApi(deviceName);

            if (deviceIndex < 0)
            {
                throw new ArgumentException($"No input device found with name {deviceName} on host API {GetHostApi()}");
            }

            return deviceIndex;
        }

        /**
         * Writes the recorded samples of all channels to the buffer in the form
         * [sample_channel_0, sample_channel_1, ..., sample_channel_n, sample_channel_0, ...]
         */
        public static void GetAllRecordedSamples(string inputDeviceName, float[] bufferToBeFilled)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            PortAudioSampleRecorder sampleRecorder = GetSampleRecorderByInputDeviceName(inputDeviceName);
            if (sampleRecorder != null)
            {
                sampleRecorder.GetAllRecordedSamples(bufferToBeFilled);
            }
        }

        /**
         * Writes the recorded samples of one channels to the buffer
         */
        public static void GetRecordedSamples(string inputDeviceName, int channelIndex, float[] bufferToBeFilled)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            PortAudioSampleRecorder sampleRecorder = GetSampleRecorderByInputDeviceName(inputDeviceName);
            if (sampleRecorder != null)
            {
                sampleRecorder.GetRecordedSamples(channelIndex, bufferToBeFilled);
            }
        }

        public static int GetSingleChannelRecordingPosition(string inputDeviceName)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            PortAudioSampleRecorder sampleRecorder = GetSampleRecorderByInputDeviceName(inputDeviceName);
            if (sampleRecorder != null)
            {
                return sampleRecorder.GetSingleChannelRecordingPosition();
            }

            return 0;
        }

        private static int GetGlobalOutputDeviceIndexOnCurrentHostApiOrThrow(string deviceName)
        {
            int deviceIndex = GetGlobalOutputDeviceIndexOnCurrentHostApi(deviceName);

            if (deviceIndex < 0)
            {
                throw new ArgumentException($"No output device found with name {deviceName} on host API {GetHostApi()}");
            }

            return deviceIndex;
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
