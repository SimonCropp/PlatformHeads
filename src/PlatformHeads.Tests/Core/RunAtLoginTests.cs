/// <summary>
/// What each platform's entry says, without writing it: no test touches the registry, the
/// autostart folder or launchd.
/// </summary>
public class RunAtLoginTests
{
    static readonly LoginItem item = new("SampleApp", "A sample tray app", "sampleapp", "com.example.sampleapp", () => "/unused");

    [Test]
    public async Task TheDesktopEntryStartsTheCommand() =>
        await Assert.That(XdgAutostart.Compose(item, "/home/someone/.dotnet/tools/sampleapp"))
            .IsEqualTo(
                """
                [Desktop Entry]
                Type=Application
                Name=SampleApp
                Comment=A sample tray app
                Exec="/home/someone/.dotnet/tools/sampleapp"
                Terminal=false
                X-GNOME-Autostart-enabled=true

                """);

    [Test]
    public async Task TheDesktopEntryIsNamedByTheItem()
    {
        var path = new XdgAutostart(item).DesktopPath;

        await Assert.That(Path.GetFileName(path)).IsEqualTo("sampleapp.desktop");
        await Assert.That(Path.GetFileName(Path.GetDirectoryName(path))).IsEqualTo("autostart");
    }

    /// <summary>
    /// A path with an ampersand in it, as a home directory can have, is escaped rather than ending
    /// the plist's XML there.
    /// </summary>
    [Test]
    public async Task TheLaunchAgentStartsTheCommandOnceAtLogin() =>
        await Assert.That(LaunchAgent.Compose(item, "/Users/R&D/.dotnet/tools/sampleapp"))
            .IsEqualTo(
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                    <key>Label</key>
                    <string>com.example.sampleapp</string>
                    <key>ProgramArguments</key>
                    <array>
                        <string>/Users/R&amp;D/.dotnet/tools/sampleapp</string>
                    </array>
                    <key>RunAtLoad</key>
                    <true/>
                    <key>LimitLoadToSessionType</key>
                    <string>Aqua</string>
                    <key>AbandonProcessGroup</key>
                    <true/>
                </dict>
                </plist>

                """);

    [Test]
    public async Task TheLaunchAgentIsNamedByItsLabel() =>
        await Assert.That(Path.GetFileName(new LaunchAgent(item).PlistPath)).IsEqualTo("com.example.sampleapp.plist");

    /// <summary>
    /// The value is a command line, and an unquoted path with a space in it is read as a program
    /// and an argument.
    /// </summary>
    [Test]
    public async Task TheRunKeyValueIsQuoted() =>
        await Assert.That(WindowsRunKey.Value(@"C:\Program Files\x\sampleapp.exe")).IsEqualTo("\"C:\\Program Files\\x\\sampleapp.exe\"");
}
