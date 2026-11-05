namespace PlatformHeads;

/// <summary>
/// The library an app linked PlatformHeadsMacTray into, which <see cref="StatusItemTray{TIcon}" />
/// and <see cref="StatusItemApp" /> call.
/// </summary>
public static class StatusItemLibrary
{
    /// <summary>
    /// Must match PH_TRAY_VERSION in platformheads_tray.h, so a stale library is reported rather
    /// than read as garbage.
    /// </summary>
    public const int ExpectedVersion = 1;

    /// <summary>
    /// Has the tray's P/Invokes find their exports in <paramref name="library" />, under
    /// runtimes/{rid}/native as <see cref="NativeResolver" /> finds one. Once a process: the same
    /// library again does nothing, and another throws.
    /// </summary>
    /// <param name="library">The app's library, with no prefix or extension.</param>
    public static void Use(string library) =>
        NativeResolver.Register(typeof(PhTray).Assembly, PhTray.Name, library);

    /// <summary>
    /// The PH_TRAY_VERSION of the library <see cref="Use" /> named.
    /// </summary>
    public static int Version() =>
        PhTray.Version();

    /// <summary>
    /// What the library makes of <paramref name="items" />, a line an item, as text: the menu
    /// handed over as one array and made a tree again on the other side. Needs no status bar, so a
    /// test can ask it with no menu bar to put an item in.
    /// </summary>
    public static string Describe(IReadOnlyList<TrayMenuItem> items)
    {
        Send(0, "", items);
        var buffer = new byte[64 * 1024];
        var length = PhTray.Describe(buffer, buffer.Length);
        return Encoding.UTF8.GetString(buffer, 0, Math.Min(length, buffer.Length));
    }

    /// <summary>
    /// The menu as <see cref="TrayMenuItem.Flatten" /> writes it, which PlatformHeadsMac's
    /// <c>StatusMenuItem.tree</c> reads back, with <see cref="TrayMenuItem.Flags" /> as the header's
    /// <c>PH_TRAY_*</c> bits, which have the same values.
    /// </summary>
    internal static void Send(int icon, string tooltip, IReadOnlyList<TrayMenuItem> items)
    {
        var flat = TrayMenuItem.Flatten(items);
        var native = new PhTrayItem[flat.Count];
        var strings = new List<IntPtr>();
        try
        {
            for (var i = 0; i < flat.Count; i++)
            {
                var (item, parent) = flat[i];
                native[i] = new()
                {
                    Id = Utf8(item.Id, strings),
                    Label = Utf8(item.Label, strings),
                    Icon = Utf8(item.IconName ?? "", strings),
                    Flags = (int) item.Flags,
                    Parent = parent
                };
            }

            PhTray.Apply(icon, tooltip, native, native.Length);
        }
        finally
        {
            foreach (var pointer in strings)
            {
                Marshal.FreeCoTaskMem(pointer);
            }
        }
    }

    static IntPtr Utf8(string text, List<IntPtr> strings)
    {
        var pointer = Marshal.StringToCoTaskMemUTF8(text);
        strings.Add(pointer);
        return pointer;
    }
}
