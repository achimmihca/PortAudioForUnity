using System;
using System.Collections;
using PortAudioForUnity;
using UnityEngine;

// See the original portaudio example: http://portaudio.com/docs/v19-doxydocs/paex__record_8c_source.html
// and the used C# binding of portaudio: https://github.com/tlove123/portaudiosharp
public class PortAudioTest : MonoBehaviour
{
    private const int SampleRate = 44100;
    private const int NumSeconds = 5;

    private static long startTimeMillis;

    public int targetFrameRate = 30;
    public AudioSource audioSource;

    private string inputDeviceName;
    private string outputDeviceName;

    private void Awake()
    {
        Application.targetFrameRate = targetFrameRate;
        startTimeMillis = 0;
    }

    private void Start()
    {
        Debug.Log("Start");
        startTimeMillis = GetUnixTimeMilliseconds();

        inputDeviceName = PortAudioUtils.GetDefaultInputDeviceName();
        outputDeviceName = PortAudioUtils.GetDefaultOutputDeviceName();

        AudioClip recordingAudioClip = PortAudioMicrophone.Start(
            inputDeviceName,
            false,
            NumSeconds,
            SampleRate,
            0,
            outputDeviceName);

        audioSource.clip = recordingAudioClip;

        StartCoroutine(ExecuteAfterDelayInSeconds(NumSeconds, () => StopRecording()));

        Debug.Log("Start done");
    }

    private void StopRecording()
    {
        Debug.Log($"Stopping after {(GetUnixTimeMilliseconds() - startTimeMillis) / 1000} seconds");
        PortAudioMicrophone.End(inputDeviceName);

        PortAudioUtils.UpdateAudioClipDataWithRecordedSamples(inputDeviceName);
        audioSource.Play();
    }

    private static IEnumerator ExecuteAfterDelayInSeconds(float delayInSeconds, Action action)
    {
        yield return new WaitForSeconds(delayInSeconds);
        // Code to execute after the delay
        action();
    }

    private static long GetUnixTimeMilliseconds()
    {
        // See https://stackoverflow.com/questions/4016483/get-time-in-milliseconds-using-c-sharp
        return DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }
}