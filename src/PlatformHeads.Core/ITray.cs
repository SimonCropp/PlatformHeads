namespace PlatformHeads;

/// <summary>
/// One platform's tray icon: a NotifyIcon on Windows (PlatformHeads.WindowsForms), a
/// StatusNotifierItem on Linux (PlatformHeads.Linux), and on macOS a status item an app's own
/// native library draws with PlatformHeadsMac.
/// <para>
/// <see cref="Apply" /> is handed the whole model, as often as the app likes, and is expected to
/// work out what changed; <see cref="Poll" /> drains what the user did, from whichever thread the
/// app's loop runs on.
/// </para>
/// </summary>
public interface ITray<TIcon> : IDisposable
    where TIcon : struct, Enum
{
    void Apply(TrayModel<TIcon> model);

    /// <summary>
    /// Pops a transient notification. Best effort: a desktop without a notification service
    /// shows nothing, and the caller never waits on it.
    /// </summary>
    void Notify(TrayNotification notification);

    TrayInput Poll();
}
