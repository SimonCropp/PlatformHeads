namespace PlatformHeads;

/// <summary>
/// Hears that the Windows session is ending, for an app with no <see cref="Form"/> to hear it, as
/// a tray that is only a notify icon has none. <see cref="HeadForm.SessionEnding"/> is the same for
/// an app that has one.
/// <para>
/// Nothing else would. A logoff or a shutdown sends every top level window WM_QUERYENDSESSION and
/// then WM_ENDSESSION, and Windows may end the process as soon as those are answered. A form turns
/// them into a close; a notify icon's window agrees to the query and does nothing more, so
/// <see cref="Application.Run()"/> never returns, nothing after it runs, and whatever a clean exit
/// would have saved went with the process.
/// </para>
/// <para>
/// A window of its own, because the two messages are sent rather than posted, which a message
/// filter never sees. Top level, because a message only window is not sent them. Never shown.
/// </para>
/// <para>
/// On WM_ENDSESSION rather than on the query before it. Another application can still refuse the
/// query, and a session that then carries on would have had its app save and wind down for
/// nothing.
/// </para>
/// </summary>
public sealed class SessionEndWindow :
    NativeWindow,
    IDisposable
{
    const int endSession = 0x0016;

    readonly Action ending;
    readonly Action<Exception>? failed;

    /// <param name="ending">
    /// Run inside the message, on the thread this is created on, so whatever it does is done by
    /// the time the message is answered: there may be no time after that.
    /// </param>
    /// <param name="failed">
    /// Told what <paramref name="ending"/> threw. Nothing more is done with it: thrown out of the
    /// window procedure it reaches WinForms' exception dialog, which during a logoff is a window
    /// holding the session open with nobody left to answer it.
    /// </param>
    public SessionEndWindow(Action ending, Action<Exception>? failed = null)
    {
        this.ending = ending;
        this.failed = failed;
        CreateHandle(new());
    }

    protected override void WndProc(ref Message message)
    {
        // WParam is zero when the session is not ending after all, which everything that agreed to
        // the query is told once something else has refused it
        if (message.Msg == endSession &&
            message.WParam != IntPtr.Zero)
        {
            try
            {
                ending();
            }
            catch (Exception exception)
            {
                failed?.Invoke(exception);
            }
        }

        base.WndProc(ref message);
    }

    public void Dispose() =>
        DestroyHandle();
}
