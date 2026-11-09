// ReSharper disable RedundantUnsafeContext
// ReSharper disable ArrangeModifiersOrder
namespace PlatformHeads;

/// <summary>
/// native/swift/include/platformheads_tray.h, by the name <see cref="Name" />, which
/// <see cref="StatusItemLibrary.Use" /> resolves to the app's own library: the exports are linked
/// into whichever library the app builds from the PlatformHeadsMacTray target, so nothing here can
/// know the file they are in.
/// </summary>
static partial class PhTray
{
    public const string Name = "platformheads_tray";

    [LibraryImport(Name, EntryPoint = "ph_tray_version")]
    public static partial int Version();

    [LibraryImport(Name, EntryPoint = "ph_tray_init")]
    public static partial int Init();

    [LibraryImport(Name, EntryPoint = "ph_tray_set_icon")]
    public static partial void SetIcon(int icon, byte[] png, int length);

    [LibraryImport(Name, EntryPoint = "ph_tray_set_menu_icon", StringMarshalling = StringMarshalling.Utf8)]
    public static partial void SetMenuIcon(string name, byte[] png, int length);

    [LibraryImport(Name, EntryPoint = "ph_tray_apply", StringMarshalling = StringMarshalling.Utf8)]
    public static partial void Apply(int icon, string tooltip, PhTrayItem[] items, int count);

    // [Out], since a generated array is only [In] unless it says otherwise, and the library fills
    // it: without, the bytes it wrote are not promised to reach the caller.
    [LibraryImport(Name, EntryPoint = "ph_tray_poll")]
    public static partial int Poll([Out] byte[] buffer, int capacity);

    [LibraryImport(Name, EntryPoint = "ph_tray_icon_clicked")]
    public static partial int IconClicked();

    [LibraryImport(Name, EntryPoint = "ph_tray_describe")]
    public static partial int Describe([Out] byte[] buffer, int capacity);

    [LibraryImport(Name, EntryPoint = "ph_tray_shutdown")]
    public static partial void Shutdown();

    [LibraryImport(Name, EntryPoint = "ph_tray_app_on_session_end")]
    public static unsafe partial void AppOnSessionEnd(delegate* unmanaged<void> ended);

    [LibraryImport(Name, EntryPoint = "ph_tray_app_start", StringMarshalling = StringMarshalling.Utf8)]
    public static partial void AppStart(string napReason);

    [LibraryImport(Name, EntryPoint = "ph_tray_app_pump")]
    public static partial void AppPump();

    [LibraryImport(Name, EntryPoint = "ph_tray_app_quitting")]
    public static partial int AppQuitting();

    [LibraryImport(Name, EntryPoint = "ph_tray_app_stop")]
    public static partial void AppStop();
}
