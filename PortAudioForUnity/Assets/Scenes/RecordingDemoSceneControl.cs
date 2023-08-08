using PortAudioForUnity;
using UnityEngine;
using UnityEngine.UIElements;

public class RecordingDemoSceneControl : AbstractPortAudioDemoSceneControl
{
    public AudioSource audioSource;
    private AudioClip monoAudioClip;
    private AudioClip allChannelsAudioClip;

    public int bufferLengthInSeconds = 2;
    public int sampleRate;
    public int inputChannelIndex;
    public bool loop;
    public bool playRecordedAudio;
    public float playRecordedAudioAmplificationFactor = 1;
    public float audioWaveFormRefreshRateTimeInSeconds;

    protected override DeviceInfo OutputDeviceInfo => playRecordedAudio ? base.OutputDeviceInfo : null;

    private Button startRecordingButton;
    private Button stopRecordingButton;
    private Button playRecordingMonoButton;
    private Button playRecordingAllChannelsButton;

    private int inputChannelCount;

    protected override void Start()
    {
        base.Start();

        if (inputChannelCount <= 0)
        {
            inputChannelCount = InputDeviceInfo.MaxInputChannels;
        }
        if (sampleRate <= 0)
        {
            sampleRate = (int)InputDeviceInfo.DefaultSampleRate;
        }
        Debug.Log($"Input channel count: {inputChannelCount}");
        Debug.Log($"Sample rate: {sampleRate}");
        Debug.Log($"Loop recording: {loop}");

        PortAudioUtils.StartRecording(
            InputDeviceInfo,
            loop,
            bufferLengthInSeconds,
            sampleRate,
            OutputDeviceInfo,
            playRecordedAudioAmplificationFactor);

        if (!loop)
        {
            Debug.Log($"Will stop recording in {bufferLengthInSeconds} seconds");
            StartCoroutine(ExecuteAfterDelayInSeconds(bufferLengthInSeconds, () => StopRecording()));
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
            int textureWidth = 200;
            int textureHeight = 100;
            firstChannelAudioWaveFormVisualization = new AudioWaveFormVisualization(gameObject, firstChannelAudioWaveForm, textureWidth, textureHeight);
            secondChannelAudioWaveFormVisualization = new AudioWaveFormVisualization(gameObject, secondChannelAudioWaveForm, textureWidth, textureHeight);
        }

        float currentTimeInSeconds = Time.time;
        if (currentTimeInSeconds - lastUpdateTimeInSeconds > audioWaveFormRefreshRateTimeInSeconds
            && firstChannelAudioWaveFormVisualization != null
            && secondChannelAudioWaveFormVisualization != null)
        {
            UpdateAudioWaveForm();
            lastUpdateTimeInSeconds = currentTimeInSeconds;
        }

        PortAudioUtils.SetOutputAmplificationFactor(InputDeviceInfo, playRecordedAudioAmplificationFactor);
    }

    private void UpdateAudioWaveForm()
    {
        float[] firstChannelSamples = new float[sampleRate * bufferLengthInSeconds];
        PortAudioUtils.GetRecordedSamples(InputDeviceInfo, 0, firstChannelSamples);
        firstChannelAudioWaveFormVisualization.DrawWaveFormMinAndMaxValues(firstChannelSamples);

        if (inputChannelCount > 1)
        {
            float[] secondChannelSamples = new float[sampleRate * bufferLengthInSeconds];
            PortAudioUtils.GetRecordedSamples(InputDeviceInfo, 1, secondChannelSamples);
            secondChannelAudioWaveFormVisualization.DrawWaveFormMinAndMaxValues(secondChannelSamples);
        }
    }

    protected override void InitUi()
    {
        base.InitUi();

        UIDocument uiDocument = FindObjectOfType<UIDocument>();
        startRecordingButton = uiDocument.rootVisualElement.Q<Button>("startRecordingButton");
        stopRecordingButton = uiDocument.rootVisualElement.Q<Button>("stopRecordingButton");
        playRecordingMonoButton = uiDocument.rootVisualElement.Q<Button>("playRecordingMonoButton");
        playRecordingAllChannelsButton = uiDocument.rootVisualElement.Q<Button>("playRecordingAllChannelsButton");

        startRecordingButton.RegisterCallback<ClickEvent>(_ =>
        PortAudioUtils.StartRecording(InputDeviceInfo, loop, bufferLengthInSeconds, sampleRate, OutputDeviceInfo, playRecordedAudioAmplificationFactor));
        stopRecordingButton.RegisterCallback<ClickEvent>(_ => StopRecording());
        playRecordingMonoButton.RegisterCallback<ClickEvent>(_ => PlayRecordedAudioMono());
        playRecordingAllChannelsButton.RegisterCallback<ClickEvent>(_ => PlayRecordedAudioAllChannels());
    }

    private void StopRecording()
    {
        PortAudioUtils.StopRecording(InputDeviceInfo);
    }

    private void PlayRecordedAudioAllChannels()
    {
        DestroyAudioClips();

        float[] allChannelsSamples = new float[sampleRate * bufferLengthInSeconds * inputChannelCount];
        PortAudioUtils.GetAllRecordedSamples(InputDeviceInfo, allChannelsSamples);
        allChannelsAudioClip = AudioClip.Create("Microphone Samples AudioClip (all channels)", bufferLengthInSeconds * sampleRate * inputChannelCount, inputChannelCount, sampleRate, false);
        allChannelsAudioClip.SetData(allChannelsSamples, 0);

        audioSource.clip = allChannelsAudioClip;
        audioSource.Play();
    }

    private void PlayRecordedAudioMono()
    {
        DestroyAudioClips();

        float[] singleChannelSamples = new float[sampleRate * bufferLengthInSeconds];
        PortAudioUtils.GetRecordedSamples(InputDeviceInfo, inputChannelIndex, singleChannelSamples);
        monoAudioClip = AudioClip.Create("Microphone Samples AudioClip (mono)", bufferLengthInSeconds * sampleRate, 1, sampleRate, false);
        monoAudioClip.SetData(singleChannelSamples, 0);

        audioSource.clip = monoAudioClip;
        audioSource.Play();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
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
}
