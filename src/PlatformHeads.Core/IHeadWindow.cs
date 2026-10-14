namespace PlatformHeads;

/// <summary>
/// One platform's renderer, and the only thing in an app that knows how pixels get drawn.
/// Everything above it is a pure function of the screen, which is what lets a text renderer and
/// its snapshots stand in for every implementation.
/// <para>
/// The frame contract is deliberately coarse: one <see cref="Present"/> carrying a whole screen
/// and one <see cref="Poll"/> per iteration, rather than a stream of draw calls. That is what
/// makes a retained mode toolkit and an immediate mode renderer equally implementable behind it.
/// </para>
/// <para>
/// An app's own window interface derives from this and adds what only it needs, such as a
/// directory chooser.
/// </para>
/// </summary>
public interface IHeadWindow<in TScreen, out TInput> : IDisposable
{
    /// <summary>
    /// Draws one frame. False once the window is gone for good, which is how the loop learns to
    /// stop. A hidden window keeps returning true; hiding is not closing.
    /// </summary>
    bool Present(TScreen screen);

    /// <summary>
    /// Idles after <see cref="Present"/> until the user does something, <paramref name="wake"/> is
    /// set, or <paramref name="timeout"/> passes, and answers whether it did. A loop that slept a
    /// frame at a time woke sixty times a second to find nothing new, which with the window open was
    /// nearly all of BuildMonitor's processor while nothing was building. A head that cannot wait on
    /// its input alongside a handle, as a native head whose frame call already waits a frame, answers
    /// false at once, and the loop paces itself.
    /// </summary>
    /// <param name="timeout">The longest to idle, from <see cref="Pacing.Idle"/>.</param>
    /// <param name="wake">Set when the state changes, or a command arrives, off the loop's thread.
    /// See <see cref="LoopWake{TState}"/>.</param>
    bool Wait(TimeSpan timeout, WaitHandle wake) =>
        false;

    /// <summary>
    /// Everything the user did since the last call. Drains as it reads, so each event arrives once.
    /// </summary>
    TInput Poll();

    void SetHidden(bool hidden);

    void Focus();

    /// <summary>
    /// Puts text on the system clipboard. Here rather than with an app's other IO because a
    /// clipboard belongs to a toolkit the way a window does: WinForms, AppKit and GLFW each own one,
    /// and the alternative was shelling out to whichever of <c>clip</c>, <c>pbcopy</c>,
    /// <c>xclip</c> or <c>wl-copy</c> happened to be installed.
    /// <para>
    /// False when the desktop would not take it, which on Windows is an ordinary outcome: one
    /// process owns the clipboard at a time, and a clipboard manager or a remote desktop session
    /// holding it fails the set outright. <see cref="ClipboardPump"/> is what does something about
    /// that, so an implementation only has to answer honestly rather than retry.
    /// </para>
    /// </summary>
    bool SetClipboard(string text);

    /// <summary>
    /// Where the window is and whether it is maximised, to open the next one the same way. Null
    /// from a head that cannot say, which is also what a window that never opened says.
    /// <para>
    /// Still answered once the window has gone, with how it was last seen: a session that is
    /// ending closes the window before the loop gets to ask.
    /// </para>
    /// </summary>
    WindowPlacement? Placement => null;

    /// <summary>
    /// Renders one frame offscreen to a PNG. Only pixel snapshots use this.
    /// </summary>
    bool Capture(TScreen screen, int width, int height, string pngPath);
}
