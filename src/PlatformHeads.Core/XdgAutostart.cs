namespace PlatformHeads;

/// <summary>
/// A desktop entry in ~/.config/autostart, or under XDG_CONFIG_HOME where that is set, which every
/// freedesktop session starts at login.
/// </summary>
public sealed class XdgAutostart(LoginItem item) : IRunAtLogin
{
    public string DesktopPath
    {
        get
        {
            var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(config))
            {
                config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            }

            return Path.Combine(config, "autostart", $"{item.FileName}.desktop");
        }
    }

    public bool Exists() =>
        File.Exists(DesktopPath);

    public void Set(bool enabled)
    {
        var path = DesktopPath;
        if (enabled)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Compose(item, item.Command()));
            return;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static string Compose(LoginItem item, string command) =>
        $"""
         [Desktop Entry]
         Type=Application
         Name={item.Name}
         Comment={item.Comment}
         Exec="{command}"
         Terminal=false
         X-GNOME-Autostart-enabled=true

         """;
}
