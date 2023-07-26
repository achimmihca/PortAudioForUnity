using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PortAudioForUnity;
using PortAudioSharp;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

public class SampleSceneControl : MonoBehaviour
{
    public bool overwriteHostApi;
    public HostApi hostApi = HostApi.WASAPI;
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

    private float audioWaveFormRefreshRateTimeInSeconds = 0;

    private void Start()
    {
        Application.targetFrameRate = targetFrameRate;

        Debug.Log("Start");

        string hostApiNameCsv = string.Join(", ", PortAudioUtils.HostApiInfos
            .Select(hostApiInfo => $"'{hostApiInfo.Name}' ({hostApiInfo.HostApi})"));
        Debug.Log($"Available host APIs: {hostApiNameCsv}");
        Debug.Log($"Default host API: {PortAudioUtils.DefaultHostApiInfo.HostApi}");

        foreach (DeviceInfo deviceInfo in PortAudioUtils.DeviceInfos)
        {
            Debug.Log($"Device: '{deviceInfo.Name}'," +
                      $" Host API: {deviceInfo.HostApi}," +
                      $" host API device index: {deviceInfo.HostApiDeviceIndex}," +
                      $" global device index: {deviceInfo.GlobalDeviceIndex}," +
                      $" low input latency: {deviceInfo.DefaultLowInputLatency}," +
                      $" low output latency: {deviceInfo.DefaultLowOutputLatency}," +
                      $" sample rate: {deviceInfo.DefaultSampleRate}," +
                      $" input channels: {deviceInfo.MaxInputChannels}, " +
                      $" output channels: {deviceInfo.MaxOutputChannels}");
        }

        if (overwriteHostApi)
        {
            MicrophoneAdapter.SetHostApi(hostApi);
        }
        Debug.Log($"Using host API: {MicrophoneAdapter.GetHostApi()}");

        inputDeviceName = PortAudioUtils.DefaultInputDeviceInfo.Name;
        outputDeviceName = directlyPlayRecordedAudio
            ? PortAudioUtils.DefaultOutputDeviceInfo.Name
            : "";
        Debug.Log($"Input device: {inputDeviceName}");
        Debug.Log($"Output device: {outputDeviceName}");

        inputChannelCount = PortAudioUtils.DefaultInputDeviceInfo.MaxInputChannels;

        Debug.Log($"Loop recording: {loop}");

        if (sampleRate <= 0)
        {
            sampleRate = (int)PortAudioUtils.DefaultInputDeviceInfo.DefaultSampleRate;
        }
        Debug.Log($"Sample rate: {sampleRate}");

        PortAudioUtils.StartRecording(
            PortAudioUtils.DefaultInputDeviceInfo,
            loop,
            recordingLengthInSeconds,
            sampleRate,
            PortAudioUtils.DefaultOutputDeviceInfo,
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
        PortAudioUtils.GetRecordedSamples(PortAudioUtils.DefaultInputDeviceInfo, 0, firstChannelSamples);
        firstChannelAudioWaveFormVisualization.DrawWaveFormMinAndMaxValues(firstChannelSamples);

        if (inputChannelCount > 1)
        {
            float[] secondChannelSamples = new float[sampleRate * recordingLengthInSeconds];
            PortAudioUtils.GetRecordedSamples(PortAudioUtils.DefaultInputDeviceInfo, 1, secondChannelSamples);
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
            PortAudioUtils.StartRecording(PortAudioUtils.DefaultInputDeviceInfo, loop, recordingLengthInSeconds, sampleRate, PortAudioUtils.DefaultOutputDeviceInfo, directlyPlayRecordedAudioAmplificationFactor));
        stopRecordingButton.RegisterCallback<ClickEvent>(evt => StopRecording());
        playRecordingMonoButton.RegisterCallback<ClickEvent>(evt => PlayRecordedAudioMono());
        playRecordingAllChannelsButton.RegisterCallback<ClickEvent>(evt => PlayRecordedAudioAllChannels());
    }

    private void StopRecording()
    {
        PortAudioUtils.StopRecording(PortAudioUtils.DefaultInputDeviceInfo);
    }

    private void PlayRecordedAudioAllChannels()
    {
        DestroyAudioClips();

        float[] allChannelsSamples = new float[sampleRate * recordingLengthInSeconds * inputChannelCount];
        PortAudioUtils.GetAllRecordedSamples(PortAudioUtils.DefaultInputDeviceInfo, allChannelsSamples);
        allChannelsAudioClip = AudioClip.Create("Microphone Samples AudioClip (all channels)", recordingLengthInSeconds * sampleRate * inputChannelCount, inputChannelCount, sampleRate, false);
        allChannelsAudioClip.SetData(allChannelsSamples, 0);

        audioSource.clip = allChannelsAudioClip;
        audioSource.Play();
    }

    private void PlayRecordedAudioMono()
    {
        DestroyAudioClips();

        float[] singleChannelSamples = new float[sampleRate * recordingLengthInSeconds];
        PortAudioUtils.GetRecordedSamples(PortAudioUtils.DefaultInputDeviceInfo, inputChannelIndex, singleChannelSamples);
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
