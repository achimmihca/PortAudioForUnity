using System.Linq;
using UnityEngine;

namespace PortAudioForUnity
{
    public static class MicrophoneAdapter
    {
        public static bool UsePortAudio;

        private static int selectedHostApiAsInt = -1;

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

        public static string[] Devices
        {
            get
            {
                if (UsePortAudio)
                {
                    return PortAudioUtils.DeviceInfos
                        .Where(deviceInfo => deviceInfo.MaxOutputChannels > 0)
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
                DeviceInfo deviceInfo = GetSelectedHostApiDeviceInfo(deviceName);
                return PortAudioUtils.IsRecording(deviceInfo);
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
                DeviceInfo inputDeviceInfo = GetSelectedHostApiDeviceInfo(inputDeviceName);
                DeviceInfo outputDeviceInfo = GetSelectedHostApiDeviceInfo(outputDeviceName);
                PortAudioUtils.StartRecording(inputDeviceInfo, loop, bufferLengthSec, sampleRate, outputDeviceInfo, directOutputAmplificationFactor);
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
                DeviceInfo deviceInfo = GetSelectedHostApiDeviceInfo(deviceName);
                PortAudioUtils.StopRecording(deviceInfo);
            }
            else
            {
                Microphone.End(deviceName);
            }
        }

        private static DeviceInfo GetSelectedHostApiDeviceInfo(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName))
            {
                return null;
            }

            HostApi hostApi = GetHostApi();
            DeviceInfo deviceInfo = PortAudioUtils.DeviceInfos
                .FirstOrDefault(deviceInfo => deviceInfo.HostApi == hostApi
                                              && deviceInfo.Name == deviceName);
            return deviceInfo;
        }

        public static int GetPosition(string deviceName)
        {
            if (UsePortAudio)
            {
                DeviceInfo deviceInfo = GetSelectedHostApiDeviceInfo(deviceName);
                return PortAudioUtils.GetSingleChannelRecordingPosition(deviceInfo);
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
                DeviceInfo deviceInfo = GetSelectedHostApiDeviceInfo(deviceName);
                PortAudioUtils.GetRecordedSamples(deviceInfo, channelIndex, bufferToBeFilled);
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
                DeviceInfo deviceInfo = GetSelectedHostApiDeviceInfo(deviceName);
                minFreq = (int)deviceInfo.DefaultSampleRate;
                maxFreq = (int)deviceInfo.DefaultSampleRate;
                channelCount = deviceInfo.MaxInputChannels;
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
