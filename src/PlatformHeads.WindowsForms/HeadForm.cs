namespace PlatformHeads;

/// <summary>
/// The form a WinForms head draws in, pumped by the app's own frame loop rather than by
/// <see cref="Application.Run()"/>: a frame is one <see cref="Application.DoEvents"/>, which is what
/// keeps the loop shared with the native heads. Everything here is what a window has to do for
/// that loop whatever the app draws in it.
/// <list type="bullet">
/// <item>It opens where it was left (<see cref="WindowFit.Restorable"/>), or at its first size scaled
/// to the display and centred again for it.</item>
/// <item>It says where it is (<see cref="Placement"/>, <see cref="TakeSettled"/>), with a maximised
/// window remembered as maximised through being minimised.</item>
/// <item>Closing it is a request (<see cref="TakeCloseRequest"/>), since whether that hides or exits
/// is the app's rule, except when the session is ending.</item>
/// <item>It idles on the message queue and the loop's wake together (<see cref="WaitForFrame"/>).</item>
/// <item>It keeps frames coming while user32 holds the thread in a modal loop of its own
/// (<see cref="ModalFrame"/>).</item>
/// </list>
/// </summary>
public class HeadForm : Form
{
    readonly Size logicalSize;
    bool sized;
    Rectangle? restored;
    bool maximized;
    bool closeRequested;
    bool closingForReal;
    WindowPlacement? settled;
    FormWindowState settledState;
    WindowPlacement? lastPlacement;
    long lastFrame;

    /// <summary>
    /// A WinForms timer because its ticks are window messages, and a modal loop - the frame's while
    /// the window is moved or sized, a scroll bar's while it is held - still dispatches those, where
    /// it never returns to the loop that called DoEvents.
    /// </summary>
    readonly System.Windows.Forms.Timer modalFrames = new()
    {
        Interval = 16
    };

    /// <param name="title">The window's title.</param>
    /// <param name="logicalClientSize">The client size of a window opened for the first time, in
    /// logical pixels: scaled to the display it opens on once its handle exists.</param>
    /// <param name="placement">How the last window was left, to open this one the same way. Ignored
    /// when no display there is now can show it.</param>
    public HeadForm(string title, Size logicalClientSize, WindowPlacement? placement)
    {
        Text = title;
        logicalSize = logicalClientSize;
        ClientSize = logicalClientSize;
        StartPosition = FormStartPosition.CenterScreen;
        if (placement is { } saved &&
            WindowFit.Restorable(saved, WorkingAreas()) is { } bounds)
        {
            // Already in device pixels, on the display they were measured on, so not scaled again
            sized = true;
            restored = bounds;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            // Before the window is shown, so it comes up maximised rather than growing into it, on
            // the display its bounds are on. Those stay what it restores to.
            if (saved.Maximized)
            {
                maximized = true;
                WindowState = FormWindowState.Maximized;
            }
        }

        settledState = WindowState;
        modalFrames.Tick += (_, _) => ModalFrame?.Invoke();
    }

    static List<Rectangle> WorkingAreas() =>
        Screen.AllScreens
            .Select(_ => _.WorkingArea)
            .ToList();

    /// <summary>
    /// Once the handle exists, because DeviceDpi is only meaningful then. A subclass that lays out
    /// for the display calls this first.
    /// </summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!sized)
        {
            sized = true;
            var area = Screen.FromControl(this).WorkingArea;
            ClientSize = WindowFit.InitialClientSize(logicalSize, DeviceDpi, area.Size);

            // Centred again, for the size it has now. WinForms centres a window as it creates it,
            // which is before this, so it was centred for the size asked for in logical pixels and
            // then grew down and to the right from there: at 150% on a 1080p display the footer was
            // under the taskbar, and that was the placement remembered for every run after.
            if (StartPosition == FormStartPosition.CenterScreen &&
                WindowState == FormWindowState.Normal)
            {
                Location = WindowFit.Centred(area, Size);
            }
        }
        else if (restored is { } bounds &&
                 WindowState == FormWindowState.Normal &&
                 Bounds != bounds)
        {
            // Asked for before there was a handle, on a display whose scaling the form had not met
            // yet. Whatever creating it there did to the size, this is the size it had.
            Bounds = bounds;
        }

        restored = null;
    }

    /// <summary>
    /// Where the window is and whether it is maximised, to open the next one the same way, as
    /// <see cref="IHeadWindow{TScreen, TInput}.Placement"/> answers. Read from the window while
    /// there is one, and what that last came to once there is not: a session that is ending closes
    /// the window before the loop gets to ask.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public WindowPlacement? Placement
    {
        get
        {
            if (IsHandleCreated &&
                !IsDisposed)
            {
                // Maximised or minimised, the bounds are of neither: RestoreBounds is where the
                // window goes back to, which is the only size worth opening the next one at.
                var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                if (bounds is { Width: > 0, Height: > 0 })
                {
                    lastPlacement = new(bounds.X, bounds.Y, bounds.Width, bounds.Height, maximized);
                }
            }

            return lastPlacement;
        }
    }

    /// <summary>
    /// Where the window settled since the last call, or null when it has not moved: for an app that
    /// saves a placement as it changes rather than asking for one as the window goes. Each report
    /// is a save, so it comes at the end of a drag, at a maximise or restore, and at a hide, never
    /// for every frame of a move.
    /// </summary>
    protected WindowPlacement? TakeSettled()
    {
        var taken = settled;
        settled = null;
        return taken;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        // A minimised window's own state says only that it is minimised, and it comes back as
        // whichever it was before.
        if (WindowState != FormWindowState.Minimized)
        {
            maximized = WindowState == FormWindowState.Maximized;
        }
    }

    /// <summary>
    /// The end of a drag, by the title bar or an edge.
    /// </summary>
    protected override void OnResizeEnd(EventArgs e)
    {
        base.OnResizeEnd(e);
        Settle();
    }

    /// <summary>
    /// A maximise or a restore, which the caption buttons and a double click on the title bar do
    /// without the drag that ends in <see cref="OnResizeEnd"/>.
    /// </summary>
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (WindowState != settledState)
        {
            Settle();
        }
    }

    /// <summary>
    /// A hide, which is how the window is left every time: it catches what the two above do not,
    /// such as a snap to half the screen from the keyboard.
    /// </summary>
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible)
        {
            Settle();
        }
    }

    /// <summary>
    /// A minimised window leaves the last placement standing, since opening minimised would look
    /// like not opening at all.
    /// </summary>
    void Settle()
    {
        settledState = WindowState;
        if (WindowState == FormWindowState.Minimized ||
            !IsHandleCreated)
        {
            return;
        }

        settled = Placement;
    }

    /// <summary>
    /// Shows the window as it was before it was hidden or minimised: a window the user had maximised
    /// comes back maximised rather than at its restored size, and is then remembered as maximised.
    /// </summary>
    public void ShowRestored()
    {
        Visible = true;
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = maximized ? FormWindowState.Maximized : FormWindowState.Normal;
        }
    }

    /// <summary>
    /// Brings the window up, for something that wants reading. The restore is what BringToFront and
    /// Activate leave out: a minimised window stays minimised with its taskbar button flashing, and
    /// whatever it was being asked to show stays out of sight.
    /// </summary>
    public void Raise()
    {
        ShowRestored();
        BringToFront();
        Activate();
    }

    /// <summary>
    /// Shown without being made the window the keyboard goes to. For a form that is shown only so
    /// that it will paint and take messages, somewhere nobody can see it: a capture, and tests that
    /// post input to one. Shown as a window ordinarily is, it took the keyboard from whatever the
    /// person at the machine was typing into for as long as it was up.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Parked { get; set; }

    protected override bool ShowWithoutActivation =>
        Parked;

    /// <summary>
    /// Whether the user asked to close the window since the last call. The close is cancelled,
    /// because whether it means hide or exit is the app's rule; <see cref="CloseForReal"/> is how
    /// the answer comes back when it is exit.
    /// </summary>
    protected bool TakeCloseRequest()
    {
        var taken = closeRequested;
        closeRequested = false;
        return taken;
    }

    public void CloseForReal()
    {
        closingForReal = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Never cancelled when the session is ending: WinForms answers WM_QUERYENDSESSION with
        // !e.Cancel, so refusing made Windows report the app as preventing shutdown while it only
        // hid its window, and the user had to choose "Shut down anyway". Letting it through is safe,
        // because the loop sees the disposed form and returns, which runs its own shutdown.
        if (!closingForReal &&
            !EndsTheSession(e.CloseReason))
        {
            closeRequested = true;
            e.Cancel = true;
        }

        base.OnFormClosing(e);
    }

    /// <summary>
    /// The process is going away whatever this form says. Task Manager's End Task is here with
    /// shutdown because refusing it buys the same nothing: the user has already decided.
    /// </summary>
    public static bool EndsTheSession(CloseReason reason) =>
        reason is CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing;

    /// <summary>
    /// Run as the form closes because the session is ending. WinForms closes the form inside
    /// WM_ENDSESSION, and Windows may end the process as soon as that returns, before the loop has
    /// noticed the form is gone, let alone got through its own shutdown.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action? SessionEnding { get; set; }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (EndsTheSession(e.CloseReason))
        {
            SessionEnding?.Invoke();
        }

        base.OnFormClosed(e);
    }

    /// <summary>
    /// One frame of the loop, run on a timer while user32 holds the thread in a modal loop of its
    /// own: the window being moved or sized, or whatever a subclass enters with
    /// <see cref="EnterModal"/>. The loop's next step never comes while one runs, so without it the
    /// window froze, or drew the old screen into the new size, until the mouse came up.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action? ModalFrame { get; set; }

    /// <summary>
    /// A modal loop is starting, such as a scroll bar's from a press to its release.
    /// </summary>
    protected void EnterModal()
    {
        if (ModalFrame is not null &&
            !modalFrames.Enabled)
        {
            modalFrames.Start();
        }
    }

    protected void ExitModal() =>
        modalFrames.Stop();

    /// <summary>
    /// The loop is presenting a frame of its own, so nothing is holding the thread: whatever modal
    /// loop was entered has returned. Its exit is normally what says so, but a scroll sent to a bar
    /// by something other than a press, an accessibility tool say, need not be followed by an
    /// EndScroll, and frames would then come from the timer as well as the loop from there on.
    /// </summary>
    public void LoopReturned() =>
        ExitModal();

    const int enterSizeMove = 0x0231;
    const int exitSizeMove = 0x0232;

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == enterSizeMove)
        {
            EnterModal();
        }
        else if (message.Msg == exitSizeMove)
        {
            ExitModal();
        }

        base.WndProc(ref message);
    }

    /// <summary>
    /// Whether input is already waiting for a frame, so the loop takes the next frame now rather
    /// than idling until one is due: a key and a click that land together are two frames.
    /// </summary>
    [Browsable(false)]
    public virtual bool Pending => false;

    /// <summary>
    /// Whether something on the window moves by itself, such as a spinner, which turns only as the
    /// window repaints. While it does the loop idles no longer than a frame.
    /// </summary>
    protected virtual bool Animating => false;

    /// <summary>
    /// The WinForms <see cref="IHeadWindow{TScreen, TInput}.Wait"/>: idles on the message queue and
    /// <paramref name="wake"/> together, then handles whatever ended it. No sooner than a frame after
    /// the last one ended: a wheel or a drag sends messages far faster than sixty a second, and each
    /// frame they woke would build the screen again for a change no one could see.
    /// </summary>
    public void WaitForFrame(TimeSpan timeout, WaitHandle? wake)
    {
        if (!Pending)
        {
            if (Animating &&
                timeout > Pacing.Frame)
            {
                timeout = Pacing.Frame;
            }

            var since = Stopwatch.GetElapsedTime(lastFrame);
            if (since < Pacing.Frame)
            {
                var rest = Pacing.Frame - since;
                Thread.Sleep(rest);
                timeout -= rest;
            }

            MessageWait.For(wake, timeout);
        }

        Application.DoEvents();
        lastFrame = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// A field rather than a component, so it is not in the container Dispose walks.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            modalFrames.Dispose();
        }

        base.Dispose(disposing);
    }
}
