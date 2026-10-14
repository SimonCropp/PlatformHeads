namespace PlatformHeads;

/// <summary>
/// Opens the window for one platform. Null with an <paramref name="error"/> rather than an
/// exception, because a machine with no renderer for its runtime identifier and no desktop session
/// to draw into are both ordinary, and both want the same message rather than a stack trace.
/// </summary>
/// <param name="title">The window's title.</param>
/// <param name="width">The width of a window opened for the first time, in logical units.</param>
/// <param name="height">The height of a window opened for the first time, in logical units.</param>
/// <param name="hidden">Whether the window opens hidden, as behind a tray.</param>
/// <param name="placement">How the last window was left, to open this one the same way, or null
/// to open at <paramref name="width"/> by <paramref name="height"/> wherever the platform puts a
/// new window. A head ignores one that no longer fits a screen it has.</param>
/// <param name="error">Why there is no window, when there is none.</param>
public delegate TWindow? OpenWindow<out TWindow>(string title, int width, int height, bool hidden, WindowPlacement? placement, out string? error)
    where TWindow : class;
