namespace PlatformHeads;

/// <summary>
/// The screen for a state, built once per state rather than once per frame, and once more each
/// time the clock ticks over where the screen shows the time.
/// <para>
/// A frame loop presents sixty times a second, and a state changes when something happens, which
/// for a window somebody is reading is now and then. A state is immutable and only ever replaced,
/// so the same reference is the same screen. Building it again every frame was megabytes of
/// garbage a second from a window nobody was touching: DiffEngine's viewer built two megabytes a
/// frame for a queue of 2,000 entries.
/// </para>
/// <para>
/// Handing back the same screen is also how a head learns that nothing changed, without comparing
/// two of them: a screen holds lists, so record equality is by reference and a head cannot tell an
/// identical rebuild from a change. Both apps' WinForms heads and native payloads stop at the
/// reference.
/// </para>
/// <para>
/// One thread's. The loop and a head's modal loop both run on the thread that owns the window.
/// </para>
/// </summary>
public sealed class ScreenCache<TState, TScreen>
    where TState : class
    where TScreen : class
{
    readonly Func<TState, TScreen>? timeless;
    readonly Func<TState, DateTimeOffset, TScreen>? timed;
    readonly Func<TState, TScreen, TimeSpan?>? tick;
    TState? builtState;
    DateTimeOffset builtAt;
    TScreen? screen;

    /// <summary>
    /// For a screen that does not show the time, which only another state rebuilds.
    /// </summary>
    public ScreenCache(Func<TState, TScreen> build) =>
        timeless = build;

    /// <param name="build">Builds the screen for a state at a time.</param>
    /// <param name="tick">How often the clock alone rebuilds the screen built from a state, or null
    /// when it never does, as for a window nobody can see: no one sees the screen, and BuildMonitor
    /// with a large account rebuilt it four times a second for nobody, about 22 ms of processor and
    /// 15 MB of garbage a second. On whole ticks of the clock, so a time in whole seconds moves with
    /// the clock rather than up to a second behind it.</param>
    public ScreenCache(Func<TState, DateTimeOffset, TScreen> build, Func<TState, TScreen, TimeSpan?> tick)
    {
        timed = build;
        this.tick = tick;
    }

    public TScreen Get(TState state) =>
        Get(state, DateTimeOffset.UtcNow, out _);

    public TScreen Get(TState state, DateTimeOffset now) =>
        Get(state, now, out _);

    /// <summary>
    /// The screen for this frame. <paramref name="rebuilt"/> is false when the previous instance
    /// was returned, so a caller can skip pushing it anywhere.
    /// </summary>
    public TScreen Get(TState state, DateTimeOffset now, out bool rebuilt)
    {
        rebuilt = screen is null ||
                  !ReferenceEquals(state, builtState) ||
                  Ticked(now);
        if (rebuilt)
        {
            screen = Build(state, now);
            builtState = state;
            builtAt = now;
        }

        return screen!;
    }

    /// <summary>
    /// How long from <paramref name="now"/> until the clock alone rebuilds the last screen, which is
    /// as long as a loop can idle without a time on screen falling behind. Null when the clock never
    /// will.
    /// </summary>
    public TimeSpan? UntilTick(DateTimeOffset now)
    {
        if (Tick() is not { } length)
        {
            return null;
        }

        var next = (builtAt.Ticks / length.Ticks + 1) * length.Ticks;
        if (next <= now.Ticks)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromTicks(next - now.Ticks);
    }

    TScreen Build(TState state, DateTimeOffset now)
    {
        if (timed is null)
        {
            return timeless!(state);
        }

        return timed(state, now);
    }

    bool Ticked(DateTimeOffset now)
    {
        if (Tick() is not { } length)
        {
            return false;
        }

        return now.Ticks / length.Ticks != builtAt.Ticks / length.Ticks;
    }

    TimeSpan? Tick()
    {
        if (tick is null ||
            screen is null ||
            builtState is null ||
            tick(builtState, screen) is not { } length)
        {
            return null;
        }

        if (length <= TimeSpan.Zero)
        {
            throw new InvalidOperationException($"A tick has to be longer than zero, and was {length}.");
        }

        return length;
    }
}
