namespace PlatformHeads;

/// <summary>
/// One entry of a tray menu. <see cref="Id" /> is what <see cref="TrayInput.ClickedItem" /> reports
/// when it is chosen, so it has to be unique across the whole menu, submenus included.
/// <para>
/// Equal by value, submenus included, rather than by the reference of its list of children: a
/// back end compares the menu it is handed with the one it shows, and an app builds a new list
/// every time it builds the menu.
/// </para>
/// </summary>
/// <param name="Id">What the item reports when it is chosen.</param>
/// <param name="Label">What the item says.</param>
/// <param name="Enabled">Whether it can be chosen. A disabled item is drawn dimmed.</param>
/// <param name="Separator">A line between groups of items, which has no label and can't be chosen.</param>
/// <param name="IconName">A glyph the app handed the back end when it opened, or null for none.</param>
/// <param name="Checked">Null for an item that is not a toggle, otherwise whether it shows a check.</param>
/// <param name="Children">
/// A submenu, which the item opens rather than being chosen itself. Null or empty for none.
/// </param>
public sealed record TrayMenuItem(
    string Id,
    string Label,
    bool Enabled = true,
    bool Separator = false,
    string? IconName = null,
    bool? Checked = null,
    IReadOnlyList<TrayMenuItem>? Children = null)
{
    public bool HasChildren =>
        Children is {Count: > 0};

    public bool Equals(TrayMenuItem? other) =>
        other is not null &&
        Id == other.Id &&
        Label == other.Label &&
        Enabled == other.Enabled &&
        Separator == other.Separator &&
        IconName == other.IconName &&
        Checked == other.Checked &&
        (Children ?? []).SequenceEqual(other.Children ?? []);

    public override int GetHashCode() =>
        HashCode.Combine(Id, Label, Enabled, Separator, IconName, Checked, Children?.Count ?? 0);
}
