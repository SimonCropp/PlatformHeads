namespace PlatformHeads;

/// <summary>
/// Puts a window's icons on its window class as well, where the taskbar finds them without the
/// window's thread having to answer.
/// <para>
/// Explorer asks a window for its icon (WM_GETICON) as it makes the window's taskbar button, and
/// waits well under a second for the answer. A window that has not answered by then gets its
/// class's icon, or failing that Windows' stock application icon, and keeps it for as long as the
/// button lasts: pumping again later does not get it asked again. WinForms registers its classes
/// with no icon, and a form pumped by an app's own loop answers only when the loop next pumps. So
/// a head that showed its window and then did its startup before its first frame came up with the
/// stock icon while the window held its own: BuildMonitor's tray did, relaunched by its update.
/// A form shown and then left unpumped for a second was enough. The class's icon is read without
/// the window's thread, and with it set, that form came up with its own.
/// </para>
/// <para>
/// A class is shared by every window of the same styles in the process, child controls among them,
/// and only a top level window's is ever shown. So as the window goes, a slot is cleared only while
/// it still holds what this window put there, and another window's icon is left standing.
/// </para>
/// </summary>
sealed partial class ClassIcon
{
    const int destroy = 0x0002;
    const int setIcon = 0x0080;

    // WM_SETICON's WParam
    const int smallKind = 0;
    const int bigKind = 1;

    // GCLP_HICON and GCLP_HICONSM
    const int bigSlot = -14;
    const int smallSlot = -34;

    nint big;
    nint small;

    /// <summary>
    /// Every message, once the window has handled it. WM_SETICON is how WinForms gives the window
    /// its icons, as the handle is made and whenever Icon or ShowIcon changes, with zero for none,
    /// so following it keeps the class in step with the window whichever of those it was.
    /// </summary>
    public void Follow(Message message)
    {
        if (message.Msg == setIcon)
        {
            if (message.WParam == smallKind)
            {
                small = message.LParam;
                Set(message.HWnd, smallSlot, small);
            }
            else if (message.WParam == bigKind)
            {
                big = message.LParam;
                Set(message.HWnd, bigSlot, big);
            }

            return;
        }

        // A recreated handle is destroyed and then given its icons again, so this is for the window
        // going for good as well as for that.
        if (message.Msg == destroy)
        {
            Release(message.HWnd, bigSlot, big);
            Release(message.HWnd, smallSlot, small);
            big = 0;
            small = 0;
        }
    }

    static void Release(nint window, int slot, nint icon)
    {
        if (icon != 0 &&
            Get(window, slot) == icon)
        {
            Set(window, slot, 0);
        }
    }

    // SetClassLongPtr and GetClassLongPtr are macros over the 32 bit calls in a 32 bit user32,
    // which exports no Ptr form.
    static void Set(nint window, int slot, nint icon)
    {
        if (nint.Size == 8)
        {
            SetClassLongPtr(window, slot, icon);
            return;
        }

        SetClassLong(window, slot, (int) icon);
    }

    static nint Get(nint window, int slot)
    {
        if (nint.Size == 8)
        {
            return GetClassLongPtr(window, slot);
        }

        return (int) GetClassLong(window, slot);
    }

    // By their exported names: unlike DllImport, LibraryImport does not try the A and W suffixes.
    [LibraryImport("user32.dll", EntryPoint = "SetClassLongPtrW")]
    private static partial nint SetClassLongPtr(nint window, int index, nint value);

    [LibraryImport("user32.dll", EntryPoint = "SetClassLongW")]
    private static partial uint SetClassLong(nint window, int index, int value);

    [LibraryImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
    private static partial nint GetClassLongPtr(nint window, int index);

    [LibraryImport("user32.dll", EntryPoint = "GetClassLongW")]
    private static partial uint GetClassLong(nint window, int index);
}
