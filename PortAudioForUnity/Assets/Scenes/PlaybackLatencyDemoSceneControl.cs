using System;
using PortAudioForUnity;
using UnityEngine;

/**
 * This script demonstrates that PortAudio with WASAPI host API has less latency than OnAudioFilterRead.
 */
[RequireComponent(typeof(AudioSource))]
public class AudioPlaybackLatencyTest : MonoBehaviour
{
    public HostApi hostApi = HostApi.WASAPI;

    public bool useUnity = true;
    public bool usePortAudio = true;

    private AudioSource audioSource;

    private long shouldPlayTimeInMillis;
    private bool shouldPlay;
    private bool oldShouldPlay;

    private bool unityFilledSound;
    private bool oldUnityFilledSound;

    private bool portAudioFilledSound;
    private bool oldPortAudioFilledSound;

    private SineToneGenerator unityApiSineToneGenerator;
    private SineToneGenerator portAudioApiSineToneGenerator;

    private HostApiInfo HostApiInfo => PortAudioUtils.GetHostApiInfo(MicrophoneAdapter.GetHostApi());
    private DeviceInfo OutputDeviceInfo => PortAudioUtils.GetDeviceInfo(HostApiInfo.DefaultOutputDeviceGlobalIndex);

    private int bufferLengthInSeconds = 1;

    private void Awake()
    {
        MicrophoneAdapter.SetHostApi(hostApi);

        audioSource = GetComponent<AudioSource>();
        audioSource.Play();

        unityApiSineToneGenerator = new(880, AudioSettings.outputSampleRate);
        portAudioApiSineToneGenerator = new(440, (int)OutputDeviceInfo.DefaultSampleRate);

        PortAudioUtils.StartPlayback(
            OutputDeviceInfo,
            OutputDeviceInfo.MaxOutputChannels,
            bufferLengthInSeconds,
            (int)OutputDeviceInfo.DefaultSampleRate,
            OnPortAudioReadSamples);
    }

    private void Update()
    {
        // Press right mouse button to play sound
        shouldPlay = Input.GetMouseButton(0);
        if (shouldPlay != oldShouldPlay)
        {
            shouldPlayTimeInMillis = GetUnixTimeMilliseconds();
            Debug.Log($"shouldPlay: {shouldPlay} at {shouldPlayTimeInMillis} ms");
            oldShouldPlay = shouldPlay;
        }
    }

    private void OnPortAudioReadSamples(float[] data)
    {
        if (!usePortAudio)
        {
            return;
        }

        portAudioFilledSound = shouldPlay;
        if (portAudioFilledSound != oldPortAudioFilledSound)
        {
            long currentTime = GetUnixTimeMilliseconds();
            long latencyInMillis = currentTime - shouldPlayTimeInMillis;
            Debug.Log($"portAudioFilledSound: {portAudioFilledSound} at {currentTime} ms => {latencyInMillis} ms latency");
            oldPortAudioFilledSound = portAudioFilledSound;
        }

        FillBuffer(data, OutputDeviceInfo.MaxOutputChannels, portAudioApiSineToneGenerator);
    }

    private void OnAudioFilterRead(float[] data, int channelCount)
    {
        if (!useUnity)
        {
            return;
        }

        unityFilledSound = shouldPlay;
        if (unityFilledSound != oldUnityFilledSound)
        {
            long currentTime = GetUnixTimeMilliseconds();
            long latencyInMillis = currentTime - shouldPlayTimeInMillis;
            Debug.Log($"unityFilledSound: {unityFilledSound} at {currentTime} ms => {latencyInMillis} ms latency");
            oldUnityFilledSound = unityFilledSound;
        }

        FillBuffer(data, channelCount, unityApiSineToneGenerator);
    }

    private void FillBuffer(float[] data, int channelCount, SineToneGenerator sineToneGenerator)
    {
        if (!shouldPlay)
        {
            // Output silence
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = 0;
            }
            return;
        }

        sineToneGenerator.FillBuffer(data, channelCount);
    }

    private static long GetUnixTimeMilliseconds()
    {
        // See https://stackoverflow.com/questions/4016483/get-time-in-milliseconds-using-c-sharp
        return DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }
}
