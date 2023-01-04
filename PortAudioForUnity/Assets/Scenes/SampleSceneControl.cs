using System;
using System.Collections;
using System.Diagnostics;
using PortAudioForUnity;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

public class SampleSceneControl : MonoBehaviour
{
    public int targetFrameRate = 30;
    public AudioSource audioSource;

    public int recordingLengthInSeconds = 2;
    public int sampleRate;
    public int inputChannelCount;
    public bool loop;
    public bool directlyPlayRecordedAudio;
    public float directlyPlayRecordedAudioAmplificationFactor = 1;

    private string inputDeviceName;
    private string outputDeviceName;

    private Button startRecordingButton;
    private Button stopRecordingButton;
    private VisualElement firstChannelAudioWaveForm;
    private VisualElement secondChannelAudioWaveForm;
    private AudioWaveFormVisualization firstChannelAudioWaveFormVisualization;
    private AudioWaveFormVisualization secondChannelAudioWaveFormVisualization;

    private float lastUpdateTimeInSeconds;

    private float audioWaveFormRefreshRateTimeInSeconds = 0.2f;

    private void Start()
    {
        Application.targetFrameRate = targetFrameRate;

        Debug.Log("Start");

        inputDeviceName = PortAudioUtils.GetDefaultInputDeviceName();
        outputDeviceName = directlyPlayRecordedAudio
            ? PortAudioUtils.GetDefaultOutputDeviceName()
            : "";
        Debug.Log($"Input device: {inputDeviceName}");
        Debug.Log($"Output device: {outputDeviceName}");

        PortAudioUtils.GetInputDeviceCapabilities(inputDeviceName, out int minSampleRate, out int maxSampleRate, out int maxInputChannelCount);
        inputChannelCount = maxInputChannelCount;

        Debug.Log($"Loop recording: {loop}");

        if (sampleRate <= 0)
        {
            sampleRate = maxSampleRate;
        }
        Debug.Log($"Sample rate: {sampleRate}");

        AudioClip recordingAudioClip = PortAudioMicrophone.Start(
            inputDeviceName,
            loop,
            recordingLengthInSeconds,
            sampleRate,
            outputDeviceName,
            directlyPlayRecordedAudioAmplificationFactor);
        audioSource.clip = recordingAudioClip;

        if (!loop)
        {
            Debug.Log($"Will stop recording in {recordingLengthInSeconds} seconds");
            StartCoroutine(ExecuteAfterDelayInSeconds(recordingLengthInSeconds, () => StopRecordingAndPlayRecordedAudio()));
        }

        InitUi();

        Debug.Log("Start done");
    }

    private void Update()
    {
        if (firstChannelAudioWaveFormVisualization == null
            && secondChannelAudioWaveFormVisualization == null
            && !float.IsNaN(firstChannelAudioWaveForm.resolvedStyle.width)
            && !float.IsNaN(firstChannelAudioWaveForm.resolvedStyle.height))
        {
            firstChannelAudioWaveFormVisualization = new AudioWaveFormVisualization(gameObject, firstChannelAudioWaveForm);
            secondChannelAudioWaveFormVisualization = new AudioWaveFormVisualization(gameObject, secondChannelAudioWaveForm);
        }

        float currentTimeInSeconds = Time.time;
        if (currentTimeInSeconds - lastUpdateTimeInSeconds > audioWaveFormRefreshRateTimeInSeconds
            && firstChannelAudioWaveFormVisualization != null
            && secondChannelAudioWaveFormVisualization != null)
        {
            UpdateAudioWaveForm();
            lastUpdateTimeInSeconds = currentTimeInSeconds;
        }
    }

    private void UpdateAudioWaveForm()
    {
        float[] firstChannelSamples = PortAudioUtils.GetRecordedSamples(inputDeviceName, 0);
        firstChannelAudioWaveFormVisualization.DrawWaveFormMinAndMaxValues(firstChannelSamples);

        if (inputChannelCount > 1)
        {
            float[] secondChannelSamples = PortAudioUtils.GetRecordedSamples(inputDeviceName, 1);
            secondChannelAudioWaveFormVisualization.DrawWaveFormMinAndMaxValues(secondChannelSamples);
        }
    }

    private void InitUi()
    {
        UIDocument uiDocument = FindObjectOfType<UIDocument>();
        startRecordingButton = uiDocument.rootVisualElement.Q<Button>("startRecordingButton");
        stopRecordingButton = uiDocument.rootVisualElement.Q<Button>("stopRecordingButton");
        firstChannelAudioWaveForm = uiDocument.rootVisualElement.Q<VisualElement>("firstChannelAudioWaveForm");
        secondChannelAudioWaveForm = uiDocument.rootVisualElement.Q<VisualElement>("secondChannelAudioWaveForm");

        startRecordingButton.RegisterCallback<ClickEvent>(evt =>
            PortAudioMicrophone.Start(inputDeviceName, loop, recordingLengthInSeconds, sampleRate, outputDeviceName, directlyPlayRecordedAudioAmplificationFactor));
        stopRecordingButton.RegisterCallback<ClickEvent>(evt =>
        {
            StopRecordingAndPlayRecordedAudio();
        });
    }

    private void StopRecordingAndPlayRecordedAudio()
    {
        Debug.Log($"StopRecordingAndPlayRecordedAudio");

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
