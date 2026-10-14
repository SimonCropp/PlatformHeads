namespace PlatformHeads;

/// <summary>
/// The handle an idle loop waits on (<see cref="IHeadWindow{TScreen, TInput}.Wait"/>), set whenever
/// the state changes and whenever <see cref="Set"/> is called for a command, so an idle loop draws
/// a poll's result or answers a socket at once rather than at the end of its idle.
/// </summary>
public sealed class LoopWake<TState> : IDisposable
    where TState : class
{
    readonly SessionHost<TState> source;
    readonly AutoResetEvent wake = new(false);

    public LoopWake(SessionHost<TState> host)
    {
        source = host;
        host.Changed += Set;
    }

    public WaitHandle Handle => wake;

    /// <summary>
    /// Wakes the loop, as for a command queued off its thread.
    /// <para>
    /// Harmless once disposed. A change raised on another thread can have taken the handler before
    /// <see cref="Dispose"/> removed it and call it after, and a poll finishing while the app shuts
    /// down is not the poll's failure.
    /// </para>
    /// </summary>
    public void Set()
    {
        try
        {
            wake.Set();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        source.Changed -= Set;
        wake.Dispose();
    }
}
