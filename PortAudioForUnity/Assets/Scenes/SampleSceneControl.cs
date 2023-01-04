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
    private AudioClip monoAudioClip;
    private AudioClip allChannelsAudioClip;

    public int recordingLengthInSeconds = 2;
    public int sampleRate;
    public int inputChannelCount;
    public int inputChannelIndex;
    public bool loop;
    public bool directlyPlayRecordedAudio;
    public float directlyPlayRecordedAudioAmplificationFactor = 1;

    private string inputDeviceName;
    private string outputDeviceName;

    private Button startRecordingButton;
    private Button stopRecordingButton;
    private Button playRecordingMonoButton;
    private Button playRecordingAllChannelsButton;
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

        PortAudioUtils.StartRecording(
            inputDeviceName,
            loop,
            recordingLengthInSeconds,
            sampleRate,
            outputDeviceName,
            directlyPlayRecordedAudioAmplificationFactor);

        if (!loop)
        {
            Debug.Log($"Will stop recording in {recordingLengthInSeconds} seconds");
            StartCoroutine(ExecuteAfterDelayInSeconds(recordingLengthInSeconds, () => StopRecording()));
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
        float[] firstChannelSamples = new float[sampleRate * recordingLengthInSeconds];
        PortAudioUtils.GetRecordedSamples(inputDeviceName, 0, firstChannelSamples);
        firstChannelAudioWaveFormVisualization.DrawWaveFormMinAndMaxValues(firstChannelSamples);

        if (inputChannelCount > 1)
        {
            float[] secondChannelSamples = new float[sampleRate * recordingLengthInSeconds];
            PortAudioUtils.GetRecordedSamples(inputDeviceName, 1, secondChannelSamples);
            secondChannelAudioWaveFormVisualization.DrawWaveFormMinAndMaxValues(secondChannelSamples);
        }
    }

    private void InitUi()
    {
        UIDocument uiDocument = FindObjectOfType<UIDocument>();
        startRecordingButton = uiDocument.rootVisualElement.Q<Button>("startRecordingButton");
        stopRecordingButton = uiDocument.rootVisualElement.Q<Button>("stopRecordingButton");
        playRecordingMonoButton = uiDocument.rootVisualElement.Q<Button>("playRecordingMonoButton");
        playRecordingAllChannelsButton = uiDocument.rootVisualElement.Q<Button>("playRecordingAllChannelsButton");
        firstChannelAudioWaveForm = uiDocument.rootVisualElement.Q<VisualElement>("firstChannelAudioWaveForm");
        secondChannelAudioWaveForm = uiDocument.rootVisualElement.Q<VisualElement>("secondChannelAudioWaveForm");

        startRecordingButton.RegisterCallback<ClickEvent>(evt =>
            PortAudioUtils.StartRecording(inputDeviceName, loop, recordingLengthInSeconds, sampleRate, outputDeviceName, directlyPlayRecordedAudioAmplificationFactor));
        stopRecordingButton.RegisterCallback<ClickEvent>(evt => StopRecording());
        playRecordingMonoButton.RegisterCallback<ClickEvent>(evt => PlayRecordedAudioMono());
        playRecordingAllChannelsButton.RegisterCallback<ClickEvent>(evt => PlayRecordedAudioAllChannels());
    }

    private void StopRecording()
    {
        PortAudioUtils.StopRecording(inputDeviceName);
    }

    private void PlayRecordedAudioAllChannels()
    {
        DestroyAudioClips();

        float[] allChannelsSamples = new float[sampleRate * recordingLengthInSeconds * inputChannelCount];
        PortAudioUtils.GetAllRecordedSamples(inputDeviceName, allChannelsSamples);
        allChannelsAudioClip = AudioClip.Create("Microphone Samples AudioClip (all channels)", recordingLengthInSeconds * sampleRate * inputChannelCount, inputChannelCount, sampleRate, false);
        allChannelsAudioClip.SetData(allChannelsSamples, 0);

        audioSource.clip = allChannelsAudioClip;
        audioSource.Play();
    }

    private void PlayRecordedAudioMono()
    {
        DestroyAudioClips();

        float[] singleChannelSamples = new float[sampleRate * recordingLengthInSeconds];
        PortAudioUtils.GetRecordedSamples(inputDeviceName, inputChannelIndex, singleChannelSamples);
        monoAudioClip = AudioClip.Create("Microphone Samples AudioClip (mono)", recordingLengthInSeconds * sampleRate, 1, sampleRate, false);
        monoAudioClip.SetData(singleChannelSamples, 0);

        audioSource.clip = monoAudioClip;
        audioSource.Play();
    }

    private void OnDestroy()
    {
        DestroyAudioClips();
    }

    private void DestroyAudioClips()
    {
        if (allChannelsAudioClip != null)
        {
            Destroy(allChannelsAudioClip);
            allChannelsAudioClip = null;
        }

        if (monoAudioClip != null)
        {
            Destroy(monoAudioClip);
            monoAudioClip = null;
        }
    }

    private static IEnumerator ExecuteAfterDelayInSeconds(float delayInSeconds, Action action)
    {
        yield return new WaitForSeconds(delayInSeconds);
        action();
    }
}
