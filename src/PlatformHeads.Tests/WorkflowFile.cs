/// <summary>
/// Reads the few things of build-native.yml the tests need, by its lines: the body of a block
/// scalar under a key, which is all a step's script or the build script is. Not a YAML parser,
/// and it does not need one for a file whose shape these tests are the ones holding still.
/// </summary>
static class WorkflowFile
{
    static readonly string[] lines = File.ReadAllLines(Repository.Workflow);

    /// <summary>
    /// The block scalar under the first line that, trimmed, is <paramref name="key"/> at or after
    /// the first line that, trimmed, is <paramref name="after"/>: its lines with the block's own
    /// indentation taken off.
    /// </summary>
    public static List<string> Block(string after, string key) =>
        Block([after], key);

    /// <summary>
    /// As <see cref="Block(string, string)"/>, with each of <paramref name="after"/> found after
    /// the one before it: a step's name, say, after the name of its job, where two jobs have a
    /// step of that name.
    /// </summary>
    public static List<string> Block(string[] after, string key)
    {
        var start = 0;
        foreach (var anchor in after)
        {
            start = IndexOf(anchor, start);
        }

        var keyLine = IndexOf(key, start);
        var keyIndent = Indent(lines[keyLine]);
        var body = new List<string>();
        for (var index = keyLine + 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.Trim().Length == 0)
            {
                body.Add("");
                continue;
            }

            if (Indent(line) <= keyIndent)
            {
                break;
            }

            body.Add(line);
        }

        while (body.Count > 0 &&
               body[^1].Length == 0)
        {
            body.RemoveAt(body.Count - 1);
        }

        var indent = body.Where(_ => _.Length > 0).Min(Indent);
        return body
            .Select(_ => _.Length == 0 ? "" : _[indent..])
            .ToList();
    }

    static int IndexOf(string trimmed, int from)
    {
        for (var index = from; index < lines.Length; index++)
        {
            if (lines[index].Trim() == trimmed)
            {
                return index;
            }
        }

        throw new($"No line '{trimmed}' in {Repository.Workflow} from line {from + 1}");
    }

    static int Indent(string line) =>
        line.Length - line.TrimStart().Length;
}
