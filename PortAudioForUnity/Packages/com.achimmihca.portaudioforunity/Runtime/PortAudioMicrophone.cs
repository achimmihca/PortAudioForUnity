using UnityEngine;

namespace PortAudioForUnity
{
    public static class PortAudioMicrophone
    {
        public static string[] Devices
        {
            get
            {
                return PortAudioUtils.GetInputDeviceNames();
            }
        }

        public static void GetDeviceCaps(
            string deviceName,
            out int minFreq,
            out int maxFreq,
            out int channelCount)
        {
            PortAudioUtils.GetInputDeviceCapabilities(deviceName, out minFreq, out maxFreq, out channelCount);
        }

        public static AudioClip Start(
            string deviceName,
            bool loop,
            int lengthSec,
            int frequency,
            int deviceChannel,
            string outputDeviceName = null)
        {
            return PortAudioUtils.StartRecording(deviceName, loop, lengthSec, frequency, deviceChannel, outputDeviceName);
        }

        public static void End(string deviceName)
        {
            PortAudioUtils.StopRecording(deviceName);
        }

        public static float[] GetRecordedSamples(string deviceName, int channelIndex)
        {
            return PortAudioUtils.GetRecordedSamples(deviceName, channelIndex);
        }

        public static bool IsRecording(string deviceName)
        {
            return PortAudioUtils.IsRecording(deviceName);
        }

        public static int GetPosition(string deviceName)
        {
            PortAudioUtils.UpdateAudioClipDataWithRecordedSamples(deviceName);
            return PortAudioUtils.GetSampleRecorderPosition(deviceName);
        }
    }
}
