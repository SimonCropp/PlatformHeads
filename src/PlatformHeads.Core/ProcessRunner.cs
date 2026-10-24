namespace PlatformHeads;

/// <summary>
/// Runs a program to completion with its output captured. Arguments go as a list, never a shell
/// line, so a value with a space or a quote in it arrives intact. Never throws for a program that
/// is missing or will not start: every caller is best effort.
/// </summary>
static class ProcessRunner
{
    public static (int Code, string Output) Run(string file, IReadOnlyList<string> arguments, TimeSpan? timeout = null)
    {
        var info = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
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
            if (!process.WaitForExit((int) (timeout ?? TimeSpan.FromSeconds(30)).TotalMilliseconds))
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
}
