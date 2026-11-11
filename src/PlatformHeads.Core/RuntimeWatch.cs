namespace PlatformHeads;

/// <summary>
/// Starts an app again once the .NET runtime it is running on has been deleted under it, which a
/// servicing update does: installing 10.0.15 took away the 10.0.14 that DiffEngineTray had started
/// on. The process lives on with what it has loaded, and anything it has not fails, as a
/// <see cref="FileNotFoundException" /> for an assembly of the framework. Seen on Windows: the
/// tray's icon was gone from the notification area and every move a test run sent was dropped,
/// failing to load System.Diagnostics.Process, while the tray went on holding its single instance
/// lock, so starting it again did nothing.
/// <para>
/// So the folders the framework was loaded from are looked at every few seconds. Once one has
/// gone and another version of the same major is beside it, which the app would roll forward to,
/// a shell is started that waits for this process to exit and then starts it again by
/// <see cref="Environment.ProcessPath" />: an installed tool's command, which picks the runtime as
/// it starts, or a build's apphost. Then the app is told to exit. A folder gone with nothing to
/// take its place is waited on, since a start then would fail.
/// </para>
/// <para>
/// The shell is <see cref="ToolUpdate" />'s without the update, for the same reasons: it waits for
/// the app because the app holds the lock and ports of a single instance, it is in a session or
/// group of its own so nothing that ends the app ends it, and its arguments are positional.
/// </para>
/// <para>
/// Everything starting the shell needs is loaded beforehand (<see cref="Preload" />), because once
/// the runtime has gone, loading is the one thing the process cannot do.
/// </para>
/// </summary>
public sealed class RuntimeWatch : IDisposable
{
    /// <summary>
    /// How long the shell waits for the app to exit before starting it anyway. The app started then
    /// finds the single instance lock held and exits, which is no worse than not starting one.
    /// </summary>
    const int waitSeconds = 30;

    readonly IReadOnlyList<string> frameworks;
    readonly string command;
    readonly Action exit;
    readonly Action<string>? told;
    readonly Action<Exception>? failed;
    readonly Timer timer;
    int done;
    bool waitTold;

    RuntimeWatch(IReadOnlyList<string> frameworks, string command, Action exit, Action<string>? told, Action<Exception>? failed, TimeSpan interval)
    {
        this.frameworks = frameworks;
        this.command = command;
        this.exit = exit;
        this.told = told;
        this.failed = failed;
        timer = new(_ => Check(), null, interval, interval);
    }

    /// <summary>
    /// Watches the framework folders loaded so far, so call it once the app's toolkit is loaded:
    /// the WinForms of a Windows head is in a framework of its own.
    /// </summary>
    /// <param name="exit">
    /// Ends the app as its own Exit does. Called on a thread pool thread, once, after the shell
    /// has started.
    /// </param>
    /// <param name="told">What is happening, for the app's log: waiting for a runtime, or starting again.</param>
    /// <param name="failed">Told what loading ahead or starting the shell threw. The app is left running.</param>
    /// <param name="interval">How often the folders are looked at.</param>
    /// <returns>
    /// Null for an app run as <c>dotnet app.dll</c>, which has no command of its own to start again,
    /// and for one with no shared framework, as a self contained build has none to lose.
    /// </returns>
    public static RuntimeWatch? Start(Action exit, Action<string>? told = null, Action<Exception>? failed = null, TimeSpan? interval = null)
    {
        var command = Environment.ProcessPath;
        if (command is null ||
            Path.GetFileNameWithoutExtension(command).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var frameworks = FrameworkDirectories(AppDomain.CurrentDomain.GetAssemblies());
        if (frameworks.Count == 0)
        {
            return null;
        }

        Preload(command, failed);
        return new(frameworks, command, exit, told, failed, interval ?? TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// The folders of a shared framework the assemblies were loaded from:
    /// <c>{root}/shared/{framework}/{version}/</c>, once each.
    /// </summary>
    public static IReadOnlyList<string> FrameworkDirectories(IEnumerable<Assembly> assemblies)
    {
        var directories = new List<string>();
        foreach (var assembly in assemblies)
        {
            if (assembly.IsDynamic ||
                Path.GetDirectoryName(assembly.Location) is not { Length: > 0 } directory)
            {
                continue;
            }

            var shared = Path.GetDirectoryName(Path.GetDirectoryName(directory));
            if (shared is null ||
                !Path.GetFileName(shared).Equals("shared", StringComparison.OrdinalIgnoreCase) ||
                directories.Contains(directory, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            directories.Add(directory);
        }

        return directories;
    }

    /// <summary>
    /// Whether a framework folder has gone.
    /// </summary>
    public static bool Gone(IReadOnlyList<string> frameworks) =>
        frameworks.Any(_ => !Directory.Exists(_));

    /// <summary>
    /// Whether a framework folder has gone and every one that has has another version of its major
    /// beside it. Every one, because a Windows head also runs on the desktop framework, which an
    /// update replaces after the core one: started in between, the app would not start.
    /// </summary>
    public static bool Replaced(IReadOnlyList<string> frameworks) =>
        Gone(frameworks) &&
        frameworks.All(_ => Directory.Exists(_) || HasReplacement(_));

    /// <summary>
    /// Another version of the same major, lower as well as higher: an app built for a major rolls
    /// forward to any of it.
    /// </summary>
    static bool HasReplacement(string directory)
    {
        if (!Version.TryParse(Path.GetFileName(directory), out var version))
        {
            return false;
        }

        var framework = Path.GetDirectoryName(directory);
        if (framework is null ||
            !Directory.Exists(framework))
        {
            return false;
        }

        foreach (var sibling in Directory.EnumerateDirectories(framework))
        {
            if (Version.TryParse(Path.GetFileName(sibling), out var other) &&
                other.Major == version.Major &&
                other != version)
            {
                return true;
            }
        }

        return false;
    }

    void Check()
    {
        if (Volatile.Read(ref done) == 1)
        {
            return;
        }

        try
        {
            if (!Replaced(frameworks))
            {
                if (!waitTold &&
                    Gone(frameworks))
                {
                    waitTold = true;
                    told?.Invoke("The runtime this app runs on has been removed, and there is no other version of it yet to start again on. Waiting for one.");
                }

                return;
            }

            if (Interlocked.Exchange(ref done, 1) == 1)
            {
                return;
            }

            timer.Dispose();
            told?.Invoke($"The runtime this app runs on has been removed. Starting {command} again and exiting.");
            using (Process.Start(StartInfo(Environment.ProcessId, command)))
            {
            }

            exit();
        }
        catch (Exception exception)
        {
            // Nothing else is going to start the app, so it is left running as it is
            failed?.Invoke(exception);
        }
    }

    /// <summary>
    /// The shell that waits for <paramref name="processId" /> to exit and then starts
    /// <paramref name="command" />.
    /// </summary>
    public static ProcessStartInfo StartInfo(int processId, string command)
    {
        if (OperatingSystem.IsWindows())
        {
            return new("powershell.exe")
            {
                ArgumentList =
                {
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Unrestricted",
                    "-EncodedCommand",
                    Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(WindowsScript(processId, command)))
                },
                UseShellExecute = false,
                CreateNoWindow = true,
                // Not the app's own folder, which an update may want to delete
                WorkingDirectory = Path.GetTempPath()
            };
        }

        ProcessStartInfo info;
        if (OperatingSystem.IsMacOS())
        {
            // launchd ends a login item's whole process group as the item exits, so the script runs
            // as a job with job control on, in a group of its own, as ToolUpdate's does
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
        info.WorkingDirectory = Path.GetTempPath();
        // Positional, so no path is quoted into the script
        info.ArgumentList.Add("restart");
        info.ArgumentList.Add(processId.ToString(CultureInfo.InvariantCulture));
        info.ArgumentList.Add(command);
        return info;
    }

    /// <summary>
    /// Waits for the app, then starts it as <see cref="ToolUpdate.WindowsScript" /> starts the shim,
    /// by the call operator: a GUI app is not waited for, and does not take the shell's hidden
    /// console.
    /// </summary>
    public static string WindowsScript(int processId, string command) =>
        string.Join(
            "; ",
            $"Wait-Process -Id {processId} -Timeout {waitSeconds} -ErrorAction SilentlyContinue",
            $"& '{command.Replace("'", "''")}'");

    /// <summary>
    /// For macOS and Linux: $1 the app's process and $2 the command. The count bounds the wait as
    /// -Timeout does on Windows.
    /// </summary>
    public static string UnixScript { get; } = string.Join(
        "; ",
        "waited=0",
        $"while kill -0 \"$1\" 2>/dev/null && [ $waited -lt {waitSeconds * 5} ]; do sleep 0.2; waited=$((waited+1)); done",
        "nohup \"$2\" < /dev/null > /dev/null 2>&1 &");

    /// <summary>
    /// What /bin/bash is handed on macOS. After <see cref="UnixScript" />, since a static is set in
    /// the order it is written.
    /// </summary>
    public static string Job { get; } = $"set -m; ( {UnixScript} ) &";

    /// <summary>
    /// Loads every assembly <see cref="Process" /> refers to, and builds the start, so that starting
    /// the shell loads nothing. Measured on Windows: starting a process loaded System.Threading,
    /// System.Memory, Microsoft.Win32.Primitives and System.Runtime.InteropServices beyond what
    /// building its start info had, and nothing once these were loaded.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "An app this watches runs on the shared framework, untrimmed")]
    static void Preload(string command, Action<Exception>? failed)
    {
        foreach (var name in typeof(Process).Assembly.GetReferencedAssemblies())
        {
            try
            {
                Assembly.Load(name);
            }
            catch (Exception exception)
            {
                failed?.Invoke(exception);
            }
        }

        _ = StartInfo(Environment.ProcessId, command);
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref done, 1);
        timer.Dispose();
    }
}
