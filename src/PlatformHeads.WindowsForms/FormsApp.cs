namespace PlatformHeads;

/// <summary>
/// The process wide WinForms setup, once.
/// <para>
/// Once because two entry points need it, the app and a test host that never runs the app's Main
/// yet renders the same controls into committed baselines, and SetCompatibleTextRenderingDefault
/// throws once a window exists. A host that configured itself differently would be photographing
/// something no user sees.
/// </para>
/// </summary>
public static class FormsApp
{
    static readonly Lock gate = new();
    static bool configured;

    /// <summary>
    /// Visual styles, GDI+ text, <paramref name="dpi"/> and <paramref name="colors"/>, before any
    /// window exists.
    /// </summary>
    /// <param name="dpi">PerMonitorV2 for the app, so a window follows whichever display it is on.
    /// DpiUnaware for a capture host, since a form sized with LogicalToDeviceUnits in a DPI aware
    /// host scales with the capturing machine's display, and every committed baseline becomes a
    /// picture of that machine rather than of the app.</param>
    /// <param name="colors">Set last and before any handle exists, because the mode is read as each
    /// control is created, and setting it once windows exist hangs. A theme changed later reaches the
    /// native parts, check boxes and scroll bars, on the next start.</param>
    /// <returns>False when an earlier call had already configured the process, which this one left
    /// as it was.</returns>
    public static bool Configure(HighDpiMode dpi, SystemColorMode colors)
    {
        lock (gate)
        {
            if (configured)
            {
                return false;
            }

            configured = true;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(dpi);
            // Not a diagnostic on the SDK this builds against; insurance for a machine whose
            // reference pack still marks these APIs experimental.
#pragma warning disable WFO5001
            Application.SetColorMode(colors);
#pragma warning restore WFO5001
            return true;
        }
    }
}
