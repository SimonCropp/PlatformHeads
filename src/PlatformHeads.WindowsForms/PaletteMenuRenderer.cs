namespace PlatformHeads;

/// <summary>
/// Draws a ToolStrip menu in a <see cref="MenuPalette" />. It was two, line for line the same apart
/// from which palette they read: BuildMonitor's, for its context menus and its tray, and the
/// DiffEngine viewer's, for its context menu.
/// <para>
/// Assigned per strip rather than through <c>ToolStripManager.Renderer</c>, which is process wide
/// state a test host would share. Per strip, it also beats a renderer installed for the whole
/// process, such as the system's dark mode one, so a menu's pixels are the app's on any machine and
/// a snapshot of one does not move with the OS theme.
/// </para>
/// </summary>
public sealed class PaletteMenuRenderer(MenuPalette palette) :
    ToolStripProfessionalRenderer(new PaletteColorTable(palette))
{
    public MenuPalette Palette => palette;

    /// <summary>
    /// Item text is not a colour table member. <c>ToolStripMenuItem</c> resolves
    /// <c>SystemColors.MenuText</c> unless the item's own ForeColor was set, and it is per item, so
    /// setting it on the strip does nothing, then hands that to the renderer. This is the one place
    /// it can be put right for every item at once.
    /// </summary>
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? palette.Text : palette.Dim;
        base.OnRenderItemText(e);
    }

    /// <summary>
    /// A submenu's arrow, and the overflow arrows of a menu taller than the screen.
    /// </summary>
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = palette.Text;
        base.OnRenderArrow(e);
    }
}

/// <summary>
/// Every member a drop down menu is drawn from, including some that a menu with no icons, no
/// submenus or no separators never reaches: set anyway, since the cost is nothing and the
/// alternative is a later separator or icon bleeding the light default into a committed baseline.
/// </summary>
sealed class PaletteColorTable(MenuPalette palette) : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => palette.Background;
    public override Color MenuBorder => palette.Border;
    public override Color MenuItemBorder => palette.ItemBorder;
    public override Color MenuItemSelected => palette.Selected;
    public override Color MenuItemSelectedGradientBegin => palette.Selected;
    public override Color MenuItemSelectedGradientEnd => palette.Selected;
    public override Color MenuItemPressedGradientBegin => palette.Pressed;
    public override Color MenuItemPressedGradientMiddle => palette.Pressed;
    public override Color MenuItemPressedGradientEnd => palette.Pressed;
    public override Color ImageMarginGradientBegin => palette.Background;
    public override Color ImageMarginGradientMiddle => palette.Background;
    public override Color ImageMarginGradientEnd => palette.Background;
    public override Color SeparatorDark => palette.Separator;
    public override Color SeparatorLight => palette.Separator;
}
