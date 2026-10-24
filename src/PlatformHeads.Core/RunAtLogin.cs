namespace PlatformHeads;

public static class RunAtLogin
{
    /// <summary>
    /// The Run key on Windows, a launch agent on macOS, and an XDG autostart entry everywhere else.
    /// </summary>
    public static IRunAtLogin ForPlatform(LoginItem item)
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsRunKey(item);
        }

        if (OperatingSystem.IsMacOS())
        {
            return new LaunchAgent(item);
        }

        return new XdgAutostart(item);
    }
}
