/// <summary>
/// A copy of the fixture app in a folder of its own, outside the repository, so that each test
/// builds its own and none of them takes anything from the Directory.Build.props above this one.
/// </summary>
sealed class FixtureApp : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "PlatformHeads.Tests", Guid.NewGuid().ToString("N"));

    public string HeadRuntimes =>
        Path.Combine(Root, "Head", "runtimes");

    public FixtureApp()
    {
        foreach (var source in Directory.EnumerateFiles(Repository.Fixture, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(Root, Path.GetRelativePath(Repository.Fixture, source));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
        }
    }

    /// <summary>
    /// Stands in for a committed native library. Written here and not kept in the repository,
    /// where a file named for a library that is not one would be found by whoever looked.
    /// </summary>
    public void AddNative(string runtimeIdentifier)
    {
        var directory = Path.Combine(HeadRuntimes, runtimeIdentifier, "native");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "libfixture.so"), runtimeIdentifier);
    }

    /// <summary>
    /// Packs the app, which is what bundles its heads. The props and targets are named through
    /// the environment and not as a property on the command line: each head is published by a
    /// dotnet of its own, which inherits the one and not the other.
    /// </summary>
    public Task<RunResult> Pack(params string[] properties) =>
        Run.Program(
            "dotnet",
            ["pack", Path.Combine(Root, "App", "App.csproj"), "--configuration", "Release", "--output", Path.Combine(Root, "packages"), .. properties],
            Root,
            new Dictionary<string, string>
            {
                ["PlatformHeadsBuild"] = Repository.BuildFolder
            });

    /// <summary>
    /// The paths in the package that was packed, with forward slashes.
    /// </summary>
    public List<string> PackageEntries()
    {
        using var archive = ZipFile.OpenRead(Path.Combine(Root, "packages", "Fixture.App.1.0.0.nupkg"));
        return archive.Entries
            .Select(_ => Uri.UnescapeDataString(_.FullName))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, true);
        }
        catch (IOException)
        {
            // A build server still holding a file is no reason to fail a test that passed.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
