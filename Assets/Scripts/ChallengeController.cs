using UnityEngine;

/// <summary>
/// Coordinates the stop-listen-resume flow at each specific zone. Watches the
/// player's x; when it reaches the next challenge midpoint, it freezes movement,
/// runs a cumulative-correct-time timer against a timeout, then releases the
/// player. Timeouts are recorded as failures; success and failure both resume.
/// </summary>
public class ChallengeController : MonoBehaviour
{
    [SerializeField] private PlayerController _player;
    [SerializeField] private SoundClassifier _soundClassifier;
    [SerializeField] private BackgroundSpawner _backgroundSpawner;
    [SerializeField] private RunScore _runScore;

    [Tooltip("Seconds of correct-type input needed to complete a challenge.")]
    [SerializeField] private float _requiredHoldTime = 2f;
    [Tooltip("Seconds allowed before the challenge fails (from when the player stops).")]
    [SerializeField] private float _timeout = 6f;

    private int _nextChallengeIndex;
    private ChallengeTimer _timer;
    private ZoneInputType _requiredType;
    private float _lastDiagLogTime;
    private float _lastWaitLogTime;

    public bool InChallenge => _timer != null;
    public ZoneInputType RequiredType => _requiredType;
    public float HeldTime => _timer?.Accumulated ?? 0f;
    public float RequiredHoldTime => _requiredHoldTime;

    private void Awake()
    {
        if (_player == null) Debug.LogError("[ChallengeController] _player is not assigned.", this);
        if (_soundClassifier == null) Debug.LogError("[ChallengeController] _soundClassifier is not assigned.", this);
        if (_backgroundSpawner == null) Debug.LogError("[ChallengeController] _backgroundSpawner is not assigned.", this);
        if (_runScore == null) Debug.LogError("[ChallengeController] _runScore is not assigned.", this);
    }

    private void Update()
    {
        if (InChallenge)
            TickChallenge();
        else
            TryStartChallenge();
    }

    private void TryStartChallenge()
    {
        var challenges = _backgroundSpawner.Challenges;

        // DIAGNOSTIC (temporary): once/sec, show why no challenge has started.
        if (Time.time - _lastWaitLogTime >= 1f)
        {
            _lastWaitLogTime = Time.time;
            float nextMid = _nextChallengeIndex < challenges.Count
                ? challenges[_nextChallengeIndex].midpointX : float.NaN;
            Debug.Log($"[ChallengeController] waiting — playerX={_player.transform.position.x:F1} " +
                      $"challenges={challenges.Count} nextIdx={_nextChallengeIndex} nextMidpoint={nextMid:F1}", this);
        }

        if (_nextChallengeIndex >= challenges.Count) return;

        var next = challenges[_nextChallengeIndex];
        if (_player.transform.position.x < next.midpointX) return;

        // Stop exactly on the midpoint and begin listening.
        var pos = _player.transform.position;
        pos.x = next.midpointX;
        _player.transform.position = pos;
        _player.CanMove = false;

        _requiredType = next.requiredType;
        _timer = new ChallengeTimer(_requiredHoldTime, _timeout);
        _timer.Start();
        Debug.Log($"[ChallengeController] Challenge {_nextChallengeIndex} started — hold {_requiredType} for {_requiredHoldTime:F1}s.", this);
    }

    private void TickChallenge()
    {
        bool correct = _soundClassifier.IsActive && _soundClassifier.CurrentType == _requiredType;
        var state = _timer.Tick(Time.deltaTime, correct);

        // DIAGNOSTIC (temporary): shows whether correct-type input is registering.
        if (Time.time - _lastDiagLogTime >= 0.5f)
        {
            _lastDiagLogTime = Time.time;
            Debug.Log($"[ChallengeController] need={_requiredType} got={_soundClassifier.CurrentType} " +
                      $"active={_soundClassifier.IsActive} correct={correct} " +
                      $"held={_timer.Accumulated:F2}/{_requiredHoldTime:F1} elapsed={_timer.Elapsed:F2}/{_timeout:F1}", this);
        }

        if (state == ChallengeTimer.State.InProgress) return;

        if (state == ChallengeTimer.State.Failed)
        {
            Debug.Log($"[ChallengeController] Challenge {_nextChallengeIndex} timed out.", this);
            _runScore.RegisterFailure();
        }
        else
        {
            Debug.Log($"[ChallengeController] Challenge {_nextChallengeIndex} completed.", this);
        }

        _timer = null;
        _nextChallengeIndex++;
        _player.CanMove = true;
    }
}
