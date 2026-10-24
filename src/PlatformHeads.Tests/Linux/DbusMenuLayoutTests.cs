public class DbusMenuLayoutTests
{
    static readonly IReadOnlyList<TrayMenuItem> items =
    [
        new("open", "Open", IconName: "open"),
        new("separator", "", Separator: true),
        new("pending", "Pending", Children: [new("accept", "Accept"), new("discard", "Discard", Enabled: false)]),
        new("startup", "Run at startup", Checked: true)
    ];

    static byte[]? Glyph(string name, int size) =>
        name == "open" ? [1, 2, 3] : null;

    /// <summary>
    /// Depth first, so a submenu's items are numbered straight after the item that opens it, and
    /// the root holds only the top level.
    /// </summary>
    [Test]
    public async Task NodesAreNumberedDepthFirst()
    {
        var layout = DbusMenuLayout.Build(items, Glyph);

        await Assert.That(layout.All.Select(_ => (_.Id, _.ItemId))).IsEquivalentTo(
        [
            (1, "open"),
            (2, null),
            (3, "pending"),
            (4, "accept"),
            (5, "discard"),
            (6, "startup")
        ]);
        await Assert.That(layout.Root.Children.Select(_ => _.Id)).IsEquivalentTo([1, 2, 3, 6]);
        await Assert.That(layout.Find(3)!.Children.Select(_ => _.Id)).IsEquivalentTo([4, 5]);
    }

    /// <summary>
    /// A host asks a node for every property it says it has, so a toggle says it is one, an item
    /// with a submenu says it opens one, and only an item with a glyph says it has an icon.
    /// </summary>
    [Test]
    public async Task EachNodeHasThePropertiesItsItemNeeds()
    {
        var layout = DbusMenuLayout.Build(items, Glyph);

        await Assert.That(layout.Find(1)!.PropertyNames).IsEquivalentTo(["label", "enabled", "visible", "icon-data"]);
        await Assert.That(layout.Find(2)!.PropertyNames).IsEquivalentTo(["type"]);
        await Assert.That(layout.Find(3)!.PropertyNames).IsEquivalentTo(["label", "enabled", "visible", "children-display"]);
        await Assert.That(layout.Find(5)!.Enabled).IsFalse();
        await Assert.That(layout.Find(6)!.PropertyNames).IsEquivalentTo(["label", "enabled", "visible", "toggle-type", "toggle-state"]);
        await Assert.That(layout.Root.PropertyNames).IsEquivalentTo(["children-display"]);
    }

    /// <summary>
    /// The signature is what decides whether the host is told the layout changed, so a check
    /// going on and an item moving into a submenu each have to change it.
    /// </summary>
    [Test]
    public async Task TheSignatureChangesWithAChecksAndWithWhereASubmenuEnds()
    {
        var signature = DbusMenuLayout.Build(items).Signature;
        IReadOnlyList<TrayMenuItem> cleared = [..items.Take(3), items[3] with {Checked = false}];
        IReadOnlyList<TrayMenuItem> moved =
        [
            items[0],
            items[1],
            new("pending", "Pending", Children: [new("accept", "Accept")]),
            new("discard", "Discard", Enabled: false),
            items[3]
        ];

        await Assert.That(DbusMenuLayout.Build(items).Signature).IsEqualTo(signature);
        await Assert.That(DbusMenuLayout.Build(cleared).Signature).IsNotEqualTo(signature);
        await Assert.That(DbusMenuLayout.Build(moved).Signature).IsNotEqualTo(signature);
    }
}
