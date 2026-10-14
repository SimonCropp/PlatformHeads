namespace PlatformHeads;

/// <summary>
/// Waits on the calling thread's message queue and a handle together, which neither WinForms nor
/// <see cref="WaitHandle"/> can: a WaitOne on the UI thread leaves a click in the queue until the
/// timeout, and <see cref="Application.DoEvents"/> only drains what has already arrived.
/// </summary>
public static partial class MessageWait
{
    // QS_ALLINPUT: any message, including the paints and timers WinForms posts to itself.
    const uint allInput = 0x04FF;

    // MWMO_INPUTAVAILABLE: also wake for input already queued that an earlier peek saw, rather than
    // only for input arriving after the call.
    const uint inputAvailable = 0x0004;

    /// <summary>
    /// Returns once a message is waiting, <paramref name="wake"/> is set, or
    /// <paramref name="timeout"/> passes. A set <paramref name="wake"/> is reset by the wait. With
    /// no <paramref name="wake"/>, only a message or the timeout ends it.
    /// </summary>
    public static void For(WaitHandle? wake, TimeSpan timeout)
    {
        // Up rather than down, so a wait for the clock's next tick does not end just short of it and
        // spin through a few empty frames until it arrives.
        var milliseconds = (uint) Math.Ceiling(Math.Clamp(timeout.TotalMilliseconds, 0, int.MaxValue));
        if (wake is null)
        {
            var none = nint.Zero;
            MsgWaitForMultipleObjectsEx(0, ref none, milliseconds, allInput, inputAvailable);
            return;
        }

        var handle = wake.SafeWaitHandle.DangerousGetHandle();
        MsgWaitForMultipleObjectsEx(1, ref handle, milliseconds, allInput, inputAvailable);
        GC.KeepAlive(wake);
    }

    [LibraryImport("user32.dll")]
    private static partial uint MsgWaitForMultipleObjectsEx(uint count, ref nint handles, uint milliseconds, uint wakeMask, uint flags);
}
