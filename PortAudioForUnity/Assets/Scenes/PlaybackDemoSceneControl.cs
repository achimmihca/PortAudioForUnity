using System;
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
    public bool playOnStart;

    private Button startPlaybackButton;
    private Button stopPlaybackButton;

    private HostApiInfo HostApiInfo => PortAudioUtils.GetHostApiInfo(MicrophoneAdapter.GetHostApi());
    private DeviceInfo OutputDeviceInfo => PortAudioUtils.GetDeviceInfo(HostApiInfo.DefaultOutputDeviceGlobalIndex);

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
        Debug.Log($"Loop playback: {loop}");

        demoAudioClipSamples = new float[demoAudioClip.samples * demoAudioClip.channels];
        demoAudioClip.GetData(demoAudioClipSamples, 0);

        if (playOnStart)
        {
            StartPlayback();
            if (!loop)
            {
                Debug.Log($"Will stop playback in {demoAudioClip.length} seconds");
                StartCoroutine(ExecuteAfterDelayInSeconds(demoAudioClip.length, () => StopPlayback()));
            }
        }

        InitUi();

        Debug.Log("Start done");
    }

    private void StartPlayback()
    {
        PortAudioUtils.StartPlayback(
            OutputDeviceInfo,
            outputChannelCount,
            bufferLengthInSeconds,
            sampleRate,
            OnFillSampleBuffer);
    }

    private void OnFillSampleBuffer(float[] data)
    {
        if (outputSampleIndex >= demoAudioClipSamples.Length
            && !loop)
        {
            Array.Clear(data, 0, data.Length);
            return;
        }

        for (int i = 0; i < data.Length; i++)
        {
            outputSampleIndex %= demoAudioClipSamples.Length;
            data[i] = demoAudioClipSamples[outputSampleIndex];
            outputSampleIndex++;

            if (outputSampleIndex >= demoAudioClipSamples.Length
                && !loop)
            {
                Array.Clear(data, i, data.Length - i);
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

        startPlaybackButton.RegisterCallback<ClickEvent>(_ => StartPlayback());
        stopPlaybackButton.RegisterCallback<ClickEvent>(_ => StopPlayback());
    }

    private void StopPlayback()
    {
        PortAudioUtils.StopPlayback(OutputDeviceInfo);
        outputSampleIndex = 0;
    }
}
