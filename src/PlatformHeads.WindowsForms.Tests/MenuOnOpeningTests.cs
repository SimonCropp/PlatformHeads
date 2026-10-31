/// <summary>
/// Opened with nothing put on the desktop: a menu is a top level window, and shown as one it came
/// up at the top left of the screen of whoever was at the machine. Not top level it is a child of
/// no window on a screen, and still opens, lays out and closes as it does under a tray icon.
/// </summary>
[TUnit.Core.Executors.STAThreadExecutor]
public class MenuOnOpeningTests
{
    [Test]
    public async Task EachOpenBuildsTheItemsAgainBesideTheKeptOnes()
    {
        using var menu = new ContextMenuStrip();
        var exit = menu.Items.Add("Exit");
        var opens = 0;
        MenuOnOpening.Rebuild(
            menu,
            _ =>
            {
                opens++;
                _.Add($"Open {opens}");
            });

        Open(menu);
        var first = Texts(menu);
        menu.Close();
        var closed = Texts(menu);
        Open(menu);
        var second = Texts(menu);
        menu.Close();

        await Assert.That(first).IsEquivalentTo(["Exit", "Open 1"]);
        await Assert.That(closed).IsEquivalentTo(["Exit"]);
        await Assert.That(second).IsEquivalentTo(["Exit", "Open 2"]);
        await Assert.That(menu.Items.Contains(exit)).IsTrue();
    }

    /// <summary>
    /// A kept item is told apart as itself, so one built with the same text is still taken off.
    /// </summary>
    [Test]
    public async Task ABuiltItemWithAKeptItemsTextIsStillTakenOff()
    {
        using var menu = new ContextMenuStrip();
        menu.Items.Add("Options");
        MenuOnOpening.Rebuild(menu, _ => _.Add("Options"));

        Open(menu);
        menu.Close();

        await Assert.That(Texts(menu)).IsEquivalentTo(["Options"]);
    }

    /// <summary>
    /// Disposed at the next open, not while the close that took them off is still running.
    /// ToolStripItem.IsDisposed stays false through a Dispose, so the event is what says it happened.
    /// </summary>
    [Test]
    public async Task WhatAnOpenBuiltIsDisposedAtTheNext()
    {
        using var menu = new ContextMenuStrip();
        var built = new List<ToolStripItem>();
        MenuOnOpening.Rebuild(menu, _ => built.Add(_.Add("Pending")));

        Open(menu);
        var disposed = 0;
        built[0].Disposed += (_, _) => disposed++;
        menu.Close();
        var afterClose = disposed;
        Open(menu);
        menu.Close();

        await Assert.That(afterClose).IsEqualTo(0);
        await Assert.That(disposed).IsEqualTo(1);
    }

    /// <summary>
    /// A menu that starts with no items has its first open marked cancelled before Opening is
    /// raised, which showed nothing on the first click of the icon.
    /// </summary>
    [Test]
    public async Task AMenuThatStartsEmptyStillOpens()
    {
        using var menu = new ContextMenuStrip();
        MenuOnOpening.Rebuild(menu, _ => _.Add("Refresh"));

        Open(menu);
        var shown = menu.Visible;
        menu.Close();

        await Assert.That(shown).IsTrue();
    }

    [Test]
    public async Task AnOpenThatBuildsNothingIsCancelled()
    {
        using var menu = new ContextMenuStrip();
        MenuOnOpening.Rebuild(menu, _ =>
        {
        });

        Open(menu);

        await Assert.That(menu.Visible).IsFalse();
    }

    static void Open(ContextMenuStrip menu)
    {
        menu.TopLevel = false;
        menu.Show();
    }

    static List<string> Texts(ContextMenuStrip menu) =>
        menu.Items
            .Cast<ToolStripItem>()
            .Select(_ => _.Text ?? "")
            .ToList();
}
