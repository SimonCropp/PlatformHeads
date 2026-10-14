namespace PlatformHeads;

/// <summary>
/// Offers a copy to the clipboard a frame at a time until the desktop takes it or a few frames
/// pass, so the state lets go of the text only once it has been taken.
/// <para>
/// BuildMonitor's loop used to clear the copy first, on the reasoning that something that throws
/// should not be asked again sixty times a second. A clipboard is not like that. On Windows one
/// process owns it at a time, so a set while a clipboard manager, a remote desktop session or a
/// browser holds it fails outright, and the toolkit's own retry is only about a second long.
/// Clearing first meant those seconds cost the user what had been copied, while the status line
/// still said it was copied.
/// </para>
/// </summary>
public sealed class ClipboardPump
{
    /// <summary>
    /// How many frames one copy gets before the pump gives up on it. Each Windows attempt already
    /// retries inside the toolkit for about a second, so this is a few seconds of a clipboard
    /// someone else is holding, and then a status that says so rather than a silent drop.
    /// </summary>
    public const int Attempts = 3;

    object? pending;

    /// <summary>
    /// How many times the copy last offered has been tried, for saying so when it is given up on.
    /// </summary>
    public int Tries { get; private set; }

    /// <summary>
    /// Offers this frame's copy. <paramref name="copy"/> is compared by reference: a second copy
    /// arriving while the first is still being tried is a new copy, and starts its own attempts
    /// rather than inheriting a count that would give up on it at once.
    /// </summary>
    /// <param name="copy">What identifies this copy in the state, such as the record that holds
    /// <paramref name="text"/>.</param>
    /// <param name="text">What goes on the clipboard.</param>
    /// <param name="setClipboard">The window's <see cref="IHeadWindow{TScreen, TInput}.SetClipboard"/>.</param>
    /// <returns>Whether the copy was taken, is to be offered again on the next frame, or has been
    /// given up on. On either of the first and last the state lets go of it.</returns>
    public ClipboardOffer Offer(object copy, string text, Func<string, bool> setClipboard)
    {
        if (!ReferenceEquals(pending, copy))
        {
            pending = copy;
            Tries = 0;
        }

        Tries++;
        if (setClipboard(text))
        {
            pending = null;
            return ClipboardOffer.Taken;
        }

        if (Tries < Attempts)
        {
            return ClipboardOffer.Again;
        }

        pending = null;
        return ClipboardOffer.GaveUp;
    }
}
