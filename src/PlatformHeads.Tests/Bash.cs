/// <summary>
/// Runs a workflow step's script the way GitHub runs a bash step.
/// </summary>
static class Bash
{
    /// <summary>
    /// Git's on Windows, by where Git for Windows puts it: the bash on a Windows PATH is as
    /// likely to be WSL's launcher, which runs the script in a distribution that cannot see it.
    /// </summary>
    public static string? Find()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "bash";
        }

        var git = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe");
        return File.Exists(git) ? git : null;
    }

    public static async Task<RunResult> Run(string bash, string script, string workingDirectory)
    {
        var file = Path.Combine(Path.GetTempPath(), $"PlatformHeads.Tests.{Guid.NewGuid():N}.sh");
        await File.WriteAllTextAsync(file, script);
        try
        {
            // The flags GitHub runs a bash step with.
            return await global::Run.Program(bash, ["--noprofile", "--norc", "-eo", "pipefail", file], workingDirectory);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
