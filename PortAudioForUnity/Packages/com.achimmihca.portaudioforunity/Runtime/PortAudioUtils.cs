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

            globalDeviceIndexToSampleRecorder
                .Values
                .ToList()
                .ForEach(sampleRecorder => sampleRecorder.Dispose());
            globalDeviceIndexToSampleRecorder.Clear();

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
        private static readonly Dictionary<int, PortAudioSampleRecorder> globalDeviceIndexToSampleRecorder = new();

        private static GameObject portAudioDisposeOnDestroyGameObject;

        public static List<HostApi> HostApis => HostApiInfos
            .Select(hostApiInfo => hostApiInfo.HostApi)
            .ToList();

        private static List<HostApiInfo> hostApiInfos;
        public static List<HostApiInfo> HostApiInfos
        {
            get
            {
                if (hostApiInfos == null)
                {
                    hostApiInfos = GetHostApiInfos();
                }

                return hostApiInfos;
            }
        }

        private static List<DeviceInfo> deviceInfos;
        public static List<DeviceInfo> DeviceInfos
        {
            get
            {
                if (deviceInfos == null)
                {
                    deviceInfos = GetDeviceInfos();
                }

                return deviceInfos;
            }
        }

        private static int defaultHostApiAsInt = -1;
        private static HostApi DefaultHostApi
        {
            get
            {
                if (defaultHostApiAsInt < 0)
                {
                    ThrowIfNotOnMainThread();
                    InitializeIfNotDoneYet();
                    if (HostApiInfos == null
                        || HostApiInfos.Count <= 0)
                    {
                        throw new PortAudioException("No host API available");
                    }

                    int defaultHostApiIndex = PortAudio.Pa_GetDefaultHostApi();
                    if (defaultHostApiIndex >= HostApiInfos.Count)
                    {
                        throw new PortAudioException("Default host API not available");
                    }

                    HostApiInfo hostApiInfo = HostApiInfos[defaultHostApiIndex];
                    defaultHostApiAsInt = (int)hostApiInfo.HostApi;
                }

                return (HostApi)defaultHostApiAsInt;
            }
        }

        public static HostApiInfo DefaultHostApiInfo => GetHostApiInfo(DefaultHostApi);
        public static DeviceInfo DefaultInputDeviceInfo => GetDeviceInfo(DefaultHostApiInfo.DefaultInputDeviceGlobalIndex);
        public static DeviceInfo DefaultOutputDeviceInfo => GetDeviceInfo(DefaultHostApiInfo.DefaultOutputDeviceGlobalIndex);

        public static void StartRecording(
            DeviceInfo inputDeviceInfo,
            bool loop,
            int bufferLengthInSeconds,
            int sampleRate,
            DeviceInfo outputDeviceInfo = null,
            float outputAmplificationFactor = 1)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            if (inputDeviceInfo == null)
            {
                throw new NullReferenceException(nameof(inputDeviceInfo));
            }

            Log($"Starting recording with input device: {inputDeviceInfo} and output device: {outputDeviceInfo}");

            int globalOutputDeviceIndex = outputDeviceInfo?.GlobalDeviceIndex ?? -1;
            if (globalDeviceIndexToSampleRecorder.TryGetValue(inputDeviceInfo.GlobalDeviceIndex, out PortAudioSampleRecorder existingSampleRecorder)
                && existingSampleRecorder != null)
            {
                if (existingSampleRecorder.Loop != loop
                    || existingSampleRecorder.SampleBufferLengthInSeconds != bufferLengthInSeconds
                    || existingSampleRecorder.SampleRate != sampleRate
                    || Math.Abs(existingSampleRecorder.OutputAmplificationFactor - outputAmplificationFactor) > 0.001f
                    || existingSampleRecorder.GlobalOutputDeviceIndex != globalOutputDeviceIndex)
                {
                    // Cannot reuse existing sample recorder. Dispose the old one and create a new one.
                    existingSampleRecorder.Dispose();
                    globalDeviceIndexToSampleRecorder.Remove(inputDeviceInfo.GlobalDeviceIndex);
                }
                else
                {
                    // Reuse existing sample recorder.
                    existingSampleRecorder.StartRecording();
                    return;
                }
            }

            PortAudioSampleRecorder newSampleRecorder = new PortAudioSampleRecorder(
                inputDeviceInfo,
                outputDeviceInfo,
                outputAmplificationFactor,
                sampleRate,
                SamplesPerBuffer,
                bufferLengthInSeconds,
                loop);
            globalDeviceIndexToSampleRecorder[inputDeviceInfo.GlobalDeviceIndex] = newSampleRecorder;

            newSampleRecorder.StartRecording();
        }

        public static void SetOutputAmplificationFactor(DeviceInfo deviceInfo, float outputAmplificationFactor)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            if (globalDeviceIndexToSampleRecorder.TryGetValue(deviceInfo.GlobalDeviceIndex, out PortAudioSampleRecorder sampleRecorder)
                && sampleRecorder != null)
            {
                sampleRecorder.OutputAmplificationFactor = outputAmplificationFactor;
            }
        }

        public static void StopRecording(DeviceInfo deviceInfo)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            if (globalDeviceIndexToSampleRecorder.TryGetValue(deviceInfo.GlobalDeviceIndex, out PortAudioSampleRecorder sampleRecorder)
                && sampleRecorder != null)
            {
                sampleRecorder.StopRecording();
            }
        }

        public static bool IsRecording(DeviceInfo deviceInfo)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            if (globalDeviceIndexToSampleRecorder.TryGetValue(deviceInfo.GlobalDeviceIndex, out PortAudioSampleRecorder sampleRecorder)
                && sampleRecorder != null)
            {
                return sampleRecorder.IsRecording;
            }

            return false;
        }

        private static List<HostApiInfo> GetHostApiInfos()
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            List<HostApiInfo> result = new();
            int apiCount = PortAudio.Pa_GetHostApiCount();
            for (int i = 0; i < apiCount; i++) {
                PortAudio.PaHostApiInfo paHostApiInfo = PortAudio.Pa_GetHostApiInfo(i);
                HostApiInfo hostApiInfo = new(paHostApiInfo);
                result.Add(hostApiInfo);
            }
            return result;
        }

        public static HostApiInfo GetHostApiInfo(HostApi hostApi)
        {
            return hostApiInfos.FirstOrDefault(hostApiInfo => hostApiInfo.HostApi == hostApi);
        }

        private static List<DeviceInfo> GetDeviceInfos()
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            List<DeviceInfo> result = new();
            int apiCount = PortAudio.Pa_GetHostApiCount();
            for (int hostApiIndex = 0; hostApiIndex < apiCount; hostApiIndex++) 
            {
                PortAudio.PaHostApiInfo paHostApiInfo = PortAudio.Pa_GetHostApiInfo(hostApiIndex);

                for (int hostApiDeviceIndex = 0; hostApiDeviceIndex < paHostApiInfo.deviceCount; hostApiDeviceIndex++)
                {
                    int globalDeviceIndex = PortAudio.Pa_HostApiDeviceIndexToDeviceIndex(hostApiIndex, hostApiDeviceIndex);
                    PortAudio.PaDeviceInfo paDeviceInfo = PortAudio.Pa_GetDeviceInfo(globalDeviceIndex);

                    DeviceInfo deviceInfo = new(paHostApiInfo, paDeviceInfo, hostApiDeviceIndex, globalDeviceIndex);
                    result.Add(deviceInfo);
                }
            }

            return result;
        }

        public static DeviceInfo GetDeviceInfo(int globalDeviceIndex)
        {
            return DeviceInfos.FirstOrDefault(deviceInfo => deviceInfo.GlobalDeviceIndex == globalDeviceIndex);
        }

        public static DeviceInfo GetDeviceInfo(HostApi hostApi, int hostApiDeviceIndex)
        {
            return DeviceInfos.FirstOrDefault(deviceInfo => deviceInfo.HostApi == hostApi
                                                            && deviceInfo.HostApiDeviceIndex == hostApiDeviceIndex);
        }

        public static void Dispose()
        {
            Log("PortAudioUtils.Dispose");
            globalDeviceIndexToSampleRecorder
                .Values
                .ToList()
                .ForEach(sampleRecorder => sampleRecorder.Dispose());
            globalDeviceIndexToSampleRecorder.Clear();

            PortAudio.Pa_Terminate();

            // Wait a short duration such that PortAudio and running recording callbacks have time to shut down properly.
            Thread.Sleep(100);
        }

        /**
         * Writes the recorded samples of all channels to the buffer in the form
         * [sample_channel_0, sample_channel_1, ..., sample_channel_n, sample_channel_0, ...]
         */
        public static void GetAllRecordedSamples(DeviceInfo deviceInfo, float[] bufferToBeFilled)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            if (globalDeviceIndexToSampleRecorder.TryGetValue(deviceInfo.GlobalDeviceIndex, out PortAudioSampleRecorder sampleRecorder)
                && sampleRecorder != null)
            {
                sampleRecorder.GetAllRecordedSamples(bufferToBeFilled);
            }
        }

        /**
         * Writes the recorded samples of one channel to the buffer
         */
        public static void GetRecordedSamples(DeviceInfo deviceInfo, int channelIndex, float[] bufferToBeFilled)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            if (globalDeviceIndexToSampleRecorder.TryGetValue(deviceInfo.GlobalDeviceIndex, out PortAudioSampleRecorder sampleRecorder)
                && sampleRecorder != null)
            {
                sampleRecorder.GetRecordedSamples(channelIndex, bufferToBeFilled);
            }
        }

        public static int GetSingleChannelRecordingPosition(DeviceInfo deviceInfo)
        {
            ThrowIfNotOnMainThread();
            InitializeIfNotDoneYet();

            if (globalDeviceIndexToSampleRecorder.TryGetValue(deviceInfo.GlobalDeviceIndex, out PortAudioSampleRecorder sampleRecorder)
                && sampleRecorder != null)
            {
                return sampleRecorder.GetSingleChannelRecordingPosition();
            }

            return 0;
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

            if (CheckAndLogError("Initialize", PortAudio.Pa_Initialize()))
            {
                failedToInitialize = true;
                // if Pa_Initialize() returns an error code,
                // Pa_Terminate() should NOT be called.
                throw new PortAudioException("Failed to initialize PortAudio");
            }

            Audio.LoggingEnabled = true;

            isInitialized = true;
        }

        private static bool CheckAndLogError(string actionName, PortAudio.PaError errorCode)
        {
            if (errorCode == PortAudio.PaError.paNoError)
            {
                Log($"{actionName}: OK");
                return false;
            }

            Log($"{actionName} error: {PortAudio.Pa_GetErrorText(errorCode)}", LogType.Error);
            if (errorCode == PortAudio.PaError.paUnanticipatedHostError)
            {
                PortAudio.PaHostErrorInfo errorInfo = PortAudio.Pa_GetLastHostErrorInfo();
                Log($"Host error API type: {errorInfo.hostApiType}", LogType.Error);
                Log($"Host error code: {errorInfo.errorCode}", LogType.Error);
                Log($"Host error text: {errorInfo.errorText}", LogType.Error);
            }

            return true;
        }

        internal static void Log(string message, LogType logType = LogType.Log)
        {
            Debug.LogFormat(logType, LogOption.None, null, $"PortAudioForUnity - {message}");
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
