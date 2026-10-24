namespace PlatformHeads;

/// <summary>
/// How an app is named where the desktop starts it at login, by <see cref="RunAtLogin" />.
/// </summary>
/// <param name="Name">The Run key's value name on Windows and the desktop entry's name on Linux.</param>
/// <param name="Comment">The desktop entry's comment.</param>
/// <param name="FileName">The desktop entry's file name, without <c>.desktop</c>.</param>
/// <param name="Label">The launch agent's label in reverse DNS, which is also its plist's name.</param>
/// <param name="Command">
/// The program to start, asked as the entry is written rather than when this is made. A path that
/// survives an update, such as a .NET tool's shim, rather than the running executable's, which an
/// update deletes.
/// </param>
public sealed record LoginItem(string Name, string Comment, string FileName, string Label, Func<string> Command);
