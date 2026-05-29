/// <summary>
/// Pure (no-MonoBehaviour) timer for a specific-zone challenge. The player must
/// accumulate <see cref="_requiredHoldTime"/> seconds of correct-type input
/// before <see cref="_timeout"/> seconds of wall-clock time elapse.
///
/// Correct time is cumulative: wrong type or silence pauses the accumulator but
/// never resets it. Extracted from the MonoBehaviour so it can be unit-tested.
/// </summary>
public class ChallengeTimer
{
    public enum State { Idle, InProgress, Succeeded, Failed }

    private readonly float _requiredHoldTime;
    private readonly float _timeout;

    private float _accumulated;
    private float _elapsed;

    public ChallengeTimer(float requiredHoldTime, float timeout)
    {
        _requiredHoldTime = requiredHoldTime;
        _timeout = timeout;
    }

    public State Current { get; private set; } = State.Idle;
    public float Accumulated => _accumulated;
    public float Elapsed => _elapsed;

    public void Start()
    {
        _accumulated = 0f;
        _elapsed = 0f;
        Current = State.InProgress;
    }

    /// <summary>
    /// Advance the timer by <paramref name="dt"/> seconds. Pass true for
    /// <paramref name="correctTypeActive"/> when the required input type is
    /// currently detected. Returns the resulting state.
    /// </summary>
    public State Tick(float dt, bool correctTypeActive)
    {
        if (Current != State.InProgress) return Current;

        _elapsed += dt;
        if (correctTypeActive)
            _accumulated += dt;

        // Success wins ties: if the hold is met on the same frame the timeout
        // expires, the player completed the task in time.
        if (_accumulated >= _requiredHoldTime)
            Current = State.Succeeded;
        else if (_elapsed >= _timeout)
            Current = State.Failed;

        return Current;
    }
}
