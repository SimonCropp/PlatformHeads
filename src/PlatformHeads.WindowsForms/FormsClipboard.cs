namespace PlatformHeads;

/// <summary>
/// The WinForms half of <see cref="IHeadWindow{TScreen, TInput}.SetClipboard"/>.
/// </summary>
public static class FormsClipboard
{
    /// <summary>
    /// Puts <paramref name="text"/> on the clipboard, or empties it for empty text, which SetText
    /// refuses. False when another process holds the clipboard open, which WinForms has already
    /// retried ten times over about a second: answered rather than thrown, and not retried here,
    /// since <see cref="ClipboardPump"/> offers it again on the next frames.
    /// </summary>
    /// <param name="text">What goes on the clipboard.</param>
    /// <param name="failure">Why it was not taken, for the app to log as it logs.</param>
    public static bool Set(string text, out Exception? failure)
    {
        failure = null;
        try
        {
            if (text.Length == 0)
            {
                Clipboard.Clear();
                return true;
            }

            // SetText and not SetDataObject: the toolkit's own attempts are most of what a clipboard
            // another process has open needs, and the pump has the rest.
            Clipboard.SetText(text);
            return true;
        }
        catch (ExternalException exception)
        {
            failure = exception;
            return false;
        }
    }
}
