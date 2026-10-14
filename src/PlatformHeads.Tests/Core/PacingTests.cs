/// <summary>
/// How long a loop idles after a frame.
/// </summary>
public class PacingTests
{
    [Test]
    public async Task UntilTheTick()
    {
        var tick = TimeSpan.FromMilliseconds(750);

        await Assert.That(Pacing.Idle(tick, retrying: false)).IsEqualTo(tick);
    }

    /// <summary>
    /// A screen the clock never rebuilds leaves the loop to be woken, with the longest idle as the
    /// bound on a wake that never came.
    /// </summary>
    [Test]
    public async Task WithNoTickUntilWoken() =>
        await Assert.That(Pacing.Idle(null, retrying: false)).IsEqualTo(Pacing.LongestIdle);

    [Test]
    public async Task NeverLongerThanTheLongestIdle() =>
        await Assert.That(Pacing.Idle(TimeSpan.FromMinutes(1), retrying: false)).IsEqualTo(Pacing.LongestIdle);

    /// <summary>
    /// A copy the clipboard refused is offered again on the next frame, and a refusal changes no
    /// state to wake the loop with.
    /// </summary>
    [Test]
    public async Task AFrameWhileRetrying() =>
        await Assert.That(Pacing.Idle(null, retrying: true)).IsEqualTo(Pacing.Frame);
}
