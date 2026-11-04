public class DesktopDialogTests
{
    const string message = "Files found: 2, in /home/a & <b>.\nDelete files?";

    /// <summary>
    /// What each program is handed is all there is to see of them without a desktop, so each
    /// argument is held to whole, quotes, ampersands and line breaks and all.
    /// </summary>
    [Test]
    public async Task AppleScriptsFolder()
    {
        await Assert.That(DesktopDialog.AppleScript.FolderArguments("Choose a folder", null))
            .IsEquivalentTo(
            [
                "-e", "on run argv",
                "-e", "return POSIX path of (choose folder with prompt (item 1 of argv))",
                "-e", "end run",
                "Choose a folder"
            ]);
        await Assert.That(DesktopDialog.AppleScript.FolderArguments("Choose a folder", "/Users/a \"b\""))
            .IsEquivalentTo(
            [
                "-e", "on run argv",
                "-e", "return POSIX path of (choose folder with prompt (item 1 of argv) default location (POSIX file (item 2 of argv)))",
                "-e", "end run",
                "Choose a folder",
                "/Users/a \"b\""
            ]);
    }

    [Test]
    public async Task AppleScriptsConfirm() =>
        await Assert.That(DesktopDialog.AppleScript.ConfirmArguments("Purge", message, "Delete"))
            .IsEquivalentTo(
            [
                "-e", "on run argv",
                "-e", """display dialog (item 1 of argv) with title (item 2 of argv) buttons {"Cancel", item 3 of argv} default button (item 3 of argv) cancel button "Cancel" with icon caution""",
                "-e", "end run",
                message,
                "Purge",
                "Delete"
            ]);

    /// <summary>
    /// The trailing separator is what opens zenity inside the folder, and one already there is not
    /// doubled.
    /// </summary>
    [Test]
    public async Task ZenitysFolder()
    {
        var separator = Path.DirectorySeparatorChar;
        await Assert.That(DesktopDialog.Zenity.FolderArguments("Choose a folder", null))
            .IsEquivalentTo(["--file-selection", "--directory", "--title=Choose a folder"]);
        await Assert.That(DesktopDialog.Zenity.FolderArguments("Choose a folder", $"{separator}home{separator}a"))
            .IsEquivalentTo(["--file-selection", "--directory", "--title=Choose a folder", $"--filename={separator}home{separator}a{separator}"]);
        await Assert.That(DesktopDialog.Zenity.FolderArguments("Choose a folder", $"{separator}home{separator}a{separator}"))
            .IsEquivalentTo(["--file-selection", "--directory", "--title=Choose a folder", $"--filename={separator}home{separator}a{separator}"]);
    }

    [Test]
    public async Task ZenitysConfirmHasNoMarkup() =>
        await Assert.That(DesktopDialog.Zenity.ConfirmArguments("Purge", message, "Delete"))
            .IsEquivalentTo(["--question", "--no-markup", "--title=Purge", $"--text={message}", "--ok-label=Delete", "--cancel-label=Cancel"]);

    /// <summary>
    /// kdialog has to be told where to open, and with no start folder it is the home folder.
    /// </summary>
    [Test]
    public async Task KdialogsFolder()
    {
        await Assert.That(DesktopDialog.Kdialog.FolderArguments("Choose a folder", null))
            .IsEquivalentTo(["--title", "Choose a folder", "--getexistingdirectory", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)]);
        await Assert.That(DesktopDialog.Kdialog.FolderArguments("Choose a folder", "/home/a"))
            .IsEquivalentTo(["--title", "Choose a folder", "--getexistingdirectory", "/home/a"]);
    }

    [Test]
    public async Task KdialogsConfirm() =>
        await Assert.That(DesktopDialog.Kdialog.ConfirmArguments("Purge", message, "Delete"))
            .IsEquivalentTo(["--title", "Purge", "--yes-label", "Delete", "--no-label", "Cancel", "--yesno", message]);

    /// <summary>
    /// What osascript would run, compiled by <c>osacompile</c> with nothing shown, which is as near
    /// as a test gets to AppleScript's dialogs: a script that does not compile fails before it asks
    /// anything. Run by the <c>macos</c> job.
    /// </summary>
    [Test]
    [RunOn(TUnit.Core.Enums.OS.MacOs)]
    public async Task TheScriptsCompile()
    {
        IReadOnlyList<string>[] scripts =
        [
            DesktopDialog.AppleScript.FolderArguments("Choose a folder", null),
            DesktopDialog.AppleScript.FolderArguments("Choose a folder", "/Users"),
            DesktopDialog.AppleScript.ConfirmArguments("Purge", message, "Delete")
        ];
        foreach (var arguments in scripts)
        {
            var compiled = Path.Combine(Path.GetTempPath(), $"PlatformHeads.Tests.{Guid.NewGuid():N}.scpt");
            try
            {
                var lines = arguments.Chunk(2).TakeWhile(_ => _[0] == "-e").SelectMany(_ => _);
                var (code, output) = ProcessRunner.Run("osacompile", ["-o", compiled, .. lines]);
                await Assert.That(code).IsEqualTo(0).Because(output);
            }
            finally
            {
                File.Delete(compiled);
            }
        }
    }

    [Test]
    [Arguments("KDE", "kdialog")]
    [Arguments("ubuntu:GNOME", "zenity")]
    [Arguments("kde:plasma", "kdialog")]
    [Arguments(null, "zenity")]
    public async Task EachDesktopsOwnFirst(string? desktop, string expected) =>
        await Assert.That(DesktopDialog.ForLinux(desktop, _ => true)!.Program).IsEqualTo(expected);

    [Test]
    public async Task EitherStandsInForTheOther()
    {
        await Assert.That(DesktopDialog.ForLinux("GNOME", _ => _ == "kdialog")!.Program).IsEqualTo("kdialog");
        await Assert.That(DesktopDialog.ForLinux("KDE", _ => _ == "zenity")!.Program).IsEqualTo("zenity");
    }

    [Test]
    public async Task NoneWhereNeitherIsInstalled() =>
        await Assert.That(DesktopDialog.ForLinux("GNOME", _ => false)).IsNull();

    /// <summary>
    /// Only the line break is taken off: a folder's name can end in a space.
    /// </summary>
    [Test]
    [Arguments("/home/a/project\n", "/home/a/project")]
    [Arguments("/Users/a/project/\n", "/Users/a/project")]
    [Arguments("/home/a/trailing \n", "/home/a/trailing ")]
    [Arguments("/\n", "/")]
    [Arguments("", null)]
    [Arguments("\n", null)]
    public async Task TheFolderAsPrinted(string output, string? expected) =>
        await Assert.That(DesktopDialog.FolderFrom(output)).IsEqualTo(expected);
}
