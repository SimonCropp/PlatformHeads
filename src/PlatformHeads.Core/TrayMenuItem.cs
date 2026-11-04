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

    /// <summary>
    /// What the item is, as the bits a C ABI carries, submenu aside.
    /// </summary>
    public TrayMenuFlags Flags
    {
        get
        {
            var flags = TrayMenuFlags.None;
            if (Enabled)
            {
                flags |= TrayMenuFlags.Enabled;
            }

            if (Separator)
            {
                flags |= TrayMenuFlags.Separator;
            }

            if (Checked is { } on)
            {
                flags |= TrayMenuFlags.Checkable;
                if (on)
                {
                    flags |= TrayMenuFlags.Checked;
                }
            }

            return flags;
        }
    }

    /// <summary>
    /// The menu as one list, each submenu's items after the item that opens it, with the index of
    /// that item in the same list, or -1 for an item of the menu itself: how a C ABI carries a
    /// menu, since a struct cannot hold a list of itself. PlatformHeadsMac's
    /// <c>StatusMenuItem.tree</c> makes it a tree again.
    /// </summary>
    public static IReadOnlyList<(TrayMenuItem Item, int Parent)> Flatten(IReadOnlyList<TrayMenuItem> items)
    {
        var flat = new List<(TrayMenuItem Item, int Parent)>();
        Add(items, -1, flat);
        return flat;
    }

    static void Add(IReadOnlyList<TrayMenuItem> items, int parent, List<(TrayMenuItem Item, int Parent)> flat)
    {
        foreach (var item in items)
        {
            var index = flat.Count;
            flat.Add((item, parent));
            if (item.HasChildren)
            {
                Add(item.Children!, index, flat);
            }
        }
    }
}
