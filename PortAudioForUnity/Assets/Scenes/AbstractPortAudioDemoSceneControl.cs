using System;
using System.Collections;
using System.Linq;
using PortAudioForUnity;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

public abstract class AbstractPortAudioDemoSceneControl : MonoBehaviour
{
    public bool overwriteHostApi;
    public HostApi hostApi = HostApi.WASAPI;

    protected virtual HostApiInfo HostApiInfo => PortAudioUtils.GetHostApiInfo(MicrophoneAdapter.GetHostApi());
    protected virtual DeviceInfo InputDeviceInfo => PortAudioUtils.GetDeviceInfo(HostApiInfo.DefaultInputDeviceGlobalIndex);
    protected virtual DeviceInfo OutputDeviceInfo => PortAudioUtils.GetDeviceInfo(HostApiInfo.DefaultOutputDeviceGlobalIndex);

    protected float lastUpdateTimeInSeconds;
    protected VisualElement firstChannelAudioWaveForm;
    protected VisualElement secondChannelAudioWaveForm;
    protected AudioWaveFormVisualization firstChannelAudioWaveFormVisualization;
    protected AudioWaveFormVisualization secondChannelAudioWaveFormVisualization;

    protected virtual void Start()
    {
        Debug.Log("Start");

        string hostApiNameCsv = string.Join(", ", PortAudioUtils.HostApiInfos
            .Select(hostApiInfo => $"'{hostApiInfo.Name}' ({hostApiInfo.HostApi})"));
        Debug.Log($"Available host APIs: {hostApiNameCsv}");
        Debug.Log($"Default host API: {PortAudioUtils.DefaultHostApiInfo.HostApi}");

        Debug.Log(">>> Host API infos <<<");
        foreach (HostApiInfo hostApiInfo in PortAudioUtils.HostApiInfos)
        {
            Debug.Log($"Host API: {hostApiInfo.HostApi}," +
                      $" name: {hostApiInfo.Name}," +
                      $" device count: {hostApiInfo.DeviceCount}," +
                      $" default global input device index: {hostApiInfo.DefaultInputDeviceGlobalIndex}," +
                      $" default global output device index: {hostApiInfo.DefaultOutputDeviceGlobalIndex}");
        }

        Debug.Log(">>> Device infos <<<");
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

        Debug.Log($"Input device: {InputDeviceInfo?.Name}");
        Debug.Log($"Output device: {OutputDeviceInfo?.Name}");

        InitUi();
    }

    protected virtual void InitUi()
    {
        UIDocument uiDocument = FindObjectOfType<UIDocument>();
        firstChannelAudioWaveForm = uiDocument.rootVisualElement.Q<VisualElement>("firstChannelAudioWaveForm");
        secondChannelAudioWaveForm = uiDocument.rootVisualElement.Q<VisualElement>("secondChannelAudioWaveForm");
    }

    protected virtual void OnDestroy()
    {
        firstChannelAudioWaveFormVisualization?.Dispose();
        secondChannelAudioWaveFormVisualization?.Dispose();
    }

    protected static IEnumerator ExecuteAfterDelayInSeconds(float delayInSeconds, Action action)
    {
        yield return new WaitForSeconds(delayInSeconds);
        action();
    }
}
