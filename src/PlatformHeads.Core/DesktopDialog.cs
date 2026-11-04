namespace PlatformHeads;

/// <summary>
/// A question put by a program the desktop has, for a head with no toolkit of its own to ask
/// with: AppleScript's dialogs on macOS, and zenity's or kdialog's on Linux, as
/// <see cref="DesktopNotification" /> pops a balloon. Neither Linux desktop's chooser can be drawn
/// convincingly in Dear ImGui, and every one already ships one as a program. Each question waits
/// for as long as it is left unanswered, so none is asked on the thread a tray's menu or a
/// window's loop runs on.
/// <para>
/// Null rather than an exception where nothing is installed, which is ordinary on a minimal Linux
/// desktop: an app keeps a way to go on without asking, such as a path typed in.
/// </para>
/// <para>
/// It was two, one in each app: DiffEngineTray's, which took KDE's own on KDE and turned zenity's
/// markup off, and BuildMonitor's folder chooser, which opened where the folder already was.
/// </para>
/// </summary>
public sealed class DesktopDialog
{
    const string cancel = "Cancel";

    readonly Func<string, string?, IReadOnlyList<string>> folderArguments;
    readonly Func<string, string, string, IReadOnlyList<string>> confirmArguments;

    DesktopDialog(
        string program,
        Func<string, string?, IReadOnlyList<string>> folderArguments,
        Func<string, string, string, IReadOnlyList<string>> confirmArguments)
    {
        Program = program;
        this.folderArguments = folderArguments;
        this.confirmArguments = confirmArguments;
    }

    public string Program { get; }

    /// <summary>
    /// What the program is handed to ask for a folder.
    /// </summary>
    /// <param name="prompt">The chooser's title, or its prompt where it has no title.</param>
    /// <param name="start">The folder it opens in, or null for its own choice.</param>
    public IReadOnlyList<string> FolderArguments(string prompt, string? start) =>
        folderArguments(prompt, start);

    /// <summary>
    /// What the program is handed to ask whether to go ahead.
    /// </summary>
    /// <param name="title">The dialog's title.</param>
    /// <param name="message">What is asked, as plain text, line breaks included.</param>
    /// <param name="accept">The label of the button that goes ahead, beside Cancel.</param>
    public IReadOnlyList<string> ConfirmArguments(string title, string message, string accept) =>
        confirmArguments(title, message, accept);

    /// <summary>
    /// Arguments go to the script as argv rather than into its text, so a path or a message with a
    /// quote in it needs no escaping. A confirmation is for a step that cannot be undone, so it has
    /// the caution icon.
    /// </summary>
    public static DesktopDialog AppleScript { get; } = new(
        "osascript",
        (prompt, start) => start is null
            ?
            [
                "-e", "on run argv",
                "-e", "return POSIX path of (choose folder with prompt (item 1 of argv))",
                "-e", "end run",
                prompt
            ]
            :
            [
                "-e", "on run argv",
                "-e", "return POSIX path of (choose folder with prompt (item 1 of argv) default location (POSIX file (item 2 of argv)))",
                "-e", "end run",
                prompt,
                start
            ],
        (title, message, accept) =>
        [
            "-e", "on run argv",
            "-e", $$"""display dialog (item 1 of argv) with title (item 2 of argv) buttons {"{{cancel}}", item 3 of argv} default button (item 3 of argv) cancel button "{{cancel}}" with icon caution""",
            "-e", "end run",
            message,
            title,
            accept
        ]);

    /// <summary>
    /// The trailing separator on a start folder is what tells zenity to open inside it rather than
    /// beside it with it selected. zenity reads a question's text as Pango markup, in which a path
    /// with an ampersand or an angle bracket in it is an error, so markup is turned off.
    /// </summary>
    public static DesktopDialog Zenity { get; } = new(
        "zenity",
        (prompt, start) => start is null
            ? ["--file-selection", "--directory", $"--title={prompt}"]
            : ["--file-selection", "--directory", $"--title={prompt}", $"--filename={Path.TrimEndingDirectorySeparator(start)}{Path.DirectorySeparatorChar}"],
        (title, message, accept) =>
        [
            "--question",
            "--no-markup",
            $"--title={title}",
            $"--text={message}",
            $"--ok-label={accept}",
            $"--cancel-label={cancel}"
        ]);

    /// <summary>
    /// kdialog has to be told where to open, so with no start folder it is the home folder.
    /// </summary>
    public static DesktopDialog Kdialog { get; } = new(
        "kdialog",
        (prompt, start) => ["--title", prompt, "--getexistingdirectory", start ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)],
        (title, message, accept) =>
        [
            "--title", title,
            "--yes-label", accept,
            "--no-label", cancel,
            "--yesno", message
        ]);

    /// <summary>
    /// The one this platform has, or null for none to ask with, which is every Windows machine,
    /// where an app has a toolkit to ask with. osascript is part of macOS.
    /// </summary>
    public static DesktopDialog? ForPlatform()
    {
        if (OperatingSystem.IsMacOS())
        {
            return AppleScript;
        }

        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        return ForLinux(
            Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"),
            _ => ProcessRunner.OnPath(_) is not null);
    }

    /// <summary>
    /// KDE's own on KDE, where zenity's would be a GTK window among Qt ones, and zenity's
    /// everywhere else, either standing in for the other where only one is installed.
    /// <c>XDG_CURRENT_DESKTOP</c> is a list, such as <c>ubuntu:GNOME</c>.
    /// </summary>
    public static DesktopDialog? ForLinux(string? desktop, Func<string, bool> installed)
    {
        var kde = desktop?
            .Split(':')
            .Contains("KDE", StringComparer.OrdinalIgnoreCase) == true;
        DesktopDialog[] order = kde ? [Kdialog, Zenity] : [Zenity, Kdialog];
        return order.FirstOrDefault(_ => installed(_.Program));
    }

    /// <summary>
    /// The folder chosen, or null for none: all three exit with something other than 0 for a
    /// cancel, and a program that would not start is the same as a cancel. A start folder that is
    /// not there is left out rather than handed over, and so is an answer that is not a folder.
    /// </summary>
    public string? PickFolder(string prompt, string? start = null)
    {
        if (!Directory.Exists(start))
        {
            start = null;
        }

        var (code, output) = ProcessRunner.Run(Program, FolderArguments(prompt, start), timeout: Timeout.InfiniteTimeSpan);
        if (code != 0 ||
            FolderFrom(output) is not { } folder ||
            !Directory.Exists(folder))
        {
            return null;
        }

        return folder;
    }

    /// <summary>
    /// Whether the person chose <paramref name="accept" />. A cancel, closing the dialog, and a
    /// program that would not start are all no.
    /// </summary>
    public bool Confirm(string title, string message, string accept) =>
        ProcessRunner.Run(Program, ConfirmArguments(title, message, accept), timeout: Timeout.InfiniteTimeSpan).Code == 0;

    /// <summary>
    /// The path as printed, a line of its own, and from AppleScript with a separator after it.
    /// Only the line break is taken off, since a folder's name can end in a space.
    /// </summary>
    public static string? FolderFrom(string output)
    {
        var path = output.TrimEnd('\r', '\n');
        if (path.Length == 0)
        {
            return null;
        }

        return Path.TrimEndingDirectorySeparator(path);
    }
}
