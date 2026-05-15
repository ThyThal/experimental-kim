using UnityEngine;

public class MicrophoneManager : MonoBehaviour
{
    [SerializeField] private int sampleWindow = 128;

    private AudioClip _micClip;

    public float Amplitude { get; private set; }
    public bool IsReady { get; private set; }

    private void Awake()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[MicrophoneManager] No microphone detected.");
            return;
        }

        _micClip = Microphone.Start(null, true, 1, AudioSettings.outputSampleRate);
        IsReady = true;
    }

    private void OnDestroy()
    {
        if (Microphone.IsRecording(null))
            Microphone.End(null);
    }

    private void Update()
    {
        if (!IsReady) return;

        int micPosition = Microphone.GetPosition(null);
        if (micPosition < sampleWindow) return;

        float[] samples = new float[sampleWindow];
        _micClip.GetData(samples, micPosition - sampleWindow);

        float sum = 0f;
        for (int i = 0; i < sampleWindow; i++)
            sum += samples[i] * samples[i];

        Amplitude = Mathf.Sqrt(sum / sampleWindow);
    }
}
