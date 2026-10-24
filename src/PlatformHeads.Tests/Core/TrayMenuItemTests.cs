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
}
