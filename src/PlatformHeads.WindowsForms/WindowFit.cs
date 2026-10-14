namespace PlatformHeads;

/// <summary>
/// Where a window opens, as functions of the displays there are, so they can be held to cases
/// without a window or a monitor arrangement to make them with.
/// </summary>
public static class WindowFit
{
    /// <summary>
    /// Smaller than anything a person could have left a window at, so not something a window
    /// reported.
    /// </summary>
    public static readonly Size SmallestRestored = new(200, 150);

    /// <summary>
    /// How far past the top of a display a title bar may be and still be reachable: more than the
    /// invisible border a snapped window hangs over by, less than the bar itself.
    /// </summary>
    public const int EdgeSlack = 16;

    /// <summary>
    /// Where a remembered window may open, or null when it should open as a new one does.
    /// <para>
    /// The displays are not the ones it was remembered on often enough to matter: a laptop taken
    /// off its dock remembers a window on a monitor that is no longer there, and opening it there
    /// is an app that looks as if it did not start. So it opens where it was only when most of it,
    /// and its title bar, are on a display there is now; otherwise it is moved onto the display it
    /// overlaps most, and with none it is not used at all.
    /// </para>
    /// <para>
    /// Left exactly where it was whenever it can be, rather than tidied into the working area. A
    /// window snapped to an edge sits a few pixels past it, the width of a resize border nobody can
    /// see, and moving that inside would walk it across the screen a border at a time.
    /// </para>
    /// </summary>
    public static Rectangle? Restorable(WindowPlacement saved, IReadOnlyList<Rectangle> workingAreas)
    {
        var bounds = new Rectangle(saved.X, saved.Y, saved.Width, saved.Height);
        if (bounds.Width < SmallestRestored.Width ||
            bounds.Height < SmallestRestored.Height)
        {
            return null;
        }

        Rectangle? best = null;
        long overlap = 0;
        foreach (var area in workingAreas)
        {
            var shared = Rectangle.Intersect(area, bounds);
            var size = (long) shared.Width * shared.Height;
            if (size > overlap)
            {
                overlap = size;
                best = area;
            }
        }

        if (best is not { } screen)
        {
            return null;
        }

        if (overlap * 2 >= (long) bounds.Width * bounds.Height &&
            bounds.Top >= screen.Top - EdgeSlack &&
            bounds.Top < screen.Bottom - EdgeSlack)
        {
            return bounds;
        }

        var width = Math.Min(bounds.Width, screen.Width);
        var height = Math.Min(bounds.Height, screen.Height);
        return new(
            Math.Clamp(bounds.X, screen.Left, screen.Right - width),
            Math.Clamp(bounds.Y, screen.Top, screen.Bottom - height),
            width,
            height);
    }

    /// <summary>
    /// The client size of a window opened for the first time. The size asked for is in logical
    /// pixels, and the window is per monitor aware, so it is scaled to the display it opens on.
    /// Unscaled, DiffEngine's viewer was 1100 by 700 device pixels while its text grew with the
    /// display: at 200% each pane had room for four characters.
    /// <para>
    /// Kept to nine tenths of the working area, which a scaled window can outgrow, leaving room for
    /// the frame and title bar outside the client area: 1100 by 700 at 150% is too tall for a 1080p
    /// screen once the taskbar is taken off.
    /// </para>
    /// </summary>
    public static Size InitialClientSize(Size logical, int dpi, Size workingArea)
    {
        var width = logical.Width * dpi / 96;
        var height = logical.Height * dpi / 96;
        var maxWidth = workingArea.Width * 9 / 10;
        var maxHeight = workingArea.Height * 9 / 10;
        return new(Math.Min(width, maxWidth), Math.Min(height, maxHeight));
    }

    /// <summary>
    /// Where a window of <paramref name="size"/> sits to be in the middle of
    /// <paramref name="area"/>. One too large for it starts at the area's top left, so its title
    /// bar is still in reach.
    /// </summary>
    public static Point Centred(Rectangle area, Size size) =>
        new(
            Math.Max(area.X, area.X + (area.Width - size.Width) / 2),
            Math.Max(area.Y, area.Y + (area.Height - size.Height) / 2));
}
