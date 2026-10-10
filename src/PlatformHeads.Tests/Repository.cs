/// <summary>
/// Where the repository is, found from where this file was compiled, since what these tests are
/// about is files of the repository and not anything built into the test assembly.
/// </summary>
static class Repository
{
    public static string Root { get; } = FindRoot();

    public static string Workflow =>
        Path.Combine(Root, ".github", "workflows", "build-native.yml");

    public static string BuildScript =>
        Path.Combine(Root, "native", "build-linux.sh");

    /// <summary>
    /// The folder a package reference would import the props and targets from, ending in a
    /// separator, which is how the fixture's imports use it.
    /// </summary>
    public static string BuildFolder =>
        Path.Combine(Root, "src", "PlatformHeads", "build") + Path.DirectorySeparatorChar;

    public static string Fixture =>
        Path.Combine(Root, "src", "PlatformHeads.Tests", "Fixture");

    static string FindRoot([CallerFilePath] string file = "")
    {
        var directory = Path.GetDirectoryName(file);
        while (directory is not null &&
               !Directory.Exists(Path.Combine(directory, ".github")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        return directory ?? throw new("Could not find the repository root above " + file);
    }
}
