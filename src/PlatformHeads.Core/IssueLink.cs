namespace PlatformHeads;

/// <summary>
/// The URL of a new GitHub issue on an app's repository, with its title and body filled in, and
/// the memory of which failures have already been offered one this run.
/// </summary>
public sealed class IssueLink(string repository)
{
    readonly string repository = repository.TrimEnd('/');
    readonly ConcurrentDictionary<string, byte> raised = new();

    /// <summary>
    /// The title is encoded as the body is. One built from a message carrying a file path had a
    /// '#' in it, which started a fragment and dropped the body and the rest of the title, or an
    /// '&amp;', which started a parameter GitHub does not have and cut the title short there.
    /// </summary>
    public string Url(string title, string body) =>
        $"{repository}/issues/new?title={WebUtility.UrlEncode(title)}&body={WebUtility.UrlEncode(body)}";

    /// <summary>
    /// Whether <paramref name="message"/> is being raised for the first time this run, and so
    /// whether to offer an issue for it at all: a failure that repeats every poll otherwise opened
    /// a browser tab, or asked to, every poll.
    /// </summary>
    public bool FirstTime(string message) =>
        raised.TryAdd(message, 0);
}
