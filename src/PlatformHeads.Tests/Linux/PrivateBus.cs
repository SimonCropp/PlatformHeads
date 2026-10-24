/// <summary>
/// A session bus of the test's own, from <c>dbus-daemon</c>, so nothing is put in the tray of
/// whoever runs the tests, and a bus test finds only what it registered.
/// </summary>
sealed class PrivateBus : IAsyncDisposable
{
    readonly Process daemon;

    PrivateBus(Process daemon, string address)
    {
        this.daemon = daemon;
        Address = address;
    }

    public string Address { get; }

    /// <summary>
    /// Fails rather than skips where there is no <c>dbus-daemon</c>: a Linux test that skipped
    /// quietly would leave the tray untested on the one platform it is for.
    /// </summary>
    public static async Task<PrivateBus> Start()
    {
        var info = new ProcessStartInfo("dbus-daemon")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        info.ArgumentList.Add("--session");
        info.ArgumentList.Add("--nofork");
        info.ArgumentList.Add("--print-address");
        Process daemon;
        try
        {
            daemon = Process.Start(info)!;
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new InvalidOperationException("dbus-daemon is needed for the tray's bus tests. Install the dbus package.", exception);
        }

        var address = await daemon.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
        if (string.IsNullOrEmpty(address))
        {
            daemon.Kill();
            throw new InvalidOperationException($"dbus-daemon gave no address: {await daemon.StandardError.ReadToEndAsync()}");
        }

        return new(daemon, address.Trim());
    }

    public async ValueTask DisposeAsync()
    {
        if (!daemon.HasExited)
        {
            daemon.Kill();
            await daemon.WaitForExitAsync();
        }

        daemon.Dispose();
    }
}
