namespace PlatformHeads;

/// <summary>
/// What a <see cref="StatusNotifierTray{TIcon}" /> needs of the app: its names, and the pictures for
/// its icons and glyphs, asked for as they are drawn.
/// </summary>
/// <param name="Id">The item's Id property, and the app name notifications pop under.</param>
/// <param name="Title">The item's Title property, and its tooltip until a model says otherwise.</param>
/// <param name="IconPixels">
/// An icon as a square of ARGB32 pixels, in network byte order, which is what the item's pixmaps
/// are on the wire. Asked for at 16, 22, 24, 32 and 48 pixels, for the host to pick from. Null for
/// a size the app has no picture at.
/// </param>
/// <param name="Glyph">A menu item's <see cref="TrayMenuItem.IconName" /> as a 16 pixel PNG, or null.</param>
/// <param name="NeedsAttention">
/// Which icons the item's status is NeedsAttention for, which a host may draw differently. Active
/// for every icon when null.
/// </param>
/// <param name="Failed">
/// Told what failed on the bus and where, which nothing else would: the bus calls back on a thread
/// of its own, and the app's loop is not waiting on any of them.
/// </param>
public sealed record StatusNotifierOptions<TIcon>(
    string Id,
    string Title,
    Func<TIcon, int, byte[]?> IconPixels,
    Func<string, int, byte[]?>? Glyph = null,
    Func<TIcon, bool>? NeedsAttention = null,
    Action<Exception, string>? Failed = null)
    where TIcon : struct, Enum;
