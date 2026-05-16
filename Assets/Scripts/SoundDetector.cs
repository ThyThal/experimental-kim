using System;
using UnityEngine;

public class SoundDetector : MonoBehaviour
{
    [SerializeField] private MicrophoneManager _microphoneManager;
    [SerializeField] private float _threshold = 0.005f;
    [SerializeField] private float _silenceDelay = 0.5f;

    public event Action OnSoundStart;
    public event Action OnSoundStop;
    public bool IsActive { get; private set; }

    private float _silenceTimer;

    private void Awake()
    {
        if (_microphoneManager == null)
            Debug.LogError("[SoundDetector] microphoneManager is not assigned.", this);
    }

    private void OnDisable()
    {
        if (IsActive)
        {
            IsActive = false;
            OnSoundStop?.Invoke();
        }
        _silenceTimer = 0f;
    }

    private void Update()
    {
        if (!_microphoneManager.IsReady) return;

        if (_microphoneManager.Amplitude > _threshold)
        {
            _silenceTimer = 0f;

            if (IsActive) return;
            IsActive = true;
            OnSoundStart?.Invoke();
        }
        else if (IsActive)
        {
            _silenceTimer += Time.deltaTime;

            if (!(_silenceTimer >= _silenceDelay)) return;
            IsActive = false;
            OnSoundStop?.Invoke();
        }
    }
}
