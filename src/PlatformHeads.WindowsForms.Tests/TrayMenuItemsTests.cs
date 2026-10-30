/// <summary>
/// The ToolStrip items a tray menu is built from, made without an icon in the tray: a
/// NotifyIconTray shows one as it is made, on the desktop of whoever runs the tests.
/// </summary>
public class TrayMenuItemsTests
{
    static readonly NotifyIconOptions<TestTrayIcon> options = new("Sample app", _ => null);

    [Test]
    public async Task ASubmenuIsTheItemsDropDown()
    {
        var item = new TrayMenuItem("pending", "Pending", Children: [new("accept", "Accept"), new("", "", Separator: true), new("discard", "Discard", Enabled: false)]);

        var built = (ToolStripMenuItem) TrayMenuItems.Create(item, options, _ => { });

        await Assert.That(built.Text).IsEqualTo("Pending");
        await Assert.That(built.DropDownItems.Count).IsEqualTo(3);
        await Assert.That(built.DropDownItems[1]).IsTypeOf<ToolStripSeparator>();
        await Assert.That(built.DropDownItems[2].Enabled).IsFalse();
    }

    /// <summary>
    /// An item that opens a submenu is clicked on the way to it, so only an item with none reports
    /// a click, and the app hears no choice nobody made.
    /// </summary>
    [Test]
    public async Task OnlyAnItemWithNoSubmenuReportsAClick()
    {
        var clicked = new List<string>();
        var item = new TrayMenuItem("pending", "Pending", Children: [new("accept", "Accept")]);
        var built = (ToolStripMenuItem) TrayMenuItems.Create(item, options, clicked.Add);

        built.PerformClick();
        built.DropDownItems[0].PerformClick();

        await Assert.That(clicked).IsEquivalentTo(["accept"]);
    }

    [Test]
    public async Task AToggleShowsItsCheck()
    {
        var on = (ToolStripMenuItem) TrayMenuItems.Create(new("startup", "Run at startup", Checked: true), options, _ => { });
        var none = (ToolStripMenuItem) TrayMenuItems.Create(new("open", "Open"), options, _ => { });

        await Assert.That(on.Checked).IsTrue();
        await Assert.That(none.Checked).IsFalse();
    }

    /// <summary>
    /// The theme reaches a submenu, which is a window of its own and would otherwise be drawn in
    /// the system's colours under a menu in the app's.
    /// </summary>
    [Test]
    public async Task TheThemeAndStyleReachASubmenu()
    {
        var themed = new List<ToolStrip>();
        var styled = new List<string>();
        var themedOptions = options with
        {
            Theme = themed.Add,
            Style = (_, item) => styled.Add(item.Id)
        };
        var item = new TrayMenuItem("pending", "Pending", Children: [new("accept", "Accept")]);

        var built = (ToolStripMenuItem) TrayMenuItems.Create(item, themedOptions, _ => { });

        await Assert.That(themed.Single()).IsSameReferenceAs(built.DropDown);
        await Assert.That(styled).IsEquivalentTo(["accept", "pending"]);
    }
}
