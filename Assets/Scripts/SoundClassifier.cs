using TMPro;
using UnityEngine;

public class SoundClassifier : MonoBehaviour
{
    [SerializeField] private MicrophoneManager _microphoneManager;
    [SerializeField] private SoundDetector _soundDetector;
    [SerializeField] private TMP_Text _detectionLabel;

    [Header("Clap Detection")]
    [SerializeField] private float _clapCrestThreshold = 15f;

    [Header("Voice Levels")]
    [SerializeField] private float _screamAmplitudeThreshold = 0.03f;
    [SerializeField] private float _talkAmplitudeThreshold = 0.005f;

    private float _burstStartTime;
    private float _peakDuringBurst;
    private float _amplitudeSum;
    private int _amplitudeSampleCount;

    private void Awake()
    {
        if (_microphoneManager == null)
            Debug.LogError("[SoundClassifier] _microphoneManager is not assigned.", this);
        if (_soundDetector == null)
            Debug.LogError("[SoundClassifier] _soundDetector is not assigned.", this);
    }

    private void OnEnable()
    {
        _soundDetector.OnSoundStart += HandleSoundStart;
        _soundDetector.OnSoundStop += HandleSoundStop;
    }

    private void OnDisable()
    {
        _soundDetector.OnSoundStart -= HandleSoundStart;
        _soundDetector.OnSoundStop -= HandleSoundStop;
    }

    private void Update()
    {
        if (!_soundDetector.IsActive) return;

        if (_microphoneManager.Peak > _peakDuringBurst)
            _peakDuringBurst = _microphoneManager.Peak;

        _amplitudeSum += _microphoneManager.Amplitude;
        _amplitudeSampleCount++;
    }

    private void HandleSoundStart()
    {
        _burstStartTime = Time.time;
        _peakDuringBurst = 0f;
        _amplitudeSum = 0f;
        _amplitudeSampleCount = 0;

        if (_detectionLabel != null)
            _detectionLabel.text = "...";
    }

    private void HandleSoundStop()
    {
        float duration = Time.time - _burstStartTime;
        float avgAmplitude = _amplitudeSampleCount > 0 ? _amplitudeSum / _amplitudeSampleCount : 0f;
        float crestFactor = avgAmplitude > 0f ? _peakDuringBurst / avgAmplitude : 0f;

        string soundType = Classify(crestFactor, avgAmplitude);
        Debug.Log($"[SoundClassifier] {soundType} | duration: {duration:F2}s | crest: {crestFactor:F1} | amplitude: {avgAmplitude:F3}");

        if (_detectionLabel != null)
            _detectionLabel.text = soundType;
    }

    private string Classify(float crestFactor, float avgAmplitude)
    {
        if (crestFactor >= _clapCrestThreshold)
            return "Clap";

        if (avgAmplitude >= _screamAmplitudeThreshold) return "Scream";
        if (avgAmplitude >= _talkAmplitudeThreshold) return "Talk";
        return "Murmur";
    }
}
