namespace PlatformHeads;

/// <summary>
/// What a tray icon shows and offers: which of the app's icons, its tooltip, and its menu. Handed
/// whole to <see cref="ITray{TIcon}.Apply" />, which works out what changed, so an app builds it
/// from its own state rather than telling each platform's tray what to do.
/// </summary>
/// <typeparam name="TIcon">
/// The app's icons. Each back end is handed the pictures for every one of them when it opens, so a
/// frame names an icon rather than carrying its pixels.
/// </typeparam>
public sealed record TrayModel<TIcon>(TIcon Icon, string Tooltip, IReadOnlyList<TrayMenuItem> Items)
    where TIcon : struct, Enum;
