using NUnit.Framework;

public class RunScoreTests
{
    [Test]
    public void GetEnding_GoodBelowThreshold()
    {
        // failuresForBad = 2 -> 0 or 1 failures is still Good.
        Assert.AreEqual(EndingTier.Good, RunScore.GetEnding(0, 2));
        Assert.AreEqual(EndingTier.Good, RunScore.GetEnding(1, 2));
    }

    [Test]
    public void GetEnding_BadAtOrAboveThreshold()
    {
        Assert.AreEqual(EndingTier.Bad, RunScore.GetEnding(2, 2));
        Assert.AreEqual(EndingTier.Bad, RunScore.GetEnding(3, 2));
        Assert.AreEqual(EndingTier.Bad, RunScore.GetEnding(99, 2));
    }
}
