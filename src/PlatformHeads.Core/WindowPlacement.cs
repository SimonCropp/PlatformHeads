namespace PlatformHeads;

/// <summary>
/// Where the window was when it was last on screen, and whether it filled the screen, so the next
/// one opens the same way. It opened centred at its first size on every start, and an app that
/// runs at login and is shown and hidden all day was put back there every morning.
/// <para>
/// In whatever units the head that reported it counts in, and from its own origin: device pixels
/// from the top left on Windows and X11, points on macOS. Nothing but that head ever reads the
/// numbers back, so they need to mean the same thing only to it.
/// </para>
/// </summary>
/// <param name="X">The window's left edge when it is not maximised.</param>
/// <param name="Y">The window's edge on the axis its head counts from, when it is not maximised.</param>
/// <param name="Width">The window's width when it is not maximised.</param>
/// <param name="Height">The window's height when it is not maximised.</param>
/// <param name="Maximized">Whether it filled the screen. The bounds are then the ones it goes back
/// to on being restored, which is what makes maximise, close, open, restore land where it started.
/// A minimised window is saved as it was before, since one that opened minimised would look like
/// one that never opened.</param>
public readonly record struct WindowPlacement(int X, int Y, int Width, int Height, bool Maximized = false)
{
    /// <summary>
    /// Five numbers, which <see cref="Parse"/> reads back.
    /// </summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{X},{Y},{Width},{Height},{(Maximized ? 1 : 0)}");

    /// <summary>
    /// Null for anything that is not five numbers, or names a window with no size: the file it is
    /// kept in may be one a person can edit, and a window opened from a line that makes no sense
    /// opens as a new one does.
    /// </summary>
    public static WindowPlacement? Parse(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var parts = value.Split(',');
        if (parts.Length != 5)
        {
            return null;
        }

        var numbers = new int[5];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[index]))
            {
                return null;
            }
        }

        if (numbers[2] <= 0 ||
            numbers[3] <= 0)
        {
            return null;
        }

        return new(numbers[0], numbers[1], numbers[2], numbers[3], numbers[4] != 0);
    }
}
