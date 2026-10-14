/// <summary>
/// What the base form does for the loop, on real windows, shown without taking the keyboard.
/// </summary>
[TUnit.Core.Executors.STAThreadExecutor]
public class HeadFormTests
{
    /// <summary>
    /// Whether closing means hide or exit is the app's rule, so the close is cancelled and reported.
    /// </summary>
    [Test]
    public async Task AClosingWindowIsAskedAboutNotClosed()
    {
        using var form = new TestForm();
        form.ShowParked();

        form.Close();

        await Assert.That(form.IsDisposed).IsFalse();
        await Assert.That(form.CloseRequest()).IsTrue();
        // Once.
        await Assert.That(form.CloseRequest()).IsFalse();
    }

    [Test]
    public async Task CloseForRealCloses()
    {
        var form = new TestForm();
        form.ShowParked();

        form.CloseForReal();

        await Assert.That(form.IsDisposed).IsTrue();
    }

    /// <summary>
    /// Refusing a shutdown made Windows report the app as preventing it.
    /// </summary>
    [Test]
    [Arguments(CloseReason.WindowsShutDown, true)]
    [Arguments(CloseReason.TaskManagerClosing, true)]
    [Arguments(CloseReason.UserClosing, false)]
    [Arguments(CloseReason.ApplicationExitCall, false)]
    public async Task OnlyShutdownAndEndTaskEndTheSession(CloseReason reason, bool ends) =>
        await Assert.That(HeadForm.EndsTheSession(reason)).IsEqualTo(ends);

    /// <summary>
    /// Minimised and brought back, a maximised window comes back maximised, and is remembered so.
    /// It came back at its restored size, and was then saved as one the user had not maximised.
    /// </summary>
    [Test]
    public async Task AMaximisedWindowComesBackMaximised()
    {
        using var form = new TestForm();
        form.ShowParked();
        form.WindowState = FormWindowState.Maximized;
        form.WindowState = FormWindowState.Minimized;

        form.ShowRestored();

        await Assert.That(form.WindowState).IsEqualTo(FormWindowState.Maximized);
        await Assert.That(form.Placement?.Maximized ?? false).IsTrue();
    }

    /// <summary>
    /// Maximised, the placement is the size it restores to, which is the only one worth opening at.
    /// </summary>
    [Test]
    public async Task AMaximisedWindowsPlacementIsItsRestoredSize()
    {
        using var form = new TestForm();
        form.ShowParked();
        var normal = form.Bounds;

        form.WindowState = FormWindowState.Maximized;

        await Assert.That(form.Placement).IsEqualTo(new WindowPlacement(normal.X, normal.Y, normal.Width, normal.Height, true));
    }

    /// <summary>
    /// A session that is ending closes the window before the loop gets to ask where it was.
    /// </summary>
    [Test]
    public async Task APlacementIsStillAnsweredOnceTheWindowHasGone()
    {
        var form = new TestForm();
        form.ShowParked();
        var placement = form.Placement;

        form.CloseForReal();

        await Assert.That(form.Placement).IsEqualTo(placement);
    }

    [Test]
    public async Task AWindowOpensWhereItWasLeft()
    {
        var area = Screen.PrimaryScreen!.WorkingArea;
        var left = new WindowPlacement(area.X + 40, area.Y + 30, 700, 500);
        using var form = new TestForm(left);
        form.ShowParked();

        await Assert.That(form.Bounds).IsEqualTo(new Rectangle(left.X, left.Y, left.Width, left.Height));
        await Assert.That(form.Placement).IsEqualTo(left);
    }

    [Test]
    public async Task AWindowLeftMaximisedOpensMaximised()
    {
        var area = Screen.PrimaryScreen!.WorkingArea;
        using var form = new TestForm(new(area.X + 40, area.Y + 30, 700, 500, Maximized: true));
        form.ShowParked();

        await Assert.That(form.WindowState).IsEqualTo(FormWindowState.Maximized);
        await Assert.That(form.Placement).IsEqualTo(new WindowPlacement(area.X + 40, area.Y + 30, 700, 500, true));
    }

    /// <summary>
    /// A hide is how the window is left every time, so it is reported as settled, once.
    /// </summary>
    [Test]
    public async Task AHideIsReportedAsWhereTheWindowSettled()
    {
        using var form = new TestForm();
        form.ShowParked();
        form.Settled();

        form.Hide();

        await Assert.That(form.Settled()).IsEqualTo(form.Placement);
        await Assert.That(form.Settled()).IsNull();
    }

    [Test]
    public async Task AMaximiseIsReportedAsWhereTheWindowSettled()
    {
        using var form = new TestForm();
        form.ShowParked();
        form.Settled();

        form.WindowState = FormWindowState.Maximized;

        await Assert.That(form.Settled()?.Maximized ?? false).IsTrue();
    }

    /// <summary>
    /// Minimised, nothing is reported: opening minimised would look like not opening at all.
    /// </summary>
    [Test]
    public async Task AMinimiseIsNotReported()
    {
        using var form = new TestForm();
        form.ShowParked();
        form.Settled();

        form.WindowState = FormWindowState.Minimized;

        await Assert.That(form.Settled()).IsNull();
    }

    /// <summary>
    /// The wake ends the idle, which is how a poll's result or a command is drawn at once.
    /// </summary>
    [Test]
    public async Task AWakeEndsTheWait()
    {
        using var form = new TestForm();
        form.ShowParked();
        using var wake = new AutoResetEvent(true);
        form.WaitForFrame(TimeSpan.Zero, null);

        var stopwatch = Stopwatch.StartNew();
        form.WaitForFrame(TimeSpan.FromSeconds(5), wake);

        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Input already waiting is the next frame's, now.
    /// </summary>
    [Test]
    public async Task InputWaitingIsNotWaitedFor()
    {
        using var form = new TestForm { InputWaiting = true };
        form.ShowParked();
        using var wake = new AutoResetEvent(false);

        var stopwatch = Stopwatch.StartNew();
        form.WaitForFrame(TimeSpan.FromSeconds(5), wake);

        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// A spinner turns only as the window repaints, so the idle is a frame while one is up.
    /// </summary>
    [Test]
    public async Task ASpinnerKeepsTheWaitToAFrame()
    {
        using var form = new TestForm { Spinning = true };
        form.ShowParked();
        using var wake = new AutoResetEvent(false);
        DrainMessages(form);

        var stopwatch = Stopwatch.StartNew();
        form.WaitForFrame(TimeSpan.FromSeconds(5), wake);

        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// The timer's ticks are messages, which a modal loop still dispatches, so frames come from it
    /// for as long as one is entered and stop when it ends.
    /// </summary>
    [Test]
    public async Task FramesComeFromTheTimerInsideAModalLoop()
    {
        using var form = new TestForm();
        var frames = 0;
        form.ModalFrame = () => frames++;
        form.ShowParked();

        form.StartModal();
        Pump(TimeSpan.FromMilliseconds(400));
        form.StopModal();
        var during = frames;
        Pump(TimeSpan.FromMilliseconds(200));

        await Assert.That(during).IsGreaterThan(0);
        await Assert.That(frames).IsEqualTo(during);
    }

    /// <summary>
    /// The loop presenting a frame of its own means no modal loop is holding the thread.
    /// </summary>
    [Test]
    public async Task TheLoopReturningStopsTheModalFrames()
    {
        using var form = new TestForm();
        var frames = 0;
        form.ModalFrame = () => frames++;
        form.ShowParked();
        form.StartModal();

        form.LoopReturned();
        Pump(TimeSpan.FromMilliseconds(200));

        await Assert.That(frames).IsEqualTo(0);
    }

    /// <summary>
    /// Shown parked, the form is not the window the keyboard goes to.
    /// </summary>
    [Test]
    public async Task AParkedFormDoesNotTakeTheKeyboard()
    {
        using var form = new TestForm();

        form.ShowParked();

        await Assert.That(Form.ActiveForm).IsNotSameReferenceAs(form);
    }

    static void DrainMessages(HeadForm form)
    {
        for (var i = 0; i < 20; i++)
        {
            Application.DoEvents();
        }

        form.WaitForFrame(TimeSpan.Zero, null);
        Thread.Sleep(Pacing.Frame);
    }

    static void Pump(TimeSpan length)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < length)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }
}
