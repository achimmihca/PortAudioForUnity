using System.Linq;
using UnityEngine;

namespace PortAudioForUnity
{
    public static class MicrophoneAdapter
    {
        public static bool UsePortAudio;

        private static int selectedHostApiAsInt = -1;

        public static DeviceInfo DefaultInputDeviceInfo
        {
            get
            {
                HostApiInfo hostApiInfo = GetHostApiInfo();
                return hostApiInfo == null
                    ? null
                    : PortAudioUtils.GetDeviceInfo(hostApiInfo.DefaultInputDeviceGlobalIndex);
            }
        }

        public static DeviceInfo DefaultOutputDeviceInfo
        {
            get
            {
                HostApiInfo hostApiInfo = GetHostApiInfo();
                return hostApiInfo == null
                    ? null
                    : PortAudioUtils.GetDeviceInfo(hostApiInfo.DefaultOutputDeviceGlobalIndex);
            }
        }

        public static void SetHostApi(HostApi hostApi)
        {
            selectedHostApiAsInt = (int)hostApi;
        }

        public static HostApi GetHostApi()
        {
            if (selectedHostApiAsInt < 0)
            {
                return PortAudioUtils.DefaultHostApiInfo.HostApi;
            }
            else
            {
                return (HostApi)selectedHostApiAsInt;
            }
        }

        public static HostApiInfo GetHostApiInfo()
        {
            return PortAudioUtils.GetHostApiInfo(GetHostApi());
        }

        public static string[] Devices
        {
            get
            {
                if (UsePortAudio)
                {
                    return PortAudioUtils.DeviceInfos
                        .Where(deviceInfo => deviceInfo.HostApi == GetHostApi()
                                             && deviceInfo.MaxInputChannels > 0)
                        .Select(deviceInfo => deviceInfo.Name)
                        .ToArray();
                }
                else
                {
                    return Microphone.devices;
                }
            }
        }

        public static bool IsRecording(string deviceName)
        {
            if (UsePortAudio)
            {
                if (TryGetSelectedHostApiDeviceInfo(deviceName, out DeviceInfo deviceInfo))
                {
                    return PortAudioUtils.IsRecording(deviceInfo);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return Microphone.IsRecording(deviceName);
            }
        }

        public static AudioClip Start(
            string inputDeviceName,
            bool loop,
            int bufferLengthSec,
            int sampleRate,
            string outputDeviceName = "",
            float directOutputAmplificationFactor = 1)
        {
            if (UsePortAudio)
            {
                if (TryGetSelectedHostApiDeviceInfo(inputDeviceName, out DeviceInfo inputDeviceInfo))
                {
                    DeviceInfo outputDeviceInfo;
                    if (string.IsNullOrEmpty(outputDeviceName))
                    {
                        outputDeviceInfo = null;
                    }
                    else
                    {
                        if (!TryGetSelectedHostApiDeviceInfo(outputDeviceName, out outputDeviceInfo))
                        {
                            PortAudioUtils.Log($"Starting recording without output device because output device '{outputDeviceName}' not found in host API {GetHostApi()}", LogType.Warning);
                        }
                    }

                    PortAudioUtils.StartRecording(inputDeviceInfo, loop, bufferLengthSec, sampleRate, outputDeviceInfo, directOutputAmplificationFactor);
                }
                return null;
            }
            else
            {
                return Microphone.Start(inputDeviceName, loop, bufferLengthSec, sampleRate);
            }
        }

        public static void End(string deviceName)
        {
            if (UsePortAudio)
            {
                if (TryGetSelectedHostApiDeviceInfo(deviceName, out DeviceInfo deviceInfo))
                {
                    PortAudioUtils.StopRecording(deviceInfo);
                }
            }
            else
            {
                Microphone.End(deviceName);
            }
        }

        private static bool TryGetSelectedHostApiDeviceInfo(string deviceName, out DeviceInfo deviceInfo)
        {
            if (string.IsNullOrEmpty(deviceName))
            {
                PortAudioUtils.Log($"Device name is null", LogType.Warning);
                deviceInfo = null;
                return false;
            }

            HostApi hostApi = GetHostApi();
            deviceInfo = PortAudioUtils.DeviceInfos
                .FirstOrDefault(deviceInfo => deviceInfo.HostApi == hostApi
                                              && deviceInfo.Name == deviceName);
            if (deviceInfo == null)
            {
                PortAudioUtils.Log($"Device '{deviceName}' not found in host API '{hostApi}'", LogType.Warning);
            }
            return deviceInfo != null;
        }

        public static int GetPosition(string deviceName)
        {
            if (UsePortAudio)
            {
                if (TryGetSelectedHostApiDeviceInfo(deviceName, out DeviceInfo deviceInfo))
                {
                    return PortAudioUtils.GetSingleChannelRecordingPosition(deviceInfo);
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                return Microphone.GetPosition(deviceName);
            }
        }

        public static void GetRecordedSamples(
            string deviceName,
            int channelIndex,
            AudioClip microphoneAudioClip,
            int recordingPosition,
            float[] bufferToBeFilled)
        {
            if (UsePortAudio)
            {
                if (TryGetSelectedHostApiDeviceInfo(deviceName, out DeviceInfo deviceInfo))
                {
                    PortAudioUtils.GetRecordedSamples(deviceInfo, channelIndex, bufferToBeFilled);
                }
            }
            else
            {
                microphoneAudioClip.GetData(bufferToBeFilled, recordingPosition);
            }
        }

        public static void GetDeviceCaps(
            string deviceName,
            out int minFreq,
            out int maxFreq,
            out int channelCount)
        {
            if (UsePortAudio)
            {
                if (TryGetSelectedHostApiDeviceInfo(deviceName, out DeviceInfo deviceInfo))
                {
                    minFreq = (int)deviceInfo.DefaultSampleRate;
                    maxFreq = (int)deviceInfo.DefaultSampleRate;
                    channelCount = deviceInfo.MaxInputChannels;
                }
                else
                {
                    minFreq = 0;
                    maxFreq = 0;
                    channelCount = 0;
                }
            }
            else
            {
                // Unity API can only handle one channel
                channelCount = 1;
                Microphone.GetDeviceCaps(deviceName, out minFreq, out maxFreq);
            }
        }
    }
}
