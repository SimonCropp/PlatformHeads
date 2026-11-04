namespace PlatformHeads;

/// <summary>
/// Shows a file or a folder in the desktop's file manager: Explorer on Windows, the Finder on
/// macOS, and whatever <c>xdg-open</c> opens a folder with on Linux.
/// <para>
/// Never thrown: a file manager that will not start, or no file manager at all on a Linux machine,
/// is nothing an app can do anything about, and thrown from a click it reached the UI thread in
/// place of whatever the click was reporting. Off Windows the opener's output is kept from the
/// app's own, which on a terminal it would otherwise write over.
/// </para>
/// <para>
/// It was two, one in each app: DiffEngine's, which selected a file and knew what to do with one
/// not written yet, and BuildMonitor's, which opened a folder only while it was there.
/// </para>
/// </summary>
public static class FileManager
{
    /// <summary>
    /// The file selected in its folder, or the folder alone where it cannot be selected. False,
    /// with what went wrong, when the file manager could not be started. A file whose folder is not
    /// there either opens nothing, which is not a failure.
    /// </summary>
    public static bool TryReveal(string file, [NotNullWhen(false)] out Exception? error)
    {
        if (RevealTarget(file) is not var (target, select))
        {
            error = null;
            return true;
        }

        return TryStart(target, select, out error);
    }

    /// <summary>
    /// The folder opened, while it is there. A folder deleted since an app last looked opens
    /// nothing, which is not a failure, and is not created: opening an empty folder in its place
    /// would leave it behind.
    /// </summary>
    public static bool TryOpen(string directory, [NotNullWhen(false)] out Exception? error)
    {
        if (!Directory.Exists(directory))
        {
            error = null;
            return true;
        }

        return TryStart(directory, false, out error);
    }

    /// <summary>
    /// What revealing <paramref name="file" /> opens, and whether the file manager can be asked to
    /// select it, or null for nothing to open.
    /// <para>
    /// A path that is not there cannot be selected, and a file about to be written is one an app
    /// reveals often: DiffEngine reveals the target of a pending move, which for a new snapshot is
    /// where the file is going. Explorer answers that by opening the default folder, Documents,
    /// and <c>open -R</c> by failing, so the folder the file is about to be written into is what
    /// is opened instead.
    /// </para>
    /// </summary>
    public static (string Target, bool Select)? RevealTarget(string file)
    {
        if (File.Exists(file))
        {
            return (file, true);
        }

        var directory = Path.GetDirectoryName(file);
        if (directory is {Length: > 0} &&
            Directory.Exists(directory))
        {
            return (directory, false);
        }

        return null;
    }

    /// <summary>
    /// Explorer's <c>/select,</c> is one argument with the path quoted after the comma, which is
    /// not how a list of arguments would quote it. Linux has no file manager that every desktop can
    /// be asked to select a file in, so it opens the file's folder.
    /// </summary>
    static bool TryStart(string target, bool select, [NotNullWhen(false)] out Exception? error)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var explorer = Process.Start(
                    new ProcessStartInfo("explorer.exe", select ? $"/select,\"{target}\"" : $"\"{target}\"")
                    {
                        UseShellExecute = true
                    });
            }
            else
            {
                IReadOnlyList<string> arguments;
                string opener;
                if (OperatingSystem.IsMacOS())
                {
                    opener = "open";
                    arguments = select ? ["-R", target] : [target];
                }
                else
                {
                    opener = "xdg-open";
                    arguments = [select ? Path.GetDirectoryName(target) ?? target : target];
                }

                using var process = Process.Start(
                    new ProcessStartInfo(opener, arguments)
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    });
            }

            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception;
            return false;
        }
    }
}
