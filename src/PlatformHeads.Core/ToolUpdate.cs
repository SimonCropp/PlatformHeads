namespace PlatformHeads;

/// <summary>
/// Updates an app installed as a dotnet tool, and starts it again. The update has to run once the
/// app has exited, on Windows because a running executable cannot be replaced, and everywhere
/// because the app it then starts takes the ports and the lock of a single instance. So a shell is
/// started that waits for the app to go, runs <c>dotnet tool update</c> and starts the shim, and
/// the app exits once <see cref="Start" /> returns.
/// <para>
/// Nobody sees that shell, so how it went is written to <paramref name="outcome" /> for the app it
/// starts to report (<see cref="ToolUpdateOutcome" />). The shim is started again whether the
/// update worked or not: a failed update should leave the old version running, not no app at all.
/// </para>
/// <para>
/// It was BuildMonitor's, and DiffEngineTray needed the same for macOS and Linux.
/// </para>
/// </summary>
/// <param name="packageId">The tool's package.</param>
/// <param name="shim">
/// The tool's command, which <c>dotnet tool install</c> puts in the tool path and which stays the
/// same across updates. <see cref="ShimFor" /> finds it from a path under the store.
/// </param>
/// <param name="outcome">Where the shell writes how the update went.</param>
public sealed class ToolUpdate(string packageId, string shim, string outcome)
{
    /// <summary>
    /// How long the shell waits for the app to exit before updating anyway. Long enough for a
    /// shutdown that waits on a request in flight, short enough that an app which is never going
    /// to exit does not leave the user with no update and no reason for it.
    /// </summary>
    const int waitSeconds = 30;

    /// <summary>
    /// How long the Windows shell waits for the processes it stopped to go, since the files they
    /// hold are only released once they have.
    /// </summary>
    const int stopSeconds = 5;

    public string PackageId => packageId;

    public string Shim => shim;

    public string Outcome => outcome;

    /// <summary>
    /// The folder the shim is in, which the update is told with <c>--tool-path</c>. That is the
    /// one argument whichever way the tool was installed: the folder of a global install is a tool
    /// path too, and <c>--tool-path</c> with it updates the global install as <c>--global</c>
    /// does, which was seen on Windows and on Linux, where <c>--global</c> cannot reach an install
    /// made with <c>--tool-path</c>.
    /// </summary>
    public string ToolPath => Path.GetDirectoryName(shim) ?? "";

    /// <summary>
    /// Where the store keeps every installed version of the package, which the update empties.
    /// Matched without regard to case by the Windows script, since the store names the folder for
    /// the id in lower case.
    /// </summary>
    public string StoreDirectory => Path.Combine(ToolPath, ".store", packageId);

    /// <summary>
    /// Folders other than the store whose processes hold files the update replaces, stopped with
    /// the store's on Windows. An app that runs a copy of its head from somewhere fixed names that
    /// folder.
    /// </summary>
    public IReadOnlyList<string> AlsoStopUnder { get; init; } = [];

    /// <summary>
    /// The dotnet that runs this process, rather than whichever is first on the PATH. An app
    /// started at login has the login's PATH, and a macOS launch agent's is launchd's,
    /// <c>/usr/bin:/bin:/usr/sbin:/sbin</c>, with no dotnet on it: an update from a tray started
    /// at login could only fail. Seen from a launch agent on a GitHub macOS runner.
    /// </summary>
    public string Dotnet { get; init; } = RunningDotnet();

    /// <summary>
    /// Starts the shell and returns. Exiting is the caller's, and has to be: the shell waits for
    /// this process to go before it updates.
    /// </summary>
    public void Start()
    {
        using var process = Process.Start(StartInfo(Environment.ProcessId));
    }

    public ProcessStartInfo StartInfo(int processId)
    {
        if (OperatingSystem.IsWindows())
        {
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(WindowsScript(processId)));
            return new("powershell.exe")
            {
                ArgumentList =
                {
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Unrestricted",
                    "-EncodedCommand",
                    encoded
                },
                UseShellExecute = false,
                CreateNoWindow = true,
                // An app started from the store runs with its own folder under .store as the
                // working directory, and the shell would inherit it. Windows will not delete a
                // folder that is a process's working directory, so the uninstall the update starts
                // with would be denied the very version it is replacing, by the shell replacing it.
                WorkingDirectory = Path.GetTempPath()
            };
        }

        ProcessStartInfo info;
        if (OperatingSystem.IsMacOS())
        {
            // launchd ends a login item's whole process group as the item exits, which took the
            // shell with it and left no app at all, so the script runs as a job with job control
            // on, which puts it in a group of its own. /bin/bash rather than /bin/sh, which can be
            // set to another shell. Seen from a launch agent on a GitHub macOS runner: a plain
            // child was ended, and one started this way was not. A subshell rather than a group,
            // which takes no terminator: the script ends in `&`, and `&;` is no command.
            info = new("/bin/bash")
            {
                ArgumentList =
                {
                    "-c",
                    Job
                }
            };
        }
        else
        {
            // A session of its own, so nothing that ends the app's ends the shell.
            info = new("setsid")
            {
                ArgumentList =
                {
                    "/bin/sh",
                    "-c",
                    UnixScript
                }
            };
        }

        info.UseShellExecute = false;
        // The script's arguments, positional rather than written into it, so no path is quoted
        info.ArgumentList.Add("update");
        info.ArgumentList.Add(processId.ToString(CultureInfo.InvariantCulture));
        info.ArgumentList.Add(Dotnet);
        info.ArgumentList.Add(packageId);
        info.ArgumentList.Add(ToolPath);
        info.ArgumentList.Add(outcome);
        info.ArgumentList.Add(shim);
        return info;
    }

    /// <summary>
    /// Waits for the app to go, rather than for a guess at how long that takes, and silent where
    /// it already has, which is the usual case. Then stops whatever still holds the installed
    /// version, since the uninstall the update starts with deletes the whole version's folder and
    /// one open file under it fails all of it, as "Access to the path ... is denied": anything else
    /// started out of the store, or from <see cref="AlsoStopUnder" />.
    /// </summary>
    public string WindowsScript(int processId)
    {
        var quotedShim = Quote(shim);
        var under = string.Join(
            " -or ",
            AlsoStopUnder
                .Prepend(StoreDirectory)
                .Select(_ => $"$_.Path.StartsWith({Quote($@"{_.TrimEnd('\\')}\")}, 'OrdinalIgnoreCase')"));
        string[] steps =
        [
            $"Wait-Process -Id {processId} -Timeout {waitSeconds} -ErrorAction SilentlyContinue",
            $$"""$locking = @(Get-Process | Where-Object { $_.Path -and ($_.Path -eq {{quotedShim}} -or {{under}}) })""",
            "$locking | Stop-Process -Force -ErrorAction SilentlyContinue",
            $"$locking | Wait-Process -Timeout {stopSeconds} -ErrorAction SilentlyContinue",
            $$"""$output = & {{Quote(Dotnet)}} tool update {{packageId}} --tool-path {{Quote(ToolPath)}} --prerelease 2>&1 | ForEach-Object { "$_" }""",
            $$"""$status = if ($LASTEXITCODE -eq 0) { '{{ToolUpdateOutcome.SucceededLine}}' } else { '{{ToolUpdateOutcome.FailedLine}}' }""",
            $"Set-Content -LiteralPath {Quote(outcome)} -Value (@($status) + $output) -Encoding UTF8",
            $"& {quotedShim}"
        ];
        return string.Join("; ", steps);
    }

    static string Quote(string path) =>
        $"'{path.Replace("'", "''")}'";

    /// <summary>
    /// For macOS and Linux, where a running file can be replaced, so nothing is stopped. The app is
    /// still waited for: the shim started at the end takes the ports and the lock of a single
    /// instance, and with the app still holding them it would be the second instance, and leave
    /// the old version running and looking updated. The count bounds the wait as -Timeout does on
    /// Windows.
    /// <para>
    /// Its arguments are positional: $1 the app's process, $2 dotnet, $3 the package, $4 the tool
    /// path, $5 where the outcome goes and $6 the shim. One line, so no line ending a checkout
    /// gives the source reaches the shell.
    /// </para>
    /// </summary>
    public static string UnixScript { get; } = string.Join(
        "; ",
        "waited=0",
        $"while kill -0 \"$1\" 2>/dev/null && [ $waited -lt {waitSeconds * 5} ]; do sleep 0.2; waited=$((waited+1)); done",
        $"if output=$(\"$2\" tool update \"$3\" --tool-path \"$4\" --prerelease 2>&1); then status={ToolUpdateOutcome.SucceededLine}; else status={ToolUpdateOutcome.FailedLine}; fi",
        "printf '%s\\n%s\\n' \"$status\" \"$output\" > \"$5\"",
        "nohup \"$6\" < /dev/null > /dev/null 2>&1 &");

    /// <summary>
    /// What /bin/bash is handed on macOS: <see cref="UnixScript" /> as a job, with job control on.
    /// After it, since a static is set in the order it is written.
    /// </summary>
    public static string Job { get; } = $"set -m; ( {UnixScript} ) &";

    /// <summary>
    /// The shim of the tool whose store <paramref name="path" /> is under, or null for a path
    /// outside one, such as a build's output, with nothing to update. <c>dotnet tool install</c>
    /// puts a tool's files under <c>{tools}/.store/{package}/</c> and its command in
    /// <c>{tools}</c>, for <c>--global</c> and <c>--tool-path</c> alike.
    /// </summary>
    public static string? ShimFor(string path, string command)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var store = Array.IndexOf(segments, ".store");
        if (store <= 0)
        {
            return null;
        }

        var tools = string.Join(Path.DirectorySeparatorChar, segments[..store]);
        return Path.Combine(tools, OperatingSystem.IsWindows() ? $"{command}.exe" : command);
    }

    static string RunningDotnet()
    {
        // The runtime is {root}/shared/Microsoft.NETCore.App/{version}/, and the muxer is in {root}
        var root = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", ".."));
        var muxer = Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        if (File.Exists(muxer))
        {
            return muxer;
        }

        return "dotnet";
    }
}
