using Microsoft.Win32;

namespace PlatformHeads;

/// <summary>
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run. The value is a command line, so the path is
/// quoted: an unquoted path with a space in it is read as a program and an argument.
/// </summary>
public sealed class WindowsRunKey(LoginItem item) : IRunAtLogin
{
    const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public bool Exists()
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetValue(item.Name) is not null;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, true) ??
                        throw new InvalidOperationException("The Run key does not exist");
        if (enabled)
        {
            key.SetValue(item.Name, Value(item.Command()));
        }
        else
        {
            key.DeleteValue(item.Name, false);
        }
    }

    public static string Value(string path) =>
        $"\"{path}\"";
}
