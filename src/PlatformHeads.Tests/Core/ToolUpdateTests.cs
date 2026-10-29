/// <summary>
/// The update an app installed as a dotnet tool runs on itself, with a stand-in for dotnet and for
/// the shim, so nothing is installed or updated.
/// </summary>
public class ToolUpdateTests
{
    /// <summary>
    /// A RID specific tool, as .NET 10 lays one out, and a portable one.
    /// </summary>
    [Test]
    [Arguments("diffenginetray.osx-arm64", "osx-arm64")]
    [Arguments("buildmonitor", "any")]
    public async Task TheShimIsBesideTheStore(string package, string rid)
    {
        var tools = Path.Combine(Path.GetTempPath(), "home", ".dotnet", "tools");
        var file = Path.Combine(tools, ".store", "sampleapp", "1.0.0", package, "1.0.0", "tools", "net10.0", rid, "SampleApp.dll");
        var expected = Path.Combine(tools, OperatingSystem.IsWindows() ? "SampleApp.exe" : "SampleApp");

        await Assert.That(ToolUpdate.ShimFor(file, "SampleApp")).IsEqualTo(expected);
    }

    [Test]
    public async Task OutsideAStoreThereIsNoShim() =>
        await Assert.That(ToolUpdate.ShimFor(Path.Combine(Path.GetTempPath(), "bin", "Release", "SampleApp.dll"), "SampleApp")).IsNull();

    [Test]
    public async Task TheToolPathIsTheShimsFolder()
    {
        var tools = Path.Combine(Path.GetTempPath(), "tools");
        var update = new ToolUpdate("SampleApp", Path.Combine(tools, "sampleapp"), "outcome");

        await Assert.That(update.ToolPath).IsEqualTo(tools);
        await Assert.That(update.StoreDirectory).IsEqualTo(Path.Combine(tools, ".store", "SampleApp"));
    }

    /// <summary>
    /// Which shell this platform starts, and that the script's arguments follow it in order. The
    /// scripts themselves are pinned below, and run by the test after that.
    /// </summary>
    [Test]
    public async Task ThisPlatformsShellIsStarted()
    {
        var tools = Path.Combine(Path.GetTempPath(), "tools");
        var update = new ToolUpdate("SampleApp", Path.Combine(tools, "sampleapp"), "outcome")
        {
            Dotnet = "the-dotnet"
        };

        var info = update.StartInfo(4242);

        if (OperatingSystem.IsWindows())
        {
            await Assert.That(info.FileName).IsEqualTo("powershell.exe");
            // Anywhere but the store, whose folders the update deletes, and Windows will not delete
            // one that a running process is sitting in
            await Assert.That(info.WorkingDirectory).IsEqualTo(Path.GetTempPath());
            var script = Encoding.Unicode.GetString(Convert.FromBase64String(info.ArgumentList.Last()));
            await Assert.That(script).IsEqualTo(update.WindowsScript(4242));
            return;
        }

        string[] arguments = ["update", "4242", "the-dotnet", "SampleApp", tools, "outcome", Path.Combine(tools, "sampleapp")];
        if (OperatingSystem.IsMacOS())
        {
            await Assert.That(info.FileName).IsEqualTo("/bin/bash");
            await Assert.That(Lines(info.ArgumentList)).IsEqualTo(Lines(["-c", $"set -m; {{ {ToolUpdate.UnixScript}; }} &", ..arguments]));
            return;
        }

        await Assert.That(info.FileName).IsEqualTo("setsid");
        await Assert.That(Lines(info.ArgumentList)).IsEqualTo(Lines(["/bin/sh", "-c", ToolUpdate.UnixScript, ..arguments]));
    }

    /// <summary>
    /// In order, which is the point of positional arguments.
    /// </summary>
    static string Lines(IEnumerable<string> arguments) =>
        string.Join('\n', arguments);

    /// <summary>
    /// Under a user name with a quote in it, so the quoting is pinned as well, and with a folder
    /// besides the store to stop processes under.
    /// </summary>
    [Test]
    [RunOn(TUnit.Core.Enums.OS.Windows)]
    public async Task TheWindowsScript()
    {
        var update = new ToolUpdate("SampleApp", @"C:\Users\O'Brien\.dotnet\tools\sampleapp.exe", @"C:\Users\O'Brien\AppData\Local\SampleApp\update-outcome.log")
        {
            Dotnet = @"C:\Program Files\dotnet\dotnet.exe",
            AlsoStopUnder = [@"C:\Users\O'Brien\AppData\Local\SampleApp\head\"]
        };

        await Assert.That(update.WindowsScript(4242))
            .IsEqualTo("""Wait-Process -Id 4242 -Timeout 30 -ErrorAction SilentlyContinue; $locking = @(Get-Process | Where-Object { $_.Path -and ($_.Path -eq 'C:\Users\O''Brien\.dotnet\tools\sampleapp.exe' -or $_.Path.StartsWith('C:\Users\O''Brien\.dotnet\tools\.store\SampleApp\', 'OrdinalIgnoreCase') -or $_.Path.StartsWith('C:\Users\O''Brien\AppData\Local\SampleApp\head\', 'OrdinalIgnoreCase')) }); $locking | Stop-Process -Force -ErrorAction SilentlyContinue; $locking | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue; $output = & 'C:\Program Files\dotnet\dotnet.exe' tool update SampleApp --tool-path 'C:\Users\O''Brien\.dotnet\tools' --prerelease 2>&1 | ForEach-Object { "$_" }; $status = if ($LASTEXITCODE -eq 0) { 'ok' } else { 'failed' }; Set-Content -LiteralPath 'C:\Users\O''Brien\AppData\Local\SampleApp\update-outcome.log' -Value (@($status) + $output) -Encoding UTF8; & 'C:\Users\O''Brien\.dotnet\tools\sampleapp.exe'""");
    }

    [Test]
    public async Task TheUnixScript() =>
        await Assert.That(ToolUpdate.UnixScript)
            .IsEqualTo("""waited=0; while kill -0 "$1" 2>/dev/null && [ $waited -lt 150 ]; do sleep 0.2; waited=$((waited+1)); done; if output=$("$2" tool update "$3" --tool-path "$4" --prerelease 2>&1); then status=ok; else status=failed; fi; printf '%s\n%s\n' "$status" "$output" > "$5"; nohup "$6" < /dev/null > /dev/null 2>&1 &""");

    /// <summary>
    /// The shell this platform starts, run: it waits for the app, updates by the tool path, says
    /// how that went, and starts the shim whether it worked or not. The app is a process of the
    /// test's, and dotnet and the shim are scripts that say what they were asked.
    /// </summary>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task TheUpdateWaitsForTheAppThenStartsTheShim(bool succeeds)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"PlatformHeads.ToolUpdate.{Guid.NewGuid():N}");
        var tools = Path.Combine(directory, "tools");
        Directory.CreateDirectory(tools);
        var updated = Path.Combine(directory, "updated.txt");
        var started = Path.Combine(directory, "started.txt");
        var outcome = Path.Combine(directory, "outcome.log");
        var said = succeeds ? "Tool 'sampleapp' was successfully updated." : "It could not be updated.";
        var (dotnet, shim) = StandIns(directory, tools, updated, started, said, succeeds ? 0 : 1);
        using var app = App();
        try
        {
            var update = new ToolUpdate("SampleApp", shim, outcome)
            {
                Dotnet = dotnet
            };
            using (Process.Start(update.StartInfo(app.Id)))
            {
            }

            // Only a statement about what has not happened yet, which a slow machine cannot make
            // false
            await Task.Delay(TimeSpan.FromSeconds(1));
            await Assert.That(File.Exists(updated)).IsFalse();

            app.Kill();
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (!File.Exists(started) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
            }

            await Assert.That(File.Exists(started)).IsTrue();
            var arguments = (await File.ReadAllTextAsync(updated)).Split((char[]) ['\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries);
            await Assert.That(Lines(arguments)).IsEqualTo(Lines(["tool", "update", "SampleApp", "--tool-path", tools, "--prerelease"]));
            var result = ToolUpdateOutcome.Take(outcome)!;
            await Assert.That(result.Succeeded).IsEqualTo(succeeds);
            await Assert.That(result.Reason).IsEqualTo(said);
        }
        finally
        {
            if (!app.HasExited)
            {
                app.Kill();
            }

            Directory.Delete(directory, true);
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
    /// dotnet, which writes the arguments it was given and then what it says, and exits with
    /// <paramref name="code" />, and the shim, which says it was started. Each a script, since the
    /// shell starts whatever it is named. Paths with no spaces, as a temp folder's are, so a .cmd
    /// can write them as they came.
    /// </summary>
    static (string Dotnet, string Shim) StandIns(string directory, string tools, string updated, string started, string said, int code)
    {
        if (OperatingSystem.IsWindows())
        {
            var dotnet = Path.Combine(directory, "dotnet.cmd");
            File.WriteAllText(dotnet, $"@echo off\r\necho %*> \"{updated}\"\r\necho {said}\r\nexit /b {code}\r\n");
            var shim = Path.Combine(tools, "sampleapp.cmd");
            File.WriteAllText(shim, $"@echo off\r\necho started> \"{started}\"\r\n");
            return (dotnet, shim);
        }

        var unixDotnet = Path.Combine(directory, "dotnet");
        File.WriteAllText(unixDotnet, $"#!/bin/sh\nprintf '%s\\n' \"$@\" > \"{updated}\"\necho \"{said}\"\nexit {code}\n");
        var unixShim = Path.Combine(tools, "sampleapp");
        File.WriteAllText(unixShim, $"#!/bin/sh\necho started > \"{started}\"\n");
        foreach (var script in (string[]) [unixDotnet, unixShim])
        {
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return (unixDotnet, unixShim);
    }
}
