/// <summary>
/// Any message for the thread ends a wait, so these run on a thread of their own that has no
/// window, where only the handle and the timeout can: on a test thread other windows send it
/// messages, and a wait cut short by one says nothing about the handle.
/// </summary>
public class MessageWaitTests
{
    [Test]
    public async Task WithNothingToWakeItTheWaitIsTheTimeout()
    {
        using var wake = new AutoResetEvent(false);

        var waited = OnAThreadWithNoWindow(() => MessageWait.For(wake, TimeSpan.FromMilliseconds(300)));

        await Assert.That(waited).IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(290));
    }

    [Test]
    public async Task ASetHandleEndsTheWait()
    {
        using var wake = new AutoResetEvent(true);

        var waited = OnAThreadWithNoWindow(() => MessageWait.For(wake, TimeSpan.FromSeconds(5)));

        await Assert.That(waited).IsLessThan(TimeSpan.FromSeconds(2));
        // Reset by the wait, so the next one idles.
        await Assert.That(wake.WaitOne(0)).IsFalse();
    }

    [Test]
    public async Task AHandleSetDuringTheWaitEndsIt()
    {
        using var wake = new AutoResetEvent(false);
        using var timer = new System.Threading.Timer(_ => wake.Set(), null, 200, Timeout.Infinite);

        var waited = OnAThreadWithNoWindow(() => MessageWait.For(wake, TimeSpan.FromSeconds(5)));

        await Assert.That(waited).IsLessThan(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task WithNoHandleTheWaitIsTheTimeout()
    {
        var waited = OnAThreadWithNoWindow(() => MessageWait.For(null, TimeSpan.FromMilliseconds(300)));

        await Assert.That(waited).IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(290));
    }

    static TimeSpan OnAThreadWithNoWindow(Action wait)
    {
        var waited = TimeSpan.Zero;
        var thread = new Thread(() =>
        {
            var stopwatch = Stopwatch.StartNew();
            wait();
            waited = stopwatch.Elapsed;
        });
        thread.Start();
        thread.Join();
        return waited;
    }
}
