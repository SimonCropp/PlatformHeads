/// <summary>
/// Sent SIGCONT, whose default does nothing to a process that is running, since a test sent the
/// SIGTERM or SIGHUP it listens for by default would be ended by it as the app is. Not on Windows,
/// where it registers nothing. One at a time, because a signal goes to every test's handler.
/// </summary>
[RunOn(TUnit.Core.Enums.OS.Linux | TUnit.Core.Enums.OS.MacOs)]
[NotInParallel]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public class SessionEndSignalsTests
{
    static readonly TimeSpan wait = TimeSpan.FromSeconds(10);

    [Test]
    public async Task TheSessionEndingIsHeard()
    {
        var heard = new TaskCompletionSource();
        using var signals = new SessionEndSignals(heard.SetResult, signals: [PosixSignal.SIGCONT]);

        Send();

        await heard.Task.WaitAsync(wait);
    }

    /// <summary>
    /// systemd sends SIGHUP straight after SIGTERM, and an app saving twice, or starting again on
    /// what it had half written, is worse off than one that saved once.
    /// </summary>
    [Test]
    public async Task TheEndingRunsOnce()
    {
        var heard = 0;
        var first = new TaskCompletionSource();
        using var signals = new SessionEndSignals(
            () =>
            {
                Interlocked.Increment(ref heard);
                first.TrySetResult();
            },
            signals: [PosixSignal.SIGCONT]);

        Send();
        await first.Task.WaitAsync(wait);
        Send();
        // The second has nothing to show for itself, so it is given time to have been heard
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        await Assert.That(heard).IsEqualTo(1);
    }

    /// <summary>
    /// Told, and not thrown out of the handler, which ends the process with nothing logged.
    /// </summary>
    [Test]
    public async Task WhatTheEndingThrowsIsHandedOver()
    {
        var failed = new TaskCompletionSource<Exception>();
        using var signals = new SessionEndSignals(
            () => throw new InvalidOperationException("saving failed"),
            _ => failed.SetResult(_),
            [PosixSignal.SIGCONT]);

        Send();

        await Assert.That(await failed.Task.WaitAsync(wait)).IsTypeOf<InvalidOperationException>();
    }

    [Test]
    public async Task NothingIsHeardOnceDisposed()
    {
        var heard = 0;
        var signals = new SessionEndSignals(() => Interlocked.Increment(ref heard), signals: [PosixSignal.SIGCONT]);
        signals.Dispose();

        Send();
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        await Assert.That(heard).IsEqualTo(0);
    }

    static void Send()
    {
        using var kill = Process.Start("kill", ["-CONT", Environment.ProcessId.ToString(CultureInfo.InvariantCulture)]);
        kill.WaitForExit();
    }
}
