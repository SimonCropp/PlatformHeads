public class TrayMenuItemTests
{
    /// <summary>
    /// A back end compares the menu it is handed with the one it shows, and an app builds new lists
    /// every time it builds its menu. Compared by reference, every model was a new menu.
    /// </summary>
    [Test]
    public async Task ItemsWithTheSameSubmenuAreEqual()
    {
        var first = new TrayMenuItem("pending", "Pending", Children: [new("accept", "Accept"), new("discard", "Discard")]);
        var second = new TrayMenuItem("pending", "Pending", Children: [new("accept", "Accept"), new("discard", "Discard")]);

        await Assert.That(first).IsEqualTo(second);
        await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
    }

    [Test]
    public async Task ItemsWithDifferentSubmenusAreNot()
    {
        var first = new TrayMenuItem("pending", "Pending", Children: [new("accept", "Accept")]);
        var second = new TrayMenuItem("pending", "Pending", Children: [new("accept", "Accept", Enabled: false)]);

        await Assert.That(first).IsNotEqualTo(second);
    }

    [Test]
    public async Task NoSubmenuAndAnEmptyOneAreTheSame()
    {
        var none = new TrayMenuItem("open", "Open");
        var empty = new TrayMenuItem("open", "Open", Children: []);

        await Assert.That(none).IsEqualTo(empty);
        await Assert.That(empty.HasChildren).IsFalse();
    }

    [Test]
    public async Task ACheckIsPartOfTheItem()
    {
        var off = new TrayMenuItem("startup", "Run at startup", Checked: false);
        var on = new TrayMenuItem("startup", "Run at startup", Checked: true);
        var none = new TrayMenuItem("startup", "Run at startup");

        await Assert.That(off).IsNotEqualTo(on);
        await Assert.That(off).IsNotEqualTo(none);
    }

    /// <summary>
    /// A check that is off and no check at all are different items, so they are different bits.
    /// </summary>
    [Test]
    public async Task TheFlagsSayWhatTheItemIs()
    {
        await Assert.That(new TrayMenuItem("open", "Open").Flags).IsEqualTo(TrayMenuFlags.Enabled);
        await Assert.That(new TrayMenuItem("", "", Enabled: false, Separator: true).Flags).IsEqualTo(TrayMenuFlags.Separator);
        await Assert.That(new TrayMenuItem("startup", "Run at startup", Checked: false).Flags).IsEqualTo(TrayMenuFlags.Enabled | TrayMenuFlags.Checkable);
        await Assert.That(new TrayMenuItem("startup", "Run at startup", Checked: true).Flags).IsEqualTo(TrayMenuFlags.Enabled | TrayMenuFlags.Checkable | TrayMenuFlags.Checked);
    }

    /// <summary>
    /// The values a C ABI's header writes down, which PlatformHeadsMac's StatusMenuFlags reads.
    /// </summary>
    [Test]
    public async Task TheFlagsHaveTheValuesTheHeadersUse() =>
        await Assert.That(string.Join(" ", Enum.GetValues<TrayMenuFlags>().Select(_ => $"{_}={(int) _}")))
            .IsEqualTo("None=0 Enabled=1 Separator=2 Checkable=4 Checked=8");

    [Test]
    public async Task AFlattenedMenuNamesEachItemsParent()
    {
        TrayMenuItem[] items =
        [
            new("open", "Open"),
            new("pending", "Pending", Children:
            [
                new("accept", "Accept"),
                new("more", "More", Children: [new("discard", "Discard")])
            ]),
            new("exit", "Exit")
        ];

        var flat = TrayMenuItem.Flatten(items);

        await Assert.That(string.Join(" ", flat.Select(_ => $"{_.Item.Id}:{_.Parent}")))
            .IsEqualTo("open:-1 pending:-1 accept:1 more:1 discard:3 exit:-1");
    }

    [Test]
    public async Task AnEmptyMenuFlattensToNothing() =>
        await Assert.That(TrayMenuItem.Flatten([]).Count).IsEqualTo(0);
}
