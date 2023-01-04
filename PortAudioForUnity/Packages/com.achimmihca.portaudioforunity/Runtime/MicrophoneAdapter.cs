using UnityEngine;

namespace PortAudioForUnity
{
    public static class MicrophoneAdapter
    {
        public static bool UsePortAudio;

        public static string[] Devices
        {
            get
            {
                if (UsePortAudio)
                {
                    return PortAudioMicrophone.Devices;
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
                return PortAudioMicrophone.IsRecording(deviceName);
            }
            else
            {
                return Microphone.IsRecording(deviceName);
            }
        }

        public static AudioClip Start(
            string deviceName,
            bool loop,
            int lengthSec,
            int frequency)
        {
            if (UsePortAudio)
            {
                return PortAudioMicrophone.Start(deviceName, loop, lengthSec, frequency);
            }
            else
            {
                return Microphone.Start(deviceName, loop, lengthSec, frequency);
            }
        }

        public static void End(string deviceName)
        {
            if (UsePortAudio)
            {
                PortAudioMicrophone.End(deviceName);
            }
            else
            {
                Microphone.End(deviceName);
            }
        }

        public static int GetPosition(string deviceName)
        {
            if (UsePortAudio)
            {
                // No equivalent
                return 0;
            }
            else
            {
                return Microphone.GetPosition(deviceName);
            }
        }

        public static float[] GetRecordedSamples(string deviceName, int channelIndex, AudioClip microphoneAudioClip)
        {
            if (UsePortAudio)
            {
                return PortAudioUtils.GetRecordedSamples(deviceName, channelIndex);
            }
            else
            {
                float[] samples = new float[microphoneAudioClip.samples];
                microphoneAudioClip.GetData(samples, 0);
                return samples;
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
                PortAudioMicrophone.GetDeviceCaps(deviceName, out minFreq, out maxFreq, out channelCount);
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
