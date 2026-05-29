using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Coordinates the stop-listen-resume flow at each event. Watches the player's x;
/// when it reaches the next challenge midpoint, it freezes movement, runs a
/// cumulative-correct-time timer against a timeout, then releases the player.
/// Timeouts are recorded as failures; success and failure both resume. When the
/// player reaches the finish tile's midpoint, the run ends and the ending tier is
/// computed from accumulated failures (UI is out of scope — see <see cref="OnGameEnded"/>).
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

    [Tooltip("One animator controller per event type; the matching one is activated while its event runs.")]
    [SerializeField] private EventAnimatorController[] _eventAnimators;

    [Header("Ending scenes (must be in Build Settings)")]
    [SerializeField] private string _goodScene = "Good";
    [SerializeField] private string _badScene = "Bad";

    private int _nextChallengeIndex;
    private ChallengeTimer _timer;
    private ZoneInputType _requiredType;
    private EventAnimatorController _activeAnimators;
    private bool _ended;

    public bool InChallenge => _timer != null;
    public ZoneInputType RequiredType => _requiredType;
    public float HeldTime => _timer?.Accumulated ?? 0f;
    public float RequiredHoldTime => _requiredHoldTime;
    public bool Ended => _ended;

    /// <summary>Fired once when the player reaches the finish, with the resulting ending tier.</summary>
    public event Action<EndingTier> OnGameEnded;

    private void Awake()
    {
        if (_player == null) Debug.LogError("[ChallengeController] _player is not assigned.", this);
        if (_soundClassifier == null) Debug.LogError("[ChallengeController] _soundClassifier is not assigned.", this);
        if (_backgroundSpawner == null) Debug.LogError("[ChallengeController] _backgroundSpawner is not assigned.", this);
        if (_runScore == null) Debug.LogError("[ChallengeController] _runScore is not assigned.", this);
    }

    private void Update()
    {
        if (_ended) return;

        if (InChallenge)
        {
            TickChallenge();
            return;
        }

        if (TryStartChallenge()) return;

        CheckFinish();
    }

    /// <summary>Starts the next challenge if the player has reached its midpoint. Returns true if one started.</summary>
    private bool TryStartChallenge()
    {
        var challenges = _backgroundSpawner.Challenges;
        if (_nextChallengeIndex >= challenges.Count) return false;

        var next = challenges[_nextChallengeIndex];
        if (_player.transform.position.x < next.midpointX) return false;

        // Stop exactly on the midpoint and begin listening.
        var pos = _player.transform.position;
        pos.x = next.midpointX;
        _player.transform.position = pos;
        _player.CanMove = false;

        _requiredType = next.requiredType;
        _timer = new ChallengeTimer(_requiredHoldTime, _timeout);
        _timer.Start();

        _activeAnimators = FindAnimators(_requiredType);
        if (_activeAnimators != null)
            _activeAnimators.Activate();
        else
            Debug.LogWarning($"[ChallengeController] No EventAnimatorController matches {_requiredType} — its animations/audio won't play. Check the Event Animators array and each controller's Event Type.", this);

        Debug.Log($"[ChallengeController] Challenge {_nextChallengeIndex} started — hold {_requiredType} for {_requiredHoldTime:F1}s ({(_activeAnimators != null ? "animator matched" : "NO animator")}).", this);
        return true;
    }

    private EventAnimatorController FindAnimators(ZoneInputType type)
    {
        if (_eventAnimators == null) return null;
        foreach (var c in _eventAnimators)
            if (c != null && c.EventType == type) return c;
        return null;
    }

    private void TickChallenge()
    {
        bool correct = _soundClassifier.IsActive && _soundClassifier.CurrentType == _requiredType;
        var state = _timer.Tick(Time.deltaTime, correct);

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

        _activeAnimators?.Deactivate();
        _activeAnimators = null;

        _timer = null;
        _nextChallengeIndex++;
        _player.CanMove = true;
    }

    /// <summary>Ends the run once every challenge is done and the player reaches the finish midpoint.</summary>
    private void CheckFinish()
    {
        if (!_backgroundSpawner.HasFinish) return;
        if (_nextChallengeIndex < _backgroundSpawner.Challenges.Count) return; // events still ahead
        if (_player.transform.position.x < _backgroundSpawner.FinishX) return;

        var pos = _player.transform.position;
        pos.x = _backgroundSpawner.FinishX;
        _player.transform.position = pos;
        _player.CanMove = false;
        _ended = true;

        EndingTier ending = _runScore.GetEnding();
        Debug.Log($"[ChallengeController] Reached finish — game over. Failures={_runScore.Failures} ending={ending}.", this);
        OnGameEnded?.Invoke(ending);

        SceneManager.LoadScene(ending == EndingTier.Bad ? _badScene : _goodScene);
    }
}
