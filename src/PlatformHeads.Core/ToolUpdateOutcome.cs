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
