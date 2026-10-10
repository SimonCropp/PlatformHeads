/// <summary>
/// Runs a program to its end and hands back everything it wrote.
/// </summary>
static class Run
{
    public static async Task<RunResult> Program(
        string file,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var start = new ProcessStartInfo(file)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // What the test host was itself started with says which MSBuild a nested dotnet should
        // use, and it is the wrong one for a build that is not this one.
        foreach (var name in new[] {"MSBuildSDKsPath", "MSBUILD_EXE_PATH", "MSBuildExtensionsPath", "MSBuildLoadMicrosoftTargetsReadOnly"})
        {
            start.Environment.Remove(name);
        }

        if (environment is not null)
        {
            foreach (var (name, value) in environment)
            {
                start.Environment[name] = value;
            }
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new(process.ExitCode, await output + await error);
    }
}
