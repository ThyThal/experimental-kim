using NUnit.Framework;

public class ChallengeTimerTests
{
    [Test]
    public void Succeeds_WhenCorrectTimeAccumulatesBeforeTimeout()
    {
        var timer = new ChallengeTimer(requiredHoldTime: 1f, timeout: 5f);
        timer.Start();

        // 0.5s correct, 0.5s wrong (pauses), 0.6s correct -> 1.1s held, 1.6s elapsed
        Assert.AreEqual(ChallengeTimer.State.InProgress, timer.Tick(0.5f, true));
        Assert.AreEqual(ChallengeTimer.State.InProgress, timer.Tick(0.5f, false));
        Assert.AreEqual(ChallengeTimer.State.Succeeded, timer.Tick(0.6f, true));
    }

    [Test]
    public void WrongTypeAndSilencePauseButDoNotReset()
    {
        var timer = new ChallengeTimer(requiredHoldTime: 1f, timeout: 10f);
        timer.Start();

        timer.Tick(0.6f, true);   // held 0.6
        timer.Tick(2f, false);    // paused, still 0.6
        Assert.AreEqual(0.6f, timer.Accumulated, 1e-4f);
        Assert.AreEqual(ChallengeTimer.State.Succeeded, timer.Tick(0.5f, true)); // held 1.1
    }

    [Test]
    public void Fails_WhenTimeoutReachedBeforeRequiredHold()
    {
        var timer = new ChallengeTimer(requiredHoldTime: 3f, timeout: 2f);
        timer.Start();

        Assert.AreEqual(ChallengeTimer.State.InProgress, timer.Tick(1f, true));
        Assert.AreEqual(ChallengeTimer.State.Failed, timer.Tick(1.5f, true));
    }

    [Test]
    public void SuccessWinsTie_WhenHoldMetOnTimeoutFrame()
    {
        var timer = new ChallengeTimer(requiredHoldTime: 2f, timeout: 2f);
        timer.Start();

        // Same frame reaches both required hold and timeout -> success.
        Assert.AreEqual(ChallengeTimer.State.Succeeded, timer.Tick(2f, true));
    }

    [Test]
    public void TickAfterResolution_IsNoOp()
    {
        var timer = new ChallengeTimer(requiredHoldTime: 1f, timeout: 5f);
        timer.Start();
        timer.Tick(1f, true); // Succeeded

        Assert.AreEqual(ChallengeTimer.State.Succeeded, timer.Tick(10f, false));
    }

    [Test]
    public void Tick_BeforeStart_IsIdle()
    {
        var timer = new ChallengeTimer(1f, 5f);
        Assert.AreEqual(ChallengeTimer.State.Idle, timer.Tick(1f, true));
    }
}
