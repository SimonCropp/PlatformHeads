namespace PlatformHeads;

/// <summary>
/// A <see cref="TrayMenuItem" /> as the ToolStrip item a menu shows, submenu and all. Apart from
/// <see cref="NotifyIconTray{TIcon}" /> so it is tested without an icon in the tray.
/// </summary>
public static class TrayMenuItems
{
    public static ToolStripItem Create<TIcon>(TrayMenuItem item, NotifyIconOptions<TIcon> options, Action<string> clicked)
        where TIcon : struct, Enum
    {
        if (item.Separator)
        {
            return new ToolStripSeparator();
        }

        var menuItem = new ToolStripMenuItem(item.Label)
        {
            Enabled = item.Enabled,
            Image = item.IconName is null ? null : options.Glyph?.Invoke(item.IconName),
            Checked = item.Checked == true,
            Tag = item.Id
        };

        if (item.HasChildren)
        {
            options.Theme?.Invoke(menuItem.DropDown);
            foreach (var child in item.Children!)
            {
                menuItem.DropDownItems.Add(Create(child, options, clicked));
            }
        }
        else
        {
            // Only a leaf: an item that opens a submenu is clicked on the way to it
            menuItem.Click += (_, _) => clicked(item.Id);
        }

        options.Style?.Invoke(menuItem, item);
        return menuItem;
    }
}
