using UnityEngine;

public enum EndingTier
{
    Good,
    Bad
}

/// <summary>
/// Tracks failed challenges across a run. Successes don't count — only timeouts.
/// Reaching <see cref="_failuresForBadEnding"/> failures yields the Bad ending;
/// anything below is the Good ending.
/// </summary>
public class RunScore : MonoBehaviour
{
    [Tooltip("Number of failed challenges that triggers the Bad ending (fewer = Good).")]
    [SerializeField] private int _failuresForBadEnding = 2;

    public int Failures { get; private set; }

    public void RegisterFailure()
    {
        Failures++;
        Debug.Log($"[RunScore] Challenge failed. Total failures: {Failures}", this);
    }

    public void ResetScore() => Failures = 0;

    public EndingTier GetEnding() => GetEnding(Failures, _failuresForBadEnding);

    /// <summary>Pure threshold mapping, exposed for testing.</summary>
    public static EndingTier GetEnding(int failures, int failuresForBadEnding) =>
        failures >= failuresForBadEnding ? EndingTier.Bad : EndingTier.Good;
}
