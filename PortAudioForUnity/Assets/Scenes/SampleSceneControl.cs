using System;
using System.Collections;
using PortAudioForUnity;
using UnityEngine;

public class SampleSceneControl : MonoBehaviour
{
    private const int SampleRate = 44100;
    private const int RecordingLengthInSeconds = 5;

    public int targetFrameRate = 30;
    public AudioSource audioSource;

    private string inputDeviceName;
    private string outputDeviceName;

    private void Awake()
    {
        Application.targetFrameRate = targetFrameRate;
    }

    private void Start()
    {
        Debug.Log("Start");
        inputDeviceName = PortAudioUtils.GetDefaultInputDeviceName();
        outputDeviceName = PortAudioUtils.GetDefaultOutputDeviceName();
        Debug.Log($"Input device: {inputDeviceName}");
        Debug.Log($"Output device: {outputDeviceName}");

        AudioClip recordingAudioClip = PortAudioMicrophone.Start(
            inputDeviceName,
            false,
            RecordingLengthInSeconds,
            SampleRate,
            0,
            outputDeviceName);
        audioSource.clip = recordingAudioClip;

        StartCoroutine(ExecuteAfterDelayInSeconds(RecordingLengthInSeconds, () => StopRecording()));

        Debug.Log("Start done");
    }

    private void StopRecording()
    {
        Debug.Log($"StopRecording");

        PortAudioMicrophone.End(inputDeviceName);

        // Play recorded samples using Unity's AudioSource
        PortAudioUtils.UpdateAudioClipDataWithRecordedSamples(inputDeviceName);
        audioSource.Play();
    }

    private static IEnumerator ExecuteAfterDelayInSeconds(float delayInSeconds, Action action)
    {
        yield return new WaitForSeconds(delayInSeconds);
        action();
    }
}
