using NUnit.Framework;

public class RunScoreTests
{
    [Test]
    public void GetEnding_GoodAtOrBelowGoodThreshold()
    {
        // goodMax=0, neutralMax=2
        Assert.AreEqual(EndingTier.Good, RunScore.GetEnding(0, 0, 2));
    }

    [Test]
    public void GetEnding_NeutralBetweenThresholds()
    {
        Assert.AreEqual(EndingTier.Neutral, RunScore.GetEnding(1, 0, 2));
        Assert.AreEqual(EndingTier.Neutral, RunScore.GetEnding(2, 0, 2));
    }

    [Test]
    public void GetEnding_BadAboveNeutralThreshold()
    {
        Assert.AreEqual(EndingTier.Bad, RunScore.GetEnding(3, 0, 2));
        Assert.AreEqual(EndingTier.Bad, RunScore.GetEnding(99, 0, 2));
    }
}
