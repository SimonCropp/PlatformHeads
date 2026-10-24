namespace PlatformHeads;

/// <summary>
/// What happened at the tray icon since the last poll.
/// </summary>
/// <param name="ClickedItem">The <see cref="TrayMenuItem.Id" /> of the item chosen last, or null.</param>
/// <param name="IconClicked">Whether the icon itself was clicked, as opposed to its menu.</param>
/// <param name="ClickedNotification">
/// A click on the notification that last popped, carrying the <see cref="TrayNotification.Key" /> it
/// was built with, or "" where it had none. Null where nothing was clicked, which is why an empty
/// string and null differ here.
/// </param>
public readonly record struct TrayInput(string? ClickedItem = null, bool IconClicked = false, string? ClickedNotification = null);
