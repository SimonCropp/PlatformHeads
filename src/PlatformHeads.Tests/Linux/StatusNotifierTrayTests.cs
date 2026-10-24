/// <summary>
/// The Linux tray over a bus of the test's own, with a stand-in for the panel that hosts it. It had
/// no test at all while it was BuildMonitor's.
/// </summary>
[RunOn(TUnit.Core.Enums.OS.Linux)]
public class StatusNotifierTrayTests
{
    static readonly TimeSpan wait = TimeSpan.FromSeconds(10);

    static readonly IReadOnlyList<TrayMenuItem> items =
    [
        new("open", "Open"),
        new("pending", "Pending", Children: [new("accept", "Accept"), new("discard", "Discard")]),
        new("startup", "Run at startup", Checked: true)
    ];

    static StatusNotifierOptions<TestTrayIcon> Options() =>
        new(
            "SampleApp",
            "Sample app",
            (_, size) => new byte[size * size * 4],
            NeedsAttention: _ => _ == TestTrayIcon.Attention);

    [Test]
    public async Task TheItemRegistersWithTheWatcher()
    {
        await using var bus = await PrivateBus.Start();
        using var watcher = await StandInWatcher.Start(bus.Address);

        using var tray = StatusNotifierTray<TestTrayIcon>.Open(Options(), bus.Address, out var error);

        await Assert.That(error).IsNull();
        await Assert.That(await watcher.Registered.WaitAsync(wait)).IsEqualTo(tray!.BusName);
    }

    [Test]
    public async Task TheHostReadsWhatTheModelSays()
    {
        await using var bus = await PrivateBus.Start();
        using var watcher = await StandInWatcher.Start(bus.Address);
        using var tray = StatusNotifierTray<TestTrayIcon>.Open(Options(), bus.Address, out _)!;

        tray.Apply(new(TestTrayIcon.Attention, "Two pending", items));

        var title = await watcher.Property(tray.BusName, "/StatusNotifierItem", "org.kde.StatusNotifierItem", "Title").WaitAsync(wait);
        var status = await watcher.Property(tray.BusName, "/StatusNotifierItem", "org.kde.StatusNotifierItem", "Status").WaitAsync(wait);
        var startup = DbusMenuLayout.Build(items).All.Single(_ => _.ItemId == "startup");
        var checkedState = await watcher.MenuProperty(tray.BusName, startup.Id, "toggle-state").WaitAsync(wait);
        await Assert.That(title.GetString()).IsEqualTo("Sample app");
        await Assert.That(status.GetString()).IsEqualTo("NeedsAttention");
        await Assert.That(checkedState.GetInt32()).IsEqualTo(1);
    }

    /// <summary>
    /// The bus calls back on a thread of its own, so a click is queued and handed over by Poll,
    /// and what came between two polls comes as one.
    /// </summary>
    [Test]
    public async Task ClicksComeBackThroughPoll()
    {
        await using var bus = await PrivateBus.Start();
        using var watcher = await StandInWatcher.Start(bus.Address);
        using var tray = StatusNotifierTray<TestTrayIcon>.Open(Options(), bus.Address, out _)!;
        tray.Apply(new(TestTrayIcon.Idle, "Nothing pending", items));
        var accept = DbusMenuLayout.Build(items).All.Single(_ => _.ItemId == "accept");

        await watcher.Click(tray.BusName, accept.Id).WaitAsync(wait);
        await watcher.Activate(tray.BusName).WaitAsync(wait);

        var input = tray.Poll();
        await Assert.That(input.ClickedItem).IsEqualTo("accept");
        await Assert.That(input.IconClicked).IsTrue();
        await Assert.That(tray.Poll()).IsEqualTo(new TrayInput());
    }

    /// <summary>
    /// A stock GNOME has no watcher, and an app is told why rather than putting an icon nowhere.
    /// </summary>
    [Test]
    public async Task ThereIsNoTrayWithoutAWatcher()
    {
        await using var bus = await PrivateBus.Start();

        var tray = StatusNotifierTray<TestTrayIcon>.Open(Options(), bus.Address, out var error);

        await Assert.That(tray).IsNull();
        await Assert.That(error).Contains("AppIndicator");
    }
}
