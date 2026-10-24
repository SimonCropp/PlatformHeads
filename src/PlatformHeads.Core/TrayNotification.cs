namespace PlatformHeads;

/// <summary>
/// Something a tray tells the user about outside any window. Popped once by
/// <see cref="ITray{TIcon}.Notify" />, best effort.
/// </summary>
/// <param name="Title">The notification's first line.</param>
/// <param name="Message">What it says under the title.</param>
/// <param name="Key">
/// What it is about, in the app's own terms, so a click on it can land there: it comes back as
/// <see cref="TrayInput.ClickedNotification" />. Null where it is about nothing in particular.
/// </param>
/// <param name="Kind">Which icon it pops with, where the desktop draws one.</param>
public sealed record TrayNotification(string Title, string Message, string? Key = null, TrayNotificationKind Kind = TrayNotificationKind.Error);
