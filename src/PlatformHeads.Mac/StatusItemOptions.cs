namespace PlatformHeads;

/// <summary>
/// What a <see cref="StatusItemTray{TIcon}" /> needs of the app: the library it linked
/// PlatformHeadsMacTray into, its name, and the pictures for its icons and glyphs.
/// </summary>
/// <param name="Library">
/// The app's library that exports the tray, with no prefix or extension, as
/// <see cref="StatusItemLibrary.Use" /> takes it.
/// </param>
/// <param name="AppName">The name notifications pop under.</param>
/// <param name="IconPng">
/// An icon as a PNG, asked for at 48 pixels for each of the app's icons as the tray opens, and drawn
/// at the menu bar's 18 points, which 48 pixels is sharp at on any display. Null for one the app has
/// no picture of.
/// </param>
/// <param name="GlyphPng">
/// A menu item's <see cref="TrayMenuItem.IconName" /> as a PNG, asked for at 32 pixels the first time
/// a menu names it, and drawn at 16 points. Null for none.
/// </param>
public sealed record StatusItemOptions<TIcon>(
    string Library,
    string AppName,
    Func<TIcon, int, byte[]?> IconPng,
    Func<string, int, byte[]?>? GlyphPng = null)
    where TIcon : struct, Enum;
