using System;
using Codice.Client.GameUI.Checkin;
using PortAudioForUnity;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(AudioSource))]
public class LatencyMeasurementSceneControl : MonoBehaviour
{
    public HostApi hostApi;
    public int inputDeviceChannelIndex;
    public float sampleVolumeThreshold;

    // Generate a sine wave in OnAudioFilterRead
    private int outputChannelCount = 2;
    private int totalSampleIndex;
    private AudioSource audioSource;

    // Measure time until first sample above threshold volume is recorded
    private bool shouldMakeNoise;
    private float maxSampleValue;
    private long noiseStartInMillis;
    private float[] micSampleBuffer;

    private int onAudioFilterReadSampleRate;

    public HostApiInfo HostApiInfo => PortAudioUtils.GetHostApiInfo(hostApi);
    private DeviceInfo InputDeviceInfo => PortAudioUtils.GetDeviceInfo(HostApiInfo.DefaultInputDeviceGlobalIndex);

    private Button startMeasurementButton;
    private VisualElement firstChannelAudioWaveForm;
    private AudioWaveFormVisualization firstChannelAudioWaveFormVisualization;

    private void Awake()
    {
        onAudioFilterReadSampleRate = AudioSettings.outputSampleRate;
        audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        if (sampleVolumeThreshold <= 0)
        {
            sampleVolumeThreshold = 0.1f;
        }
        Debug.Log($"Threshold volume: {sampleVolumeThreshold}");

        InitUi();

        StartRecording();
    }

    private void InitUi()
    {
        UIDocument uiDocument = FindObjectOfType<UIDocument>();
        startMeasurementButton = uiDocument.rootVisualElement.Q<Button>("startMeasurementButton");
        startMeasurementButton.RegisterCallback<ClickEvent>(_ => StartMeasurement());
        firstChannelAudioWaveForm = uiDocument.rootVisualElement.Q<VisualElement>("firstChannelAudioWaveForm");
    }

    private void Update()
    {
        PortAudioUtils.GetRecordedSamples(InputDeviceInfo, inputDeviceChannelIndex, micSampleBuffer);

        if (shouldMakeNoise
            && noiseStartInMillis > 0)
        {
            long millisSinceNoiseStart = GetUnixTimeMilliseconds() - noiseStartInMillis;
            if (millisSinceNoiseStart > 1000)
            {
                Debug.LogError($"Failed to record sample above threshold within 1 second. Aborting measurement. Max measured sample value {maxSampleValue}.");
                StopMeasurement();
            }

            for (int i = 0; i < micSampleBuffer.Length; i++)
            {
                if (micSampleBuffer[i] > maxSampleValue)
                {
                    maxSampleValue = micSampleBuffer[i];
                }

                if (micSampleBuffer[i] > sampleVolumeThreshold)
                {
                    StopMeasurement();
                    Debug.Log($"Recorded sample above threshold after {millisSinceNoiseStart} ms with host API {HostApiInfo.HostApi}");
                    shouldMakeNoise = false;
                    break;
                }
            }
        }

        if (firstChannelAudioWaveFormVisualization == null
            && !float.IsNaN(firstChannelAudioWaveForm.resolvedStyle.width)
            && !float.IsNaN(firstChannelAudioWaveForm.resolvedStyle.height))
        {
            firstChannelAudioWaveFormVisualization = new AudioWaveFormVisualization(gameObject, firstChannelAudioWaveForm);
        }

        if (firstChannelAudioWaveFormVisualization != null
            && micSampleBuffer != null
            && micSampleBuffer.Length > 0)
        {
            firstChannelAudioWaveFormVisualization.DrawWaveFormMinAndMaxValues(micSampleBuffer);
        }
    }

    private void StartMeasurement()
    {
        // Start AudioSource such that OnAudioFilterRead is called
        audioSource.loop = true;
        audioSource.Play();

        noiseStartInMillis = GetUnixTimeMilliseconds();
        maxSampleValue = 0;
        shouldMakeNoise = true;
    }

    private void StopMeasurement()
    {
        audioSource.Stop();
        noiseStartInMillis = 0;
        maxSampleValue = 0;
        shouldMakeNoise = false;
    }

    private void StartRecording()
    {
        if (!PortAudioUtils.HostApis.Contains(hostApi))
        {
            throw new Exception($"Selected host API '{hostApi}' not available. Select one of {string.Join(", ", PortAudioUtils.HostApis)}");
        }

        if (HostApiInfo == null)
        {
            throw new Exception("No host API found");
        }

        if (InputDeviceInfo == null)
        {
            throw new Exception($"No input device found on host api {HostApiInfo}");
        }

        int sampleRate = (int)InputDeviceInfo.DefaultSampleRate;

        Debug.Log($"Start recording with {InputDeviceInfo} and sample rate {sampleRate} Hz");

        PortAudioUtils.StartRecording(
            InputDeviceInfo,
            true,
            1,
            sampleRate);

        micSampleBuffer = new float[(int)InputDeviceInfo.DefaultSampleRate];
    }

    private void StopRecording()
    {
        PortAudioUtils.StopRecording(InputDeviceInfo);
    }

    public static long GetUnixTimeMilliseconds()
    {
        // See https://stackoverflow.com/questions/4016483/get-time-in-milliseconds-using-c-sharp
        return DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }

    private void OnAudioFilterRead(float[] data, int channelCount)
    {
        if (shouldMakeNoise)
        {
            FillBufferWithSineWave(data, channelCount, 440);
        }
    }

    private void FillBufferWithSineWave(float[] data, int channelCount, int frequency)
    {
        for (int sampleIndex = 0; sampleIndex < data.Length; sampleIndex += channelCount)
        {
            for (int channelIndex = 0; channelIndex < outputChannelCount; channelIndex++)
            {
                data[sampleIndex + channelIndex] = Mathf.Sin(2 * Mathf.PI * frequency * totalSampleIndex / onAudioFilterReadSampleRate);
            }
            data[sampleIndex] = Mathf.Sin(2 * Mathf.PI * frequency * totalSampleIndex / onAudioFilterReadSampleRate);
            totalSampleIndex++;
        }
    }
}
