using PortAudioForUnity;
using UnityEngine;
using UnityEngine.UIElements;

public class PlaybackDemoSceneControl : AbstractPortAudioDemoSceneControl
{
    public AudioClip demoAudioClip;
    private float[] demoAudioClipSamples;
    private int outputSampleIndex;

    public int bufferLengthInSeconds = 2;
    public int sampleRate;
    public int outputChannelCount;
    public bool loop;

    private Button startPlaybackButton;
    private Button stopPlaybackButton;

    protected override DeviceInfo InputDeviceInfo => null;

    protected override void Start()
    {
        base.Start();

        if (outputChannelCount <= 0)
        {
            outputChannelCount = demoAudioClip.channels;
        }
        if (sampleRate <= 0)
        {
            sampleRate = (int)OutputDeviceInfo.DefaultSampleRate;
        }
        Debug.Log($"Input channel count: {outputChannelCount}");
        Debug.Log($"Sample rate: {sampleRate}");
        Debug.Log($"Loop recording: {loop}");

        demoAudioClipSamples = new float[demoAudioClip.samples * demoAudioClip.channels];
        demoAudioClip.GetData(demoAudioClipSamples, 0);

        PortAudioUtils.StartPlayback(
            OutputDeviceInfo,
            outputChannelCount,
            bufferLengthInSeconds,
            sampleRate,
            OnFillSampleBuffer);

        if (!loop)
        {
            Debug.Log($"Will stop playback in {demoAudioClip.length} seconds");
            StartCoroutine(ExecuteAfterDelayInSeconds(demoAudioClip.length, () => StopPlayback()));
        }

        InitUi();

        Debug.Log("Start done");
    }

    private void OnFillSampleBuffer(float[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            outputSampleIndex %= demoAudioClipSamples.Length;
            data[i] = demoAudioClipSamples[outputSampleIndex];
            outputSampleIndex++;

            if (outputSampleIndex >= demoAudioClipSamples.Length
                && !loop)
            {
                return;
            }
        }
    }

    protected override void InitUi()
    {
        base.InitUi();

        UIDocument uiDocument = FindObjectOfType<UIDocument>();
        startPlaybackButton = uiDocument.rootVisualElement.Q<Button>("startPlaybackButton");
        stopPlaybackButton = uiDocument.rootVisualElement.Q<Button>("stopPlaybackButton");

        startPlaybackButton.RegisterCallback<ClickEvent>(_ =>
            PortAudioUtils.StartPlayback(OutputDeviceInfo, outputChannelCount, bufferLengthInSeconds, sampleRate, OnFillSampleBuffer));
        stopPlaybackButton.RegisterCallback<ClickEvent>(_ => StopPlayback());
    }

    private void StopPlayback()
    {
        PortAudioUtils.StopPlayback(OutputDeviceInfo);
    }
}
