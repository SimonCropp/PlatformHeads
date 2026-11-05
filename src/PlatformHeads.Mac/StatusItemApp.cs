namespace PlatformHeads;

/// <summary>
/// The application around a <see cref="StatusItemTray{TIcon}" /> for an app whose only face is the
/// tray: an accessory, with no Dock icon, whose quits are answered by PlatformHeadsMac's
/// TerminateGuard, and with App Nap kept off. An app with a window library of its own has all of
/// that there, and uses none of this.
/// <para>
/// AppKit is driven from the main thread only, so the app's loop runs on the thread Main starts
/// on, with <see cref="Pump" /> as its wait.
/// </para>
/// </summary>
public static class StatusItemApp
{
    /// <summary>
    /// What a logout, restart or shutdown runs: what the app's clean exit would have saved. Called
    /// from inside <see cref="Pump" />, on the thread the loop runs on, and AppKit ends the process
    /// as it returns, so nothing after the pump runs. Static, since the library calls back through
    /// a plain function pointer, which carries no instance to find it on, and settable after
    /// <see cref="Start" />, since what it saves may be made after the tray.
    /// </summary>
    public static Action? SessionEnding { get; set; }

    /// <summary>
    /// Told what <see cref="SessionEnding" /> threw, which nothing may throw back into the library:
    /// it is inside AppKit's quit.
    /// </summary>
    public static Action<Exception>? SessionEndFailed { get; set; }

    [UnmanagedCallersOnly]
    static void SessionEnded()
    {
        try
        {
            SessionEnding?.Invoke();
        }
        catch (Exception exception)
        {
            SessionEndFailed?.Invoke(exception);
        }
    }

    /// <summary>
    /// The application as an accessory, the session's end heard, and App Nap kept off for
    /// <paramref name="napReason" />. Before <see cref="StatusItemTray{TIcon}.Open" />. Null on
    /// success, or why there is no library to start it in.
    /// </summary>
    /// <param name="library">The app's library, as <see cref="StatusItemLibrary.Use" /> takes it.</param>
    /// <param name="napReason">Why App Nap is kept off, which macOS shows to whoever asks.</param>
    public static unsafe string? Start(string library, string napReason)
    {
        try
        {
            StatusItemLibrary.Use(library);
            var version = StatusItemLibrary.Version();
            if (version != StatusItemLibrary.ExpectedVersion)
            {
                return $"The tray's native library is version {version}, and this was built for {StatusItemLibrary.ExpectedVersion}.";
            }

            // Before the start, which reads it: with none, a logout is refused and cancelled
            PhTray.AppOnSessionEnd(&SessionEnded);
            PhTray.AppStart(napReason);
            return null;
        }
        catch (Exception exception)
            when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            return exception.Message;
        }
    }

    /// <summary>
    /// AppKit's events dispatched, for a tenth of a second at most: the loop's wait, and the only
    /// way the menu opens at all. Returns at once once an item has been chosen, the icon clicked
    /// or a quit asked for.
    /// </summary>
    public static void Pump() =>
        PhTray.AppPump();

    /// <summary>
    /// AppKit was asked to quit other than by the session ending. With no Dock icon, that is a quit
    /// event sent to the app, such as AppleScript's, which the library refused and turned into
    /// this, for the loop to end the way the app's own exit does. A session's end is
    /// <see cref="SessionEnding" />.
    /// </summary>
    public static bool Quitting =>
        PhTray.AppQuitting() != 0;

    /// <summary>
    /// App Nap allowed again, for an app on its way out.
    /// </summary>
    public static void Stop() =>
        PhTray.AppStop();
}
