namespace PlatformHeads;

/// <summary>
/// Pops a notification through the desktop's own command, for a tray that has no balloon of its
/// own: <c>notify-send</c> on Linux and <c>osascript</c> on macOS. Nothing on Windows, where the
/// NotifyIcon pops its own. Best effort, and never waits longer than five seconds.
/// <para>
/// No click on one is reported. Neither command says when the notification it popped is clicked.
/// </para>
/// </summary>
public static class DesktopNotification
{
    static readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

    public static void Show(string appName, TrayNotification notification)
    {
        if (OperatingSystem.IsMacOS())
        {
            ProcessRunner.Run("osascript", ["-e", Script(notification)], timeout);
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            ProcessRunner.Run("notify-send", Arguments(appName, notification), timeout);
        }
    }

    /// <summary>
    /// notify-send speaks org.freedesktop.Notifications for the app, and is installed wherever a
    /// notification daemon is. Arguments go as a list, so a title with a quote in it is safe.
    /// </summary>
    public static IReadOnlyList<string> Arguments(string appName, TrayNotification notification) =>
        [$"--app-name={appName}", "--urgency=normal", notification.Title, notification.Message];

    /// <summary>
    /// Through osascript rather than UNUserNotificationCenter: that API refuses a process that is
    /// not an app bundle, and a .NET tool is a bare executable.
    /// </summary>
    public static string Script(TrayNotification notification) =>
        $"display notification \"{Escape(notification.Message)}\" with title \"{Escape(notification.Title)}\"";

    static string Escape(string text) =>
        text.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
