namespace PlatformHeads;

/// <summary>
/// The macOS tray: PlatformHeadsMac's StatusItem in the menu bar, in the library the app linked
/// PlatformHeadsMacTray into, since the managed side has no AppKit.
/// <para>
/// Every call is the main thread's to make, as AppKit requires, and AppKit's events have to be
/// pumped on it for the item to do anything: by the app's own window library, which then asks
/// PlatformHeadsMacTray's <c>StatusTray.shared.hasInput</c> in its pump, or by
/// <see cref="StatusItemApp.Pump" /> for an app whose only face is the tray.
/// </para>
/// </summary>
public sealed class StatusItemTray<TIcon> : ITray<TIcon>
    where TIcon : struct, Enum
{
    // An id is whatever the app made it, which this holds with room over
    readonly byte[] chosen = new byte[64 * 1024];
    readonly StatusItemOptions<TIcon> options;
    readonly HashSet<string> glyphs = [];

    StatusItemTray(StatusItemOptions<TIcon> options) =>
        this.options = options;

    /// <summary>
    /// The item in the status bar, with every icon handed over. Null with an
    /// <paramref name="error" /> when there is no library for this machine, one of another version,
    /// or no status bar to put the item in.
    /// </summary>
    public static StatusItemTray<TIcon>? Open(StatusItemOptions<TIcon> options, out string? error)
    {
        try
        {
            StatusItemLibrary.Use(options.Library);
            var version = StatusItemLibrary.Version();
            if (version != StatusItemLibrary.ExpectedVersion)
            {
                error = $"The tray's native library is version {version}, and this was built for {StatusItemLibrary.ExpectedVersion}.";
                return null;
            }

            if (PhTray.Init() == 0)
            {
                error = "AppKit gave the status item no button to draw in.";
                return null;
            }
        }
        catch (Exception exception)
            when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            error = exception.Message;
            return null;
        }

        foreach (var icon in Enum.GetValues<TIcon>())
        {
            if (options.IconPng(icon, 48) is { } png)
            {
                PhTray.SetIcon(Convert.ToInt32(icon), png, png.Length);
            }
        }

        error = null;
        return new(options);
    }

    /// <summary>
    /// The glyphs the menu names that have not been handed over yet, then the model, which the
    /// item builds its menu from as it next opens.
    /// </summary>
    public void Apply(TrayModel<TIcon> model)
    {
        HandOverGlyphs(model.Items);
        StatusItemLibrary.Send(Convert.ToInt32(model.Icon), model.Tooltip, model.Items);
    }

    void HandOverGlyphs(IReadOnlyList<TrayMenuItem> items)
    {
        if (options.GlyphPng is null)
        {
            return;
        }

        foreach (var item in items)
        {
            if (item.IconName is { Length: > 0 } name &&
                glyphs.Add(name) &&
                options.GlyphPng(name, 32) is { } png)
            {
                PhTray.SetMenuIcon(name, png, png.Length);
            }

            HandOverGlyphs(item.Children ?? []);
        }
    }

    /// <summary>
    /// What was chosen since the last poll, one item a poll in the order they were chosen, and
    /// whether the icon itself was clicked.
    /// </summary>
    public TrayInput Poll()
    {
        var iconClicked = PhTray.IconClicked() != 0;
        var length = PhTray.Poll(chosen, chosen.Length);
        var item = length == 0 ? null : Encoding.UTF8.GetString(chosen, 0, Math.Min(length, chosen.Length));
        return new(ClickedItem: item, IconClicked: iconClicked);
    }

    /// <summary>
    /// Through osascript, which <see cref="DesktopNotification" /> runs: the status item has no
    /// balloons of its own.
    /// </summary>
    public void Notify(TrayNotification notification) =>
        DesktopNotification.Show(options.AppName, notification);

    /// <summary>
    /// Takes the item out of the status bar.
    /// </summary>
    public void Dispose() =>
        PhTray.Shutdown();
}
