namespace PlatformHeads;

/// <summary>
/// Opens a URL in the user's browser, on whichever desktop this is.
/// </summary>
public static class LinkOpener
{
    /// <summary>
    /// False, with what went wrong, when it could not be opened. Never thrown: no browser
    /// registered, or one that will not start, is nothing an app can do anything about, and thrown
    /// from a click it reached the UI thread in place of whatever the click was reporting.
    /// <para>
    /// Off Windows through <c>open</c> or <c>xdg-open</c>, with their output kept from the app's
    /// own, which on a terminal they would otherwise write over.
    /// </para>
    /// </summary>
    public static bool TryOpen(string url, [NotNullWhen(false)] out Exception? error)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var process = Process.Start(
                    new ProcessStartInfo(url)
                    {
                        UseShellExecute = true
                    });
            }
            else
            {
                var opener = OperatingSystem.IsMacOS() ? "open" : "xdg-open";
                using var process = Process.Start(
                    new ProcessStartInfo(opener, [url])
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    });
            }

            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception;
            return false;
        }
    }
}
