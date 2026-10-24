namespace PlatformHeads;

/// <summary>
/// A per user launchd agent in ~/Library/LaunchAgents, loaded into the GUI session. Not
/// SMAppService, which only registers helpers inside an app bundle, and a .NET tool is not one.
/// </summary>
public sealed class LaunchAgent(LoginItem item) : IRunAtLogin
{
    public string PlistPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", $"{item.Label}.plist");

    public bool Exists() =>
        File.Exists(PlistPath);

    public void Set(bool enabled)
    {
        var domain = $"gui/{ProcessRunner.Run("/usr/bin/id", ["-u"]).Output.Trim()}";
        var path = PlistPath;
        if (enabled)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Compose(item, item.Command()));
            ProcessRunner.Run("/bin/launchctl", ["bootstrap", domain, path]);
            return;
        }

        if (File.Exists(path))
        {
            ProcessRunner.Run("/bin/launchctl", ["bootout", $"{domain}/{item.Label}"]);
            File.Delete(path);
        }
    }

    /// <summary>
    /// RunAtLoad without KeepAlive: started once at login, and the user can quit it.
    /// </summary>
    public static string Compose(LoginItem item, string command) =>
        $"""
         <?xml version="1.0" encoding="UTF-8"?>
         <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
         <plist version="1.0">
         <dict>
             <key>Label</key>
             <string>{System.Security.SecurityElement.Escape(item.Label)}</string>
             <key>ProgramArguments</key>
             <array>
                 <string>{System.Security.SecurityElement.Escape(command)}</string>
             </array>
             <key>RunAtLoad</key>
             <true/>
             <key>LimitLoadToSessionType</key>
             <string>Aqua</string>
         </dict>
         </plist>

         """;
}
