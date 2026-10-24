namespace PlatformHeads;

/// <summary>
/// What a <see cref="NotifyIconTray{TIcon}" /> needs of the app: its name, its pictures, and how
/// its menus look.
/// </summary>
/// <param name="Title">The icon's tooltip until a model says otherwise.</param>
/// <param name="Icon">The picture for each of the app's icons, at the small icon size, or null.</param>
/// <param name="Glyph">A menu item's <see cref="TrayMenuItem.IconName" /> as a picture, or null.</param>
/// <param name="Theme">
/// Applied to the menu as it is made and before every opening, and to each submenu as it is built,
/// for an app that draws its menus in colours of its own.
/// </param>
/// <param name="Style">Applied to each item as it is built, after everything else is set on it.</param>
public sealed record NotifyIconOptions<TIcon>(
    string Title,
    Func<TIcon, Icon?> Icon,
    Func<string, Image?>? Glyph = null,
    Action<ToolStrip>? Theme = null,
    Action<ToolStripMenuItem, TrayMenuItem>? Style = null)
    where TIcon : struct, Enum;
