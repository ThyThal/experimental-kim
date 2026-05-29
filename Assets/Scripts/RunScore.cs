using UnityEngine;

public enum EndingTier
{
    Good,
    Neutral,
    Bad
}

/// <summary>
/// Tracks failed challenges across a run. Successes are neutral — only timeouts
/// count. The accumulated failure tally maps to an ending tier; building the
/// actual ending screens is out of scope here.
/// </summary>
public class RunScore : MonoBehaviour
{
    [Tooltip("At most this many failures still counts as the Good ending.")]
    [SerializeField] private int _goodMaxFailures = 0;

    [Tooltip("At most this many failures counts as Neutral; more than this is Bad.")]
    [SerializeField] private int _neutralMaxFailures = 2;

    public int Failures { get; private set; }

    public void RegisterFailure()
    {
        Failures++;
        Debug.Log($"[RunScore] Challenge failed. Total failures: {Failures}", this);
    }

    public void ResetScore() => Failures = 0;

    public EndingTier GetEnding() => GetEnding(Failures, _goodMaxFailures, _neutralMaxFailures);

    /// <summary>Pure threshold mapping, exposed for testing.</summary>
    public static EndingTier GetEnding(int failures, int goodMaxFailures, int neutralMaxFailures)
    {
        if (failures <= goodMaxFailures) return EndingTier.Good;
        if (failures <= neutralMaxFailures) return EndingTier.Neutral;
        return EndingTier.Bad;
    }
}
