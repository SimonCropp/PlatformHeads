/// <summary>
/// The handle an idle loop waits on.
/// </summary>
public class LoopWakeTests
{
    [Test]
    public async Task AChangeWakesTheLoop()
    {
        var host = new SessionHost<Counter>(new(0));
        using var wake = new LoopWake<Counter>(host);

        host.Mutate(_ => _ with { Value = 1 });

        await Assert.That(wake.Handle.WaitOne(0)).IsTrue();
        // Once: the handle resets as the loop takes the wake.
        await Assert.That(wake.Handle.WaitOne(0)).IsFalse();
    }

    [Test]
    public async Task NoChangeLeavesTheLoopIdle()
    {
        var host = new SessionHost<Counter>(new(0));
        using var wake = new LoopWake<Counter>(host);

        host.Mutate(_ => _);

        await Assert.That(wake.Handle.WaitOne(0)).IsFalse();
    }

    [Test]
    public async Task ACommandWakesTheLoop()
    {
        var host = new SessionHost<Counter>(new(0));
        using var wake = new LoopWake<Counter>(host);

        wake.Set();

        await Assert.That(wake.Handle.WaitOne(0)).IsTrue();
    }

    /// <summary>
    /// A poll finishing while the app shuts down changes the state after the loop has gone.
    /// </summary>
    [Test]
    public async Task AChangeAfterDisposeIsNoFailure()
    {
        var host = new SessionHost<Counter>(new(0));
        var wake = new LoopWake<Counter>(host);
        wake.Dispose();

        host.Mutate(_ => _ with { Value = 1 });
        wake.Set();

        await Assert.That(host.State.Value).IsEqualTo(1);
    }
}
