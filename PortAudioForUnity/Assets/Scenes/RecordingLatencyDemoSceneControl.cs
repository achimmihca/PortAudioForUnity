using System;
using PortAudioForUnity;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(AudioSource))]
public class RecordingLatencyDemoSceneControl : MonoBehaviour
{
    public HostApi hostApi;
    public int inputDeviceChannelIndex;
    public float sampleVolumeThreshold;
    public bool usePortAudioToPlaySound = true;

    private AudioSource audioSource;

    // Measure time until first sample above threshold volume is recorded
    private bool shouldMakeNoise;
    private float maxSampleValue;
    private long noiseStartInMillis;
    private float[] micSampleBuffer;

    private int onAudioFilterReadSampleRate;

    private HostApiInfo HostApiInfo => PortAudioUtils.GetHostApiInfo(hostApi);
    private DeviceInfo OutputDeviceInfo => PortAudioUtils.GetDeviceInfo(HostApiInfo.DefaultOutputDeviceGlobalIndex);
    private DeviceInfo InputDeviceInfo => PortAudioUtils.GetDeviceInfo(HostApiInfo.DefaultInputDeviceGlobalIndex);

    private Button startMeasurementButton;
    private VisualElement firstChannelAudioWaveForm;
    private AudioWaveFormVisualization firstChannelAudioWaveFormVisualization;

    private SineToneGenerator portAudioSineToneGenerator;
    private SineToneGenerator unitySineToneGenerator;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.Play();

        onAudioFilterReadSampleRate = AudioSettings.outputSampleRate;
        if (sampleVolumeThreshold <= 0)
        {
            sampleVolumeThreshold = 0.1f;
        }
        Debug.Log($"Threshold volume: {sampleVolumeThreshold}");

        portAudioSineToneGenerator = new(440, (int)OutputDeviceInfo.DefaultSampleRate);
        unitySineToneGenerator = new(440, onAudioFilterReadSampleRate);

        InitUi();

        StartRecording();

        PortAudioUtils.StartPlayback(
            OutputDeviceInfo,
            OutputDeviceInfo.MaxOutputChannels,
            1,
            (int)OutputDeviceInfo.DefaultSampleRate,
            OnPortAudioReadSamples);
    }

    private void OnAudioFilterRead(float[] data, int channelCount)
    {
        if (usePortAudioToPlaySound)
        {
            return;
        }

        if (!shouldMakeNoise)
        {
            Array.Clear(data, 0, data.Length);
            return;
        }

        unitySineToneGenerator.FillBuffer(data, channelCount);
    }

    private void OnPortAudioReadSamples(float[] data)
    {
        if (!usePortAudioToPlaySound)
        {
            return;
        }

        if (!shouldMakeNoise)
        {
            Array.Clear(data, 0, data.Length);
            return;
        }

        portAudioSineToneGenerator.FillBuffer(data, OutputDeviceInfo.MaxOutputChannels);
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
                    string hostApiText = usePortAudioToPlaySound
                        ? $"host API {HostApiInfo.HostApi}"
                        : $"Unity API";
                    Debug.Log($"Recorded sample above threshold after {millisSinceNoiseStart} ms with {hostApiText}");
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
        noiseStartInMillis = GetUnixTimeMilliseconds();
        maxSampleValue = 0;
        shouldMakeNoise = true;
    }

    private void StopMeasurement()
    {
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
}
