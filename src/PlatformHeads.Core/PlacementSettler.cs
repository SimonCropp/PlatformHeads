namespace PlatformHeads;

/// <summary>
/// Decides when a native window has settled, from where the library says it is on every poll. A
/// library reports where the window is rather than when it stopped moving, as raylib has no event
/// for the end of a drag, and each placement reported is a save: passed on as it came, a drag across
/// the screen would write the settings sixty times a second.
/// <para>
/// A placement is passed on once it has held for <see cref="Settle"/>, or at once when the window is
/// hidden, which is how it is left every time. The first one seen is where the window opened, which
/// is where it was left or a first start, and neither is news.
/// </para>
/// </summary>
public sealed class PlacementSettler
{
    /// <summary>
    /// How long a placement has to hold before it is passed on.
    /// </summary>
    public static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(500);

    WindowPlacement? seen;
    TimeSpan changedAt;
    bool pending;
    bool hidden;

    /// <param name="current">Where the window is, or null where the library does not know, as
    /// before the window is first shown.</param>
    /// <param name="now">Time on any clock that only goes forward.</param>
    /// <returns>The placement to save, or null when there is none yet.</returns>
    public WindowPlacement? Poll(WindowPlacement? current, TimeSpan now)
    {
        if (current is not null &&
            current != seen)
        {
            pending = seen is not null;
            seen = current;
            changedAt = now;
        }

        if (!pending ||
            (!hidden && now - changedAt < Settle))
        {
            hidden = false;
            return null;
        }

        pending = false;
        hidden = false;
        return seen;
    }

    /// <summary>
    /// The window is being hidden, so the next poll passes on a placement that has not settled.
    /// </summary>
    public void Hidden() =>
        hidden = true;
}
