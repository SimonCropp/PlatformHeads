/// <summary>
/// An app whose runtime a servicing update deleted under it starts itself again on the version that
/// replaced it. Framework folders stood in for under a temp folder, and the app and its command by
/// processes and scripts of the test's.
/// </summary>
public class RuntimeWatchTests
{
    /// <summary>
    /// A test process runs on the shared framework, as an app does.
    /// </summary>
    [Test]
    public async Task FindsTheFolderTheFrameworkWasLoadedFrom()
    {
        var frameworks = RuntimeWatch.FrameworkDirectories(AppDomain.CurrentDomain.GetAssemblies());

        var coreLib = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        await Assert.That(frameworks).Contains(coreLib);
        // Once, though most of the framework was loaded from it
        await Assert.That(frameworks.Count(_ => _ == coreLib)).IsEqualTo(1);
        // And nothing that is not a shared framework's, as the tests' own folder is not
        await Assert.That(frameworks).DoesNotContain(Path.GetDirectoryName(typeof(RuntimeWatchTests).Assembly.Location)!);
    }

    [Test]
    public async Task AFrameworkStillThereIsNotReplaced()
    {
        using var root = new FrameworkRoot();
        var core = root.Framework("Microsoft.NETCore.App", "10.0.14");

        await Assert.That(RuntimeWatch.Gone([core])).IsFalse();
        await Assert.That(RuntimeWatch.Replaced([core])).IsFalse();
    }

    /// <summary>
    /// Started then, the app would find no runtime, so it waits for the install to land.
    /// </summary>
    [Test]
    public async Task AFrameworkGoneWithNothingBesideItIsWaitedOn()
    {
        using var root = new FrameworkRoot();
        var core = root.Framework("Microsoft.NETCore.App", "10.0.14");
        Directory.Delete(core);

        await Assert.That(RuntimeWatch.Gone([core])).IsTrue();
        await Assert.That(RuntimeWatch.Replaced([core])).IsFalse();
    }

    [Test]
    [Arguments("10.0.15")]
    [Arguments("10.0.13")]
    [Arguments("10.1.0")]
    public async Task AFrameworkGoneWithAnotherOfItsMajorBesideItIsReplaced(string other)
    {
        using var root = new FrameworkRoot();
        var core = root.Framework("Microsoft.NETCore.App", "10.0.14");
        root.Framework("Microsoft.NETCore.App", other);
        Directory.Delete(core);

        await Assert.That(RuntimeWatch.Replaced([core])).IsTrue();
    }

    /// <summary>
    /// An app built for net10.0 does not roll forward to another major.
    /// </summary>
    [Test]
    public async Task AnotherMajorIsNoReplacement()
    {
        using var root = new FrameworkRoot();
        var core = root.Framework("Microsoft.NETCore.App", "10.0.14");
        root.Framework("Microsoft.NETCore.App", "11.0.0");
        Directory.Delete(core);

        await Assert.That(RuntimeWatch.Replaced([core])).IsFalse();
    }

    /// <summary>
    /// A Windows head also runs on the desktop framework, which an update replaces after the core
    /// one.
    /// </summary>
    [Test]
    public async Task EveryFrameworkGoneHasToBeReplaced()
    {
        using var root = new FrameworkRoot();
        var core = root.Framework("Microsoft.NETCore.App", "10.0.14");
        root.Framework("Microsoft.NETCore.App", "10.0.15");
        var desktop = root.Framework("Microsoft.WindowsDesktop.App", "10.0.14");
        Directory.Delete(core);
        Directory.Delete(desktop);

        await Assert.That(RuntimeWatch.Replaced([core, desktop])).IsFalse();

        root.Framework("Microsoft.WindowsDesktop.App", "10.0.15");
        await Assert.That(RuntimeWatch.Replaced([core, desktop])).IsTrue();
    }

    /// <summary>
    /// Under a user name with a quote in it, so the quoting is pinned as well.
    /// </summary>
    [Test]
    public async Task TheWindowsScript() =>
        await Assert.That(RuntimeWatch.WindowsScript(4242, @"C:\Users\O'Brien\.dotnet\tools\sampleapp.exe"))
            .IsEqualTo("""Wait-Process -Id 4242 -Timeout 30 -ErrorAction SilentlyContinue; & 'C:\Users\O''Brien\.dotnet\tools\sampleapp.exe'""");

    [Test]
    public async Task TheUnixScript() =>
        await Assert.That(RuntimeWatch.UnixScript)
            .IsEqualTo("""waited=0; while kill -0 "$1" 2>/dev/null && [ $waited -lt 150 ]; do sleep 0.2; waited=$((waited+1)); done; nohup "$2" < /dev/null > /dev/null 2>&1 &""");

    /// <summary>
    /// macOS's job read by bash, so the ubuntu job finds what only the macOS one runs.
    /// </summary>
    [Test]
    [RunOn(TUnit.Core.Enums.OS.Linux | TUnit.Core.Enums.OS.MacOs)]
    public async Task TheMacJobIsBash()
    {
        var info = new ProcessStartInfo("/bin/bash", ["-n", "-c", RuntimeWatch.Job])
        {
            UseShellExecute = false,
            RedirectStandardError = true
        };
        using var bash = Process.Start(info)!;
        var error = await bash.StandardError.ReadToEndAsync();
        await bash.WaitForExitAsync();

        await Assert.That(error).IsEmpty();
        await Assert.That(bash.ExitCode).IsEqualTo(0);
    }

    /// <summary>
    /// The shell this platform starts, run: it waits for the app, which holds the single instance
    /// lock, and then starts the command.
    /// </summary>
    [Test]
    public async Task StartsTheCommandOnceTheAppHasExited()
    {
        using var root = new FrameworkRoot();
        var started = Path.Combine(root.Path, "started.txt");
        var command = Command(root.Path, started);
        using var app = App();
        try
        {
            using (Process.Start(RuntimeWatch.StartInfo(app.Id, command)))
            {
            }

            // Only a statement about what has not happened yet, which a slow machine cannot make
            // false
            await Task.Delay(TimeSpan.FromSeconds(1));
            await Assert.That(File.Exists(started)).IsFalse();

            app.Kill();
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (!File.Exists(started) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
            }

            await Assert.That(File.Exists(started)).IsTrue();
        }
        finally
        {
            if (!app.HasExited)
            {
                app.Kill();
            }
        }
    }

    /// <summary>
    /// A process that runs until the test ends it, standing in for the app.
    /// </summary>
    static Process App()
    {
        var info = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("ping.exe", ["-n", "120", "127.0.0.1"])
            : new ProcessStartInfo("/bin/sleep", ["120"]);
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardOutput = true;
        return Process.Start(info)!;
    }

    /// <summary>
    /// The app's command, a script that says it was started. On Windows written under another name
    /// and renamed, so the file is there only once cmd has closed it, and can be deleted.
    /// </summary>
    static string Command(string directory, string started)
    {
        if (OperatingSystem.IsWindows())
        {
            var command = Path.Combine(directory, "sampleapp.cmd");
            var writing = $"{started}.writing";
            File.WriteAllText(command, $"@echo off\r\necho started> \"{writing}\"\r\nren \"{writing}\" \"{Path.GetFileName(started)}\"\r\n");
            return command;
        }

        var unixCommand = Path.Combine(directory, "sampleapp");
        File.WriteAllText(unixCommand, $"#!/bin/sh\necho started > \"{started}\"\n");
        File.SetUnixFileMode(unixCommand, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return unixCommand;
    }

    sealed class FrameworkRoot : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"PlatformHeads.RuntimeWatch.{Guid.NewGuid():N}");

        public FrameworkRoot() =>
            Directory.CreateDirectory(Path);

        public string Framework(string name, string version)
        {
            var directory = System.IO.Path.Combine(Path, "shared", name, version);
            Directory.CreateDirectory(directory);
            return directory;
        }

        /// <summary>
        /// Again a little later while something in it is in use, as ToolUpdateTests does: a scanner
        /// looking at a file a script just wrote can hold it as the test ends.
        /// </summary>
        public void Dispose()
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    Directory.Delete(Path, true);
                    return;
                }
                catch (Exception exception)
                    when ((exception is IOException or UnauthorizedAccessException) &&
                          attempt < 20)
                {
                    Thread.Sleep(250);
                }
            }
        }
    }
}
