namespace PlatformHeads;

/// <summary>
/// How long a frame loop waits between frames. A loop that presented sixty times a second whether
/// or not anything had changed was, with the window open, nearly all of BuildMonitor's processor
/// while nothing was building.
/// </summary>
public static class Pacing
{
    /// <summary>
    /// One frame at sixty a second, which is as often as a loop runs.
    /// </summary>
    public static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(16);

    /// <summary>
    /// The longest a loop idles with nothing to wake it. Everything that changes the state or sends
    /// a command sets the wake, so this only bounds what a wake that never came would cost.
    /// </summary>
    public static readonly TimeSpan LongestIdle = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long a loop sleeps between frames when nobody can see the window, for a head that cannot
    /// <see cref="IHeadWindow{TScreen, TInput}.Wait">idle</see>. Nothing is drawn, so the only things
    /// to keep up with are a tray and a restore, neither of which needs sixty a second.
    /// </summary>
    public static readonly TimeSpan HiddenFrame = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// How long a loop may idle after presenting a frame: one frame while something has to be tried
    /// again on the next, until the clock would rebuild the screen, and otherwise until something
    /// wakes it.
    /// </summary>
    /// <param name="untilTick">From <see cref="ScreenCache{TState, TScreen}.UntilTick"/>.</param>
    /// <param name="retrying">Whether something has to be tried again on the next frame with no
    /// change of state to wake the loop, as a copy the clipboard refused (<see cref="ClipboardOffer.Again"/>).</param>
    public static TimeSpan Idle(TimeSpan? untilTick, bool retrying)
    {
        if (retrying)
        {
            return Frame;
        }

        if (untilTick is { } tick &&
            tick < LongestIdle)
        {
            return tick;
        }

        return LongestIdle;
    }
}
