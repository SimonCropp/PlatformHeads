namespace PlatformHeads;

/// <summary>
/// native/swift/include/platformheads_tray.h, by the name <see cref="Name" />, which
/// <see cref="StatusItemLibrary.Use" /> resolves to the app's own library: the exports are linked
/// into whichever library the app builds from the PlatformHeadsMacTray target, so nothing here can
/// know the file they are in.
/// </summary>
static class PhTray
{
    public const string Name = "platformheads_tray";

    [DllImport(Name, EntryPoint = "ph_tray_version")]
    public static extern int Version();

    [DllImport(Name, EntryPoint = "ph_tray_init")]
    public static extern int Init();

    [DllImport(Name, EntryPoint = "ph_tray_set_icon")]
    public static extern void SetIcon(int icon, byte[] png, int length);

    [DllImport(Name, EntryPoint = "ph_tray_set_menu_icon")]
    public static extern void SetMenuIcon([MarshalAs(UnmanagedType.LPUTF8Str)] string name, byte[] png, int length);

    [DllImport(Name, EntryPoint = "ph_tray_apply")]
    public static extern void Apply(int icon, [MarshalAs(UnmanagedType.LPUTF8Str)] string tooltip, PhTrayItem[] items, int count);

    [DllImport(Name, EntryPoint = "ph_tray_poll")]
    public static extern int Poll(byte[] buffer, int capacity);

    [DllImport(Name, EntryPoint = "ph_tray_icon_clicked")]
    public static extern int IconClicked();

    [DllImport(Name, EntryPoint = "ph_tray_describe")]
    public static extern int Describe(byte[] buffer, int capacity);

    [DllImport(Name, EntryPoint = "ph_tray_shutdown")]
    public static extern void Shutdown();

    [DllImport(Name, EntryPoint = "ph_tray_app_on_session_end")]
    public static extern unsafe void AppOnSessionEnd(delegate* unmanaged<void> ended);

    [DllImport(Name, EntryPoint = "ph_tray_app_start")]
    public static extern void AppStart([MarshalAs(UnmanagedType.LPUTF8Str)] string napReason);

    [DllImport(Name, EntryPoint = "ph_tray_app_pump")]
    public static extern void AppPump();

    [DllImport(Name, EntryPoint = "ph_tray_app_quitting")]
    public static extern int AppQuitting();

    [DllImport(Name, EntryPoint = "ph_tray_app_stop")]
    public static extern void AppStop();
}
