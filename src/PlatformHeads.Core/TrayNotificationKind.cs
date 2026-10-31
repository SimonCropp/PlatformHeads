namespace PlatformHeads;

/// <summary>
/// Which icon a notification pops with, where the desktop draws one. A failure was once the only
/// thing BuildMonitor's tray announced, so Windows drew every balloon with the error icon, and a
/// prompt that was ready to paste looked as if something had gone wrong.
/// </summary>
public enum TrayNotificationKind
{
    Error,
    Info,

    /// <summary>
    /// Something to know that is not itself a failure, such as a setting that could not take
    /// effect. DiffEngineTray drew every balloon of its own with Windows' warning icon.
    /// </summary>
    Warning
}
