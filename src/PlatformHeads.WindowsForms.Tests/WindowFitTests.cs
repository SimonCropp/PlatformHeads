/// <summary>
/// Where a window opens, for arrangements of displays no test machine has.
/// </summary>
public class WindowFitTests
{
    static readonly Rectangle primary = new(0, 0, 1920, 1040);
    static readonly Rectangle right = new(1920, 0, 2560, 1400);

    static Rectangle? Restore(int x, int y, int width, int height, params Rectangle[] areas) =>
        WindowFit.Restorable(new(x, y, width, height), areas);

    [Test]
    public async Task AWindowOnADisplayOpensWhereItWas() =>
        await Assert.That(Restore(100, 80, 1100, 700, primary)).IsEqualTo(new Rectangle(100, 80, 1100, 700));

    /// <summary>
    /// A window snapped to an edge hangs a resize border past it, and tidying that inside would walk
    /// it across the screen a border at a time.
    /// </summary>
    [Test]
    public async Task ASnappedWindowIsLeftHangingOverTheEdge() =>
        await Assert.That(Restore(-7, 0, 974, 1047, primary)).IsEqualTo(new Rectangle(-7, 0, 974, 1047));

    [Test]
    public async Task AWindowOnTheSecondDisplayOpensThere() =>
        await Assert.That(Restore(2200, 100, 1100, 700, primary, right)).IsEqualTo(new Rectangle(2200, 100, 1100, 700));

    /// <summary>
    /// The laptop off its dock: the monitor the window was on has gone, so it opens as a new window.
    /// </summary>
    [Test]
    public async Task AWindowOnADisplayThatHasGoneOpensAsANewOne() =>
        await Assert.That(Restore(2200, 100, 1100, 700, primary)).IsNull();

    /// <summary>
    /// Mostly off its display, it is moved onto the one it overlaps most, at its own size.
    /// </summary>
    [Test]
    public async Task AWindowMostlyOffItsDisplayIsMovedOnto() =>
        await Assert.That(Restore(1500, 100, 1100, 700, primary)).IsEqualTo(new Rectangle(820, 100, 1100, 700));

    /// <summary>
    /// Its title bar above the top of every display is a window nobody can drag back.
    /// </summary>
    [Test]
    public async Task ATitleBarAboveTheDisplayIsBroughtDown() =>
        await Assert.That(Restore(100, -60, 1100, 700, primary)).IsEqualTo(new Rectangle(100, 0, 1100, 700));

    /// <summary>
    /// A display smaller than the window it is moved onto takes it at the display's size.
    /// </summary>
    [Test]
    public async Task AWindowLargerThanItsDisplayIsCutToIt() =>
        await Assert.That(Restore(1000, 0, 2400, 1300, primary)).IsEqualTo(new Rectangle(0, 0, 1920, 1040));

    [Test]
    public async Task AWindowTooSmallForAPersonToHaveLeftIsNotUsed() =>
        await Assert.That(Restore(100, 80, 120, 90, primary)).IsNull();

    [Test]
    [Arguments(96, 1100, 700)]
    [Arguments(144, 1650, 1050)]
    public async Task TheFirstSizeIsScaledToTheDisplay(int dpi, int width, int height) =>
        await Assert.That(WindowFit.InitialClientSize(new(1100, 700), dpi, new(3840, 2100))).IsEqualTo(new(width, height));

    /// <summary>
    /// 1100 by 700 at 150% is too tall for a 1080p screen once the taskbar is taken off.
    /// </summary>
    [Test]
    public async Task TheFirstSizeIsKeptInsideTheWorkingArea() =>
        await Assert.That(WindowFit.InitialClientSize(new(1100, 700), 144, new(1920, 1040))).IsEqualTo(new(1650, 936));

    [Test]
    public async Task AWindowIsCentred() =>
        await Assert.That(WindowFit.Centred(primary, new(1000, 600))).IsEqualTo(new(460, 220));

    /// <summary>
    /// One too large for the area starts at its top left, so the title bar is still in reach.
    /// </summary>
    [Test]
    public async Task AWindowTooLargeToCentreStartsAtTheTopLeft() =>
        await Assert.That(WindowFit.Centred(primary with { X = 100, Y = 50 }, new(3000, 2000))).IsEqualTo(new(100, 50));
}
