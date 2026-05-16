using UnityEngine;

public class MicrophoneManager : MonoBehaviour
{
    [SerializeField] private int _sampleWindow = 128;

    private AudioClip _micClip;
    private float[] _samples;

    public float Amplitude { get; private set; }
    public float Peak { get; private set; }
    public bool IsReady { get; private set; }

    private void Awake()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[MicrophoneManager] No microphone detected.");
            return;
        }

        _micClip = Microphone.Start(null, true, 2, AudioSettings.outputSampleRate);
        _samples = new float[_sampleWindow];
        IsReady = true;
        Debug.Log($"[MicrophoneManager] Using device: {Microphone.devices[0]}");
    }

    private void OnDestroy()
    {
        if (Microphone.IsRecording(null))
            Microphone.End(null);
    }

    private void Update()
    {
        if (!IsReady) return;

        var micPosition = Microphone.GetPosition(null);
        if (micPosition < _sampleWindow) return;

        _micClip.GetData(_samples, micPosition - _sampleWindow);

        var sum = 0f;
        var peak = 0f;
        for (var i = 0; i < _sampleWindow; i++)
        {
            var abs = Mathf.Abs(_samples[i]);
            sum += abs * abs;
            if (abs > peak) peak = abs;
        }

        Amplitude = Mathf.Sqrt(sum / _sampleWindow);
        Peak = peak;
    }
}
