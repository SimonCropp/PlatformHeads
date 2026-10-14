namespace PlatformHeads;

/// <summary>
/// What came of offering a copy to the clipboard, from <see cref="ClipboardPump.Offer"/>.
/// </summary>
public enum ClipboardOffer
{
    /// <summary>
    /// The desktop took the text.
    /// </summary>
    Taken,

    /// <summary>
    /// The desktop would not take it this frame, and it is to be offered again on the next. A
    /// refusal changes no state, so a loop that idles has to come back a frame later by itself:
    /// see <see cref="Pacing.Idle"/>.
    /// </summary>
    Again,

    /// <summary>
    /// The desktop would not take it for <see cref="ClipboardPump.Attempts"/> frames, and the copy
    /// is to be dropped and said to have failed.
    /// </summary>
    GaveUp
}
