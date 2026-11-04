namespace PlatformHeads;

/// <summary>
/// How the last <see cref="ToolUpdate" /> went, for the app it started again to report. The update
/// runs in a shell nobody sees, after the app has exited, so without this there is nothing to see
/// at either end of it: the app goes away and comes back, and whether that is the new version or
/// the old one after a failure looks exactly the same.
/// </summary>
/// <param name="Succeeded">Whether <c>dotnet tool update</c> exited with success.</param>
/// <param name="Output">Everything it wrote.</param>
public sealed record ToolUpdateOutcome(bool Succeeded, string Output)
{
    /// <summary>
    /// The first line the shell writes, which says which of the two the rest of the file is.
    /// </summary>
    public const string SucceededLine = "ok";

    public const string FailedLine = "failed";

    /// <summary>
    /// The outcome <paramref name="path" /> holds, or null where there is none, which is every
    /// start that did not follow an update. Removed once read, so an outcome is reported once
    /// rather than at every start. Throws what reading or deleting the file throws, for the app
    /// to log.
    /// </summary>
    public static ToolUpdateOutcome? Take(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var text = File.ReadAllText(path);
        File.Delete(path);
        return Parse(text);
    }

    /// <summary>
    /// What the app the update started says about it, or null where no update started it, which
    /// is every start but the one after an update: <see cref="Take" />, with what it throws for a
    /// file that cannot be read or removed handed to <paramref name="failed" /> rather than out of
    /// the app's start, and the outcome handed to <paramref name="taken" /> before it is worded, for
    /// the app to log the whole of what <c>dotnet tool update</c> said, since a notification has
    /// room for a line of it.
    /// <para>
    /// Only an app that got past its single instance lock should ask, since asking removes the
    /// outcome, and a second instance leaving at once would take it from the one that stays.
    /// </para>
    /// <para>
    /// It was two, one in each app, with the same steps in different words.
    /// </para>
    /// </summary>
    /// <param name="path">Where the update wrote how it went, the <see cref="ToolUpdate" />'s outcome.</param>
    /// <param name="app">The app's name, as a notification's title says it.</param>
    /// <param name="version">The version running, which is the installed one.</param>
    /// <param name="taken">Told the outcome, for the app's log.</param>
    /// <param name="failed">Told why the outcome could not be read, for the app's log.</param>
    public static TrayNotification? Report(
        string path,
        string app,
        string version,
        Action<ToolUpdateOutcome>? taken = null,
        Action<Exception>? failed = null)
    {
        ToolUpdateOutcome? outcome;
        try
        {
            outcome = Take(path);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            failed?.Invoke(exception);
            return null;
        }

        if (outcome is null)
        {
            return null;
        }

        taken?.Invoke(outcome);
        return outcome.Notification(app, version);
    }

    /// <summary>
    /// The notification about this outcome. A success says the version rather than what the tool
    /// said, because the app showing it is the one the update started: it is the installed one,
    /// whether the update moved it or found it already current. And it is not shown as an error,
    /// which would make an update that worked look like one that had not. A failure says the
    /// <see cref="Reason" />.
    /// </summary>
    /// <param name="app">The app's name, as the title says it.</param>
    /// <param name="version">The version running.</param>
    public TrayNotification Notification(string app, string version)
    {
        if (Succeeded)
        {
            return new($"{app} updated", $"Now running {version}.", Kind: TrayNotificationKind.Info);
        }

        return new($"{app} did not update", Reason);
    }

    /// <summary>
    /// A file with no status line at all is a failure, the whole of it being the output.
    /// </summary>
    public static ToolUpdateOutcome Parse(string text)
    {
        var lines = text.Split('\n');
        var first = lines[0].Trim();
        if (first == SucceededLine)
        {
            return new(true, string.Join('\n', lines.Skip(1)));
        }

        if (first == FailedLine)
        {
            return new(false, string.Join('\n', lines.Skip(1)));
        }

        return new(false, text);
    }

    /// <summary>
    /// The last line <c>dotnet tool update</c> wrote, which is where it puts the cause, under a
    /// first line that only says the update failed.
    /// </summary>
    public string Reason
    {
        get
        {
            var last = Output
                .Split('\n')
                .Select(_ => _.Trim())
                .LastOrDefault(_ => _.Length > 0);
            return last ?? "dotnet tool update failed without saying why.";
        }
    }
}
