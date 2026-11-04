namespace PlatformHeads;

/// <summary>
/// Runs a program to completion with its output captured. Arguments go as a list, never a shell
/// line, so a value with a space or a quote in it arrives intact. Never throws for a program that
/// is missing or will not start: every caller is best effort.
/// <para>
/// Public since BuildMonitor kept a copy of its own while this was internal, for the two things it
/// lacked: a standard input, and <see cref="OnPath" />.
/// </para>
/// </summary>
public static class ProcessRunner
{
    /// <summary>
    /// The exit code and what the program wrote, or -1 and why when it could not be started or ran
    /// out of time. Both streams are read from the start, before any input is written, since a
    /// program that fills a pipe nobody reads stops before it answers: zenity writes GTK's warnings
    /// to standard error.
    /// </summary>
    /// <param name="file">The program, by path or by a name the platform finds it by.</param>
    /// <param name="arguments">Each handed over whole, however it is quoted.</param>
    /// <param name="timeout">
    /// Thirty seconds when null, after which the program is ended. One that waits on the person, as
    /// a dialog does, is given <see cref="Timeout.InfiniteTimeSpan" />.
    /// </param>
    /// <param name="input">Written to the program's standard input, which is then closed.</param>
    /// <returns>Standard output, or standard error when the program wrote nothing else.</returns>
    public static (int Code, string Output) Run(string file, IReadOnlyList<string> arguments, string? input = null, TimeSpan? timeout = null)
    {
        var info = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = input is not null,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(info) ??
                                throw new InvalidOperationException($"Could not start {file}");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (input is not null)
            {
                process.StandardInput.Write(input);
                process.StandardInput.Close();
            }

            if (!process.WaitForExit(timeout ?? TimeSpan.FromSeconds(30)))
            {
                process.Kill(true);
                return (-1, $"{file} timed out");
            }

            var text = output.Result;
            if (text.Length == 0)
            {
                text = error.Result;
            }

            return (process.ExitCode, text);
        }
        catch (Exception exception)
            when (exception is
                      System.ComponentModel.Win32Exception or
                      InvalidOperationException or
                      IOException)
        {
            return (-1, exception.Message);
        }
    }

    /// <summary>
    /// The first file of that name in a directory on <c>PATH</c>, or null for none. The name is
    /// taken as given, with no extension added, so it is for the programs of a Unix desktop.
    /// </summary>
    public static string? OnPath(string file)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (path is null)
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, file);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
