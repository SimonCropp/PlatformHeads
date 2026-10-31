/// <summary>
/// What of the tray icon can be held to without one in the tray: a NotifyIconTray shows its icon
/// as it is made, on the desktop of whoever runs the tests.
/// </summary>
public class NotifyIconTrayTests
{
    [Test]
    public async Task EachKindPopsWithItsOwnIcon()
    {
        await Assert.That(NotifyIconTray<TestTrayIcon>.BalloonIcon(TrayNotificationKind.Error)).IsEqualTo(ToolTipIcon.Error);
        await Assert.That(NotifyIconTray<TestTrayIcon>.BalloonIcon(TrayNotificationKind.Info)).IsEqualTo(ToolTipIcon.Info);
        await Assert.That(NotifyIconTray<TestTrayIcon>.BalloonIcon(TrayNotificationKind.Warning)).IsEqualTo(ToolTipIcon.Warning);
    }

    /// <summary>
    /// Read through a field WinForms does not promise, so a WinForms that renames it fails here
    /// rather than at a hot key that quietly never arrives. The window behind it is made as the
    /// icon is first shown, which no test does, so an icon never shown has none yet.
    /// </summary>
    [Test]
    [TUnit.Core.Executors.STAThreadExecutor]
    public async Task AnIconsWindowIsFoundByItsField()
    {
        using var icon = new NotifyIcon();

        await Assert.That(NotifyIconTray<TestTrayIcon>.WindowOf(icon)).IsEqualTo(IntPtr.Zero);
    }
}
